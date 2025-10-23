using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Provides file-based logging for debugging user issues.
    /// Writes to debug.log in the plugin directory when debug mode is enabled.
    /// Includes message throttling and compression to keep log files manageable.
    /// </summary>
    public class FileLoggingService : IDisposable
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();
        private readonly ConfigurationService _configService;
        private bool _disposed = false;

        // Message throttling to reduce repetitive logs
        private readonly Dictionary<string, (DateTime lastLogged, int count)> _messageThrottle = new();
        private readonly TimeSpan _throttleInterval = TimeSpan.FromSeconds(5); // Log similar messages max once per 5 seconds
        private DateTime _lastRTSSPollLog = DateTime.MinValue;
        private int _rtssPollingCount = 0;

        public FileLoggingService(ConfigurationService configService)
        {
            _configService = configService;
            try
            {
                // Get the directory where the DLL is located
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? Environment.CurrentDirectory;
                _logFilePath = Path.Combine(pluginDirectory, "debug.log");

                // Only initialize log file if debug mode is enabled
                if (_configService.IsDebugEnabled)
                {
                    WriteLogEntry("INFO", "=== InfoPanel.RTSS Debug Log Started ===");
                    WriteLogEntry("INFO", $"Plugin Directory: {pluginDirectory}");
                    WriteLogEntry("INFO", $"Assembly Location: {assemblyLocation}");
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"FileLoggingService: Failed to initialize file logging: {ex.Message}"); // Keep console for file logger initialization errors
                _logFilePath = string.Empty;
            }
        }

        public void LogInfo(string message)
        {
            WriteLogEntry("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLogEntry("WARN", message);
        }

        public void LogError(string message)
        {
            WriteLogEntry("ERROR", message);
        }

        public void LogError(string message, Exception ex)
        {
            WriteLogEntry("ERROR", $"{message}: {ex.Message}");
            WriteLogEntry("ERROR", $"Stack Trace: {ex.StackTrace}");
        }

        public void LogDebug(string message)
        {
            WriteLogEntry("DEBUG", message);
        }

        /// <summary>
        /// Logs debug messages with throttling to prevent log spam from repetitive messages.
        /// </summary>
        public void LogDebugThrottled(string message, string? throttleKey = null)
        {
            throttleKey ??= message.Substring(0, Math.Min(50, message.Length)); // Use first 50 chars as key
            
            if (ShouldLogThrottledMessage(throttleKey))
            {
                WriteLogEntry("DEBUG", message);
            }
        }

        /// <summary>
        /// Logs RTSS polling activity with heavy throttling since it happens every 16ms.
        /// </summary>
        public void LogRTSSPolling(string message)
        {
            _rtssPollingCount++;
            var now = DateTime.Now;
            
            // Log every 5 seconds with count summary
            if (now - _lastRTSSPollLog >= TimeSpan.FromSeconds(5))
            {
                WriteLogEntry("DEBUG", $"RTSS Polling (x{_rtssPollingCount} in 5s): {message}");
                _lastRTSSPollLog = now;
                _rtssPollingCount = 0;
            }
        }

        private bool ShouldLogThrottledMessage(string key)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                
                if (_messageThrottle.TryGetValue(key, out var existing))
                {
                    _messageThrottle[key] = (existing.lastLogged, existing.count + 1);
                    
                    // Only log if enough time has passed
                    if (now - existing.lastLogged >= _throttleInterval)
                    {
                        // Include count if message was suppressed
                        if (existing.count > 1)
                        {
                            // This will be handled by the caller
                        }
                        _messageThrottle[key] = (now, 0);
                        return true;
                    }
                    return false;
                }
                else
                {
                    _messageThrottle[key] = (now, 0);
                    return true;
                }
            }
        }

        private void WriteLogEntry(string level, string message)
        {
            // Only log to file if debug mode is enabled
            if (!_configService.IsDebugEnabled || string.IsNullOrEmpty(_logFilePath) || _disposed)
                return;

            try
            {
                lock (_lock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logLine = $"[{timestamp}] [{level}] {message}";
                    
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if file write fails
                Console.WriteLine($"FileLoggingService: Failed to write log: {ex.Message}"); // Keep console for file logger write errors
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
            LogInfo($"System {component}: {info}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                WriteLogEntry("INFO", "=== InfoPanel.RTSS Debug Log Ended ===");
                _disposed = true;
            }
        }
    }
}