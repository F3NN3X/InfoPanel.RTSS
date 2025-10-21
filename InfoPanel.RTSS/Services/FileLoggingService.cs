using System;
using System.IO;
using System.Threading;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Provides file-based logging for debugging user issues.
    /// Writes to debug.log in the plugin directory.
    /// </summary>
    public class FileLoggingService : IDisposable
    {
        private readonly string _logFilePath;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public FileLoggingService()
        {
            try
            {
                // Get the directory where the DLL is located
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? Environment.CurrentDirectory;
                _logFilePath = Path.Combine(pluginDirectory, "debug.log");

                // Initialize log file with session start
                WriteLogEntry("INFO", "=== InfoPanel.RTSS Debug Log Started ===");
                WriteLogEntry("INFO", $"Plugin Directory: {pluginDirectory}");
                WriteLogEntry("INFO", $"Assembly Location: {assemblyLocation}");
            }
            catch (Exception ex)
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"FileLoggingService: Failed to initialize file logging: {ex.Message}");
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

        private void WriteLogEntry(string level, string message)
        {
            if (string.IsNullOrEmpty(_logFilePath) || _disposed)
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
                Console.WriteLine($"FileLoggingService: Failed to write log: {ex.Message}");
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