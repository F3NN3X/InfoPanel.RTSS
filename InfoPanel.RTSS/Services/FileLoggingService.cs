using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Log levels for filtering debug output
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1, 
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Provides file-based logging for debugging user issues.
    /// Writes to debug.log in the plugin directory when debug mode is enabled.
    /// Includes batched writing, message throttling, and log rotation to keep log files manageable.
    /// </summary>
    public class FileLoggingService : IDisposable
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();
        private readonly ConfigurationService _configService;
        private bool _disposed = false;

        // Batched logging system for performance - much less aggressive
        private readonly List<string> _logBuffer = new();
        private readonly Timer _flushTimer;
        private readonly TimeSpan _flushInterval = TimeSpan.FromMilliseconds(500); // Write batches every 500ms (2x per second)
        private const int MAX_BUFFER_SIZE = 20; // Smaller buffer to prevent batching too much

        // Log rotation settings
        private const long MAX_LOG_SIZE_BYTES = 5 * 1024 * 1024; // 5MB max log file size
        private const int MAX_BACKUP_FILES = 3; // Keep 3 backup files

        // Smart throttling system with different intervals for different message types
        private readonly Dictionary<string, (DateTime lastLogged, int count, DateTime firstSeen)> _messageThrottle = new();
        
        // Adaptive throttling intervals based on debug mode and message importance
        private readonly Dictionary<string, TimeSpan> _throttleIntervals = new();
        
        private DateTime _lastRTSSPollLog = DateTime.MinValue;
        private int _rtssPollingCount = 0;
        
        // Burst allowance - allow first N messages of each type to go through quickly
        private readonly Dictionary<string, int> _burstCounts = new();
        private const int DEFAULT_BURST_ALLOWANCE = 5; // Allow first 5 messages quickly

        // Log level filtering (can be configured via INI file)
        private readonly LogLevel _minimumLogLevel;

        /// <summary>
        /// Initializes throttling intervals based on debug mode and message types
        /// </summary>
        private void InitializeThrottleIntervals()
        {
            if (_configService.IsDebugEnabled)
            {
                // Developer mode - more frequent logging for active debugging
                _throttleIntervals["RTSS_OPERATIONS"] = TimeSpan.FromSeconds(10);      // RTSS polling/scanning
                _throttleIntervals["PERFORMANCE_UPDATES"] = TimeSpan.FromSeconds(15);  // FPS updates
                _throttleIntervals["ENHANCED_METRICS"] = TimeSpan.FromSeconds(20);     // Enhanced metrics
                _throttleIntervals["MONITORING_STATE"] = TimeSpan.FromSeconds(5);     // State changes
                _throttleIntervals["SYSTEM_INFO"] = TimeSpan.FromSeconds(30);         // System info
                _throttleIntervals["SENSOR_UPDATES"] = TimeSpan.FromSeconds(12);      // Sensor updates
                _throttleIntervals["WINDOW_DETECTION"] = TimeSpan.FromSeconds(8);     // Window detection
                _throttleIntervals["API_DETECTION"] = TimeSpan.FromSeconds(3);        // Graphics API detection
                _throttleIntervals["DEFAULT"] = TimeSpan.FromSeconds(20);             // Fallback
            }
            else
            {
                // Production mode - minimal logging (original intervals)
                _throttleIntervals["RTSS_OPERATIONS"] = TimeSpan.FromMinutes(2);
                _throttleIntervals["PERFORMANCE_UPDATES"] = TimeSpan.FromMinutes(1);
                _throttleIntervals["ENHANCED_METRICS"] = TimeSpan.FromMinutes(1);
                _throttleIntervals["MONITORING_STATE"] = TimeSpan.FromSeconds(30);
                _throttleIntervals["SYSTEM_INFO"] = TimeSpan.FromMinutes(2);
                _throttleIntervals["SENSOR_UPDATES"] = TimeSpan.FromMinutes(1);
                _throttleIntervals["WINDOW_DETECTION"] = TimeSpan.FromSeconds(45);
                _throttleIntervals["API_DETECTION"] = TimeSpan.FromSeconds(30);
                _throttleIntervals["DEFAULT"] = TimeSpan.FromMinutes(1);
            }
        }

        public FileLoggingService(ConfigurationService configService)
        {
            _configService = configService;
            
            // Set minimum log level (Warning by default for minimal logging, Info when debug enabled)
            _minimumLogLevel = _configService.IsDebugEnabled ? LogLevel.Info : LogLevel.Warning;
            
            // Initialize smart throttling intervals based on debug mode
            InitializeThrottleIntervals();
            
            try
            {
                // Get the directory where the DLL is located
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? Environment.CurrentDirectory;
                _logFilePath = Path.Combine(pluginDirectory, "debug.log");

                // Initialize batched flush timer (convert TimeSpan to milliseconds for Timer constructor)
                _flushTimer = new Timer(FlushLogBuffer, null, (int)_flushInterval.TotalMilliseconds, (int)_flushInterval.TotalMilliseconds);

                // Only initialize log file if debug mode is enabled
                if (_configService.IsDebugEnabled)
                {
                    AddLogEntry(LogLevel.Info, "=== InfoPanel.RTSS Debug Log Started (Minimal Logging Mode) ===");
                    AddLogEntry(LogLevel.Info, $"Batched Logging: {_flushInterval.TotalMilliseconds}ms intervals, {MAX_BUFFER_SIZE} entry buffer");
                    // Immediate flush for startup messages
                    lock (_lock) { FlushLogBufferUnsafe(); }
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"FileLoggingService: Failed to initialize file logging: {ex.Message}"); // Keep console for file logger initialization errors
                _logFilePath = string.Empty;
                // Initialize timer even if logging fails to prevent null reference
                _flushTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        public void LogInfo(string message)
        {
            AddLogEntry(LogLevel.Info, message);
        }

        public void LogWarning(string message)
        {
            AddLogEntry(LogLevel.Warning, message);
        }

        public void LogError(string message)
        {
            AddLogEntry(LogLevel.Error, message);
        }

        public void LogError(string message, Exception ex)
        {
            AddLogEntry(LogLevel.Error, $"{message}: {ex.Message}");
            AddLogEntry(LogLevel.Error, $"Stack Trace: {ex.StackTrace}");
        }

        /// <summary>
        /// Logs important information that should always be recorded (bypasses throttling)
        /// </summary>
        public void LogImportant(string message)
        {
            AddLogEntry(LogLevel.Info, $"[IMPORTANT] {message}");
        }

        public void LogDebug(string message)
        {
            AddLogEntry(LogLevel.Debug, message);
        }

        /// <summary>
        /// Logs debug messages (throttling now handled automatically in AddLogEntry)
        /// </summary>
        public void LogDebugThrottled(string message, string? throttleKey = null)
        {
            // Throttling is now handled automatically in AddLogEntry
            AddLogEntry(LogLevel.Debug, message);
        }

        /// <summary>
        /// Creates an intelligent throttle key based on message patterns with improved categorization
        /// </summary>
        private string CreateThrottleKey(string message)
        {
            // More specific categorization for better throttling control
            if (message.Contains("RTSS Polling") || message.Contains("Scanning RTSS"))
                return "RTSS_OPERATIONS";
            if (message.Contains("API Detection") || message.Contains("Graphics API") || message.Contains("Detected:"))
                return "API_DETECTION";
            if (message.Contains("Performance metrics updated") || message.Contains("FPS Update"))
                return "PERFORMANCE_UPDATES";
            if (message.Contains("Enhanced Gaming Metrics") || message.Contains("Enhanced"))
                return "ENHANCED_METRICS";
            if (message.Contains("sensors updated") || message.Contains("Sensor"))
                return "SENSOR_UPDATES";
            if (message.Contains("Window") || message.Contains("Fullscreen") || message.Contains("capture"))
                return "WINDOW_DETECTION";
            if (message.Contains("Monitoring") && (message.Contains("Started") || message.Contains("Stopped")))
                return "MONITORING_STATE";
            if (message.Contains("System") || message.Contains("GPU") || message.Contains("Display"))
                return "SYSTEM_INFO";
            
            // For other messages, create more specific keys to avoid over-grouping
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0]}_{words[1]}".ToUpper();
            
            return "DEFAULT";
        }

        /// <summary>
        /// Logs RTSS polling activity with very heavy throttling since it happens every 16ms.
        /// </summary>
        public void LogRTSSPolling(string message)
        {
            _rtssPollingCount++;
            var now = DateTime.Now;
            
            // Log every 2 minutes with count summary (much less frequent)
            if (now - _lastRTSSPollLog >= TimeSpan.FromMinutes(2))
            {
                AddLogEntry(LogLevel.Debug, $"RTSS Polling Summary (x{_rtssPollingCount} operations in 2min): Last activity - {message}");
                _lastRTSSPollLog = now;
                _rtssPollingCount = 0;
            }
        }

        private bool ShouldLogThrottledMessage(string key, out int suppressedCount)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                suppressedCount = 0;
                
                // Get throttle interval for this message type
                var throttleInterval = _throttleIntervals.TryGetValue(key, out var interval) ? 
                    interval : _throttleIntervals["DEFAULT"];
                
                if (_messageThrottle.TryGetValue(key, out var existing))
                {
                    _messageThrottle[key] = (existing.lastLogged, existing.count + 1, existing.firstSeen);
                    
                    // Check if we're still in burst allowance period (first 30 seconds)
                    var burstCount = _burstCounts.TryGetValue(key, out var count) ? count : 0;
                    var timeSinceFirst = now - existing.firstSeen;
                    
                    if (burstCount < DEFAULT_BURST_ALLOWANCE && timeSinceFirst <= TimeSpan.FromSeconds(30))
                    {
                        _burstCounts[key] = burstCount + 1;
                        return true; // Allow burst messages
                    }
                    
                    // After burst period, use normal throttling
                    if (now - existing.lastLogged >= throttleInterval)
                    {
                        suppressedCount = existing.count;
                        _messageThrottle[key] = (now, 0, existing.firstSeen);
                        return true;
                    }
                    return false;
                }
                else
                {
                    // First message of this type - always allow and start tracking
                    _messageThrottle[key] = (now, 0, now);
                    _burstCounts[key] = 1;
                    return true;
                }
            }
        }

        // Overload for backward compatibility
        private bool ShouldLogThrottledMessage(string key)
        {
            return ShouldLogThrottledMessage(key, out _);
        }

        /// <summary>
        /// Adds a log entry to the batch buffer with intelligent throttling
        /// </summary>
        private void AddLogEntry(LogLevel level, string message)
        {
            // Only log if debug mode is enabled and level meets minimum threshold
            if (!_configService.IsDebugEnabled || string.IsNullOrEmpty(_logFilePath) || _disposed || level < _minimumLogLevel)
                return;

            // Apply throttling to all messages except critical errors
            if (level != LogLevel.Error && !message.StartsWith("[IMPORTANT]"))
            {
                string throttleKey = CreateThrottleKey(message);
                if (!ShouldLogThrottledMessage(throttleKey, out int suppressedCount))
                {
                    return; // Message was throttled, don't add to buffer
                }
                
                // Add suppression count to message if any were suppressed
                if (suppressedCount > 0)
                {
                    message = $"{message} [+{suppressedCount} similar suppressed]";
                }
            }

            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string levelStr = level.ToString().ToUpper();
                string logLine = $"[{timestamp}] [{levelStr}] {message}";
                
                _logBuffer.Add(logLine);
                
                // Force flush if buffer gets too large to prevent memory issues
                if (_logBuffer.Count >= MAX_BUFFER_SIZE)
                {
                    FlushLogBufferUnsafe();
                }
            }
        }

        /// <summary>
        /// Timer callback to flush log buffer to disk
        /// </summary>
        private void FlushLogBuffer(object? state)
        {
            if (_disposed || string.IsNullOrEmpty(_logFilePath))
                return;

            lock (_lock)
            {
                FlushLogBufferUnsafe();
            }
        }

        /// <summary>
        /// Internal flush method (must be called within lock)
        /// </summary>
        private void FlushLogBufferUnsafe()
        {
            if (_logBuffer.Count == 0)
                return;

            try
            {
                // Check if log rotation is needed before writing
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > MAX_LOG_SIZE_BYTES)
                    {
                        RotateLogFiles();
                    }
                }

                // Write all buffered entries at once
                File.AppendAllLines(_logFilePath, _logBuffer);
                _logBuffer.Clear();
            }
            catch (Exception ex)
            {
                // Fallback to console if file write fails
                Console.WriteLine($"FileLoggingService: Failed to write log: {ex.Message}"); // Keep console for file logger write errors
                _logBuffer.Clear(); // Clear buffer to prevent memory leak
            }
        }

        /// <summary>
        /// Rotates log files when the current log gets too large
        /// </summary>
        private void RotateLogFiles()
        {
            try
            {
                // Rotate existing backup files
                for (int i = MAX_BACKUP_FILES - 1; i >= 1; i--)
                {
                    string oldFile = $"{_logFilePath}.{i}";
                    string newFile = $"{_logFilePath}.{i + 1}";
                    
                    if (File.Exists(oldFile))
                    {
                        if (File.Exists(newFile))
                            File.Delete(newFile);
                        File.Move(oldFile, newFile);
                    }
                }

                // Move current log to .1
                if (File.Exists(_logFilePath))
                {
                    string backupFile = $"{_logFilePath}.1";
                    if (File.Exists(backupFile))
                        File.Delete(backupFile);
                    File.Move(_logFilePath, backupFile);
                }

                // Add rotation entry to new log
                _logBuffer.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] === Log file rotated (previous file archived) ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FileLoggingService: Failed to rotate log files: {ex.Message}");
            }
        }

        public void LogRTSSDetection(int pid, bool detected, string details = "")
        {
            string status = detected ? "DETECTED" : "NOT_FOUND";
            string logMessage = $"RTSS Detection - PID {pid}: {status}";
            if (!string.IsNullOrEmpty(details))
                logMessage += $" - {details}";
            
            LogDebug(logMessage);
        }

        public void LogFPSUpdate(double fps, double frameTime, string source = "")
        {
            // Use new throttling system instead of manual timing
            string sourceInfo = string.IsNullOrEmpty(source) ? "" : $" [{source}]";
            LogDebug($"FPS Update{sourceInfo}: {fps:F1} FPS, {frameTime:F2}ms");
        }

        public void LogMonitoringState(string action, int pid = 0, string windowTitle = "")
        {
            string pidInfo = pid > 0 ? $" PID {pid}" : "";
            string titleInfo = !string.IsNullOrEmpty(windowTitle) ? $" - {windowTitle}" : "";
            LogInfo($"Monitoring {action}{pidInfo}{titleInfo}");
        }

        public void LogRTSSSharedMemory(string memoryType, bool success, string details = "")
        {
            string status = success ? "SUCCESS" : "FAILED";
            string logMessage = $"RTSS Shared Memory ({memoryType}): {status}";
            if (!string.IsNullOrEmpty(details))
                logMessage += $" - {details}";
            
            LogDebug(logMessage);
        }

        public void LogSystemInfo(string component, string info)
        {
            // Use new throttling system instead of manual timing
            LogInfo($"System {component}: {info}");
        }

        /// <summary>
        /// Logs graphics API detection events with detailed flag information
        /// </summary>
        public void LogAPIDetection(string processName, uint rawFlags, uint apiValue, string detectedAPI, string architecture = "")
        {
            string archInfo = string.IsNullOrEmpty(architecture) ? "" : $", Arch: {architecture}";
            LogInfo($"API Detection - {processName}: Raw: 0x{rawFlags:X8}, API: 0x{apiValue:X4} -> {detectedAPI}{archInfo}");
        }

        /// <summary>
        /// Logs sensor update events with throttling
        /// </summary>
        public void LogSensorUpdate(string sensorType, string value, string context = "")
        {
            string contextInfo = string.IsNullOrEmpty(context) ? "" : $" [{context}]";
            LogDebug($"Sensor Update{contextInfo} - {sensorType}: {value}");
        }

        /// <summary>
        /// Logs window detection events 
        /// </summary>
        public void LogWindowDetection(string action, string windowTitle, int pid = 0, bool isFullscreen = false)
        {
            string pidInfo = pid > 0 ? $" PID: {pid}" : "";
            string modeInfo = isFullscreen ? " [Fullscreen]" : " [Windowed]";
            LogDebug($"Window {action}{modeInfo}{pidInfo}: {windowTitle}");
        }

        /// <summary>
        /// Enhanced RTSS polling logs with more context in debug mode
        /// </summary>
        public void LogRTSSOperation(string operation, string details = "", bool success = true)
        {
            string status = success ? "SUCCESS" : "FAILED";
            string detailInfo = string.IsNullOrEmpty(details) ? "" : $" - {details}";
            LogDebug($"RTSS {operation}: {status}{detailInfo}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                AddLogEntry(LogLevel.Info, "=== InfoPanel.RTSS Debug Log Ended ===");
                
                // Force final flush and dispose timer
                FlushLogBuffer(null);
                _flushTimer?.Dispose();
                
                _disposed = true;
            }
        }
    }
}