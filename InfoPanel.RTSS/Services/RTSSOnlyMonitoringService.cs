using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Simplified RTSS-only monitoring service that continuously scans RTSS shared memory
    /// and only monitors processes that RTSS has successfully hooked.
    /// No complex fullscreen detection - let RTSS decide what's worth monitoring.
    /// </summary>
    public class RTSSOnlyMonitoringService : IDisposable
    {
        private readonly FileLoggingService? _fileLogger;
        private readonly ConfigurationService _configService;
        private readonly object _lock = new object();
        private bool _disposed = false;
        
        // Current monitoring state
        private int _currentMonitoredPid = 0;
        private string _currentWindowTitle;
        private double _currentFps = 0.0;
        private double _currentFrameTime = 0.0;
        private double _current1PercentLow = 0.0;
        
        // Frame time tracking for 1% low calculation
        private readonly Queue<double> _frameTimeBuffer = new Queue<double>();
        private const int FrameBufferSize = 100;
        
        // Loop counter for periodic operations
        private int _loopCounter = 0;

        // Events for sensor updates
        public event Action<double, double, double, string, int>? MetricsUpdated;

        public RTSSOnlyMonitoringService(ConfigurationService configService, FileLoggingService? fileLogger = null)
        {
            _configService = configService;
            _fileLogger = fileLogger;
            _currentWindowTitle = _configService.DefaultCaptureMessage;
            _fileLogger?.LogInfo("RTSSOnlyMonitoringService initialized - RTSS-first approach");
        }

        /// <summary>
        /// Starts the continuous RTSS monitoring loop
        /// </summary>
        public Task StartMonitoringAsync(CancellationToken cancellationToken)
        {
            _fileLogger?.LogInfo("Starting continuous RTSS-only monitoring");
            
            // Force initial sensor clearing to ensure UI shows clean state
            _fileLogger?.LogInfo("Forcing initial sensor clear to ensure clean state");
            MetricsUpdated?.Invoke(0.0, 0.0, 0.0, _configService.DefaultCaptureMessage, 0);
            
            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        await MonitorRTSSAsync(cancellationToken).ConfigureAwait(false);
                        
                        // Increment loop counter for periodic operations
                        _loopCounter++;
                        
                        // Log every 5 seconds (5000ms / 16ms = ~312 loops) to show we're active
                        if (_loopCounter % 312 == 0)
                        {
                            _fileLogger?.LogDebug("RTSS monitoring loop active - scanning shared memory");
                        }
                        
                        // Check every 16ms (~60Hz polling rate)
                        await Task.Delay(16, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _fileLogger?.LogError($"Error in RTSS monitoring loop: {ex.Message}");
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                    }
                }
                
                _fileLogger?.LogInfo("RTSS-only monitoring stopped");
            }, cancellationToken);
        }

        /// <summary>
        /// Continuously monitors RTSS shared memory for hooked processes
        /// </summary>
        private async Task MonitorRTSSAsync(CancellationToken cancellationToken)
        {
            var hookedProcess = await FindRTSSHookedProcessAsync().ConfigureAwait(false);
            
            if (hookedProcess != null)
            {
                lock (_lock)
                {
                    // Check if we need to switch to a different process
                    if (_currentMonitoredPid != hookedProcess.Value.pid)
                    {
                        _fileLogger?.LogInfo($"RTSS hook detected: switching from PID {_currentMonitoredPid} to PID {hookedProcess.Value.pid}");
                        _currentMonitoredPid = hookedProcess.Value.pid;
                        
                        // Get window title for the new process
                        _currentWindowTitle = GetWindowTitleForPid(hookedProcess.Value.pid);
                        
                        _fileLogger?.LogInfo($"Now monitoring: PID {_currentMonitoredPid} - '{_currentWindowTitle}'");
                    }
                    
                    // Update FPS metrics
                    _currentFps = hookedProcess.Value.fps;
                    _currentFrameTime = _currentFps > 0 ? 1000.0 / _currentFps : 0.0;
                    
                    // Update 1% low calculation
                    UpdateFrameTimeBuffer(_currentFrameTime);
                    _current1PercentLow = Calculate1PercentLow();
                    
                    // Fire metrics update event
                    MetricsUpdated?.Invoke(_currentFps, _currentFrameTime, _current1PercentLow, _currentWindowTitle, _currentMonitoredPid);
                    
                    _fileLogger?.LogDebug($"FPS Update: {_currentFps:F1} FPS, {_currentFrameTime:F2}ms, 1%Low: {_current1PercentLow:F1}");
                }
            }
            else
            {
                // No valid RTSS hooks found - clear monitoring state
                lock (_lock)
                {
                    bool hadData = _currentMonitoredPid > 0 || _currentFps > 0 || !string.Equals(_currentWindowTitle, _configService.DefaultCaptureMessage, StringComparison.Ordinal);
                    
                    if (hadData)
                    {
                        _fileLogger?.LogInfo("No valid RTSS hooks found, clearing monitoring and updating sensors");
                        
                        // Clear the state when no valid FPS data found
                        _currentMonitoredPid = 0;
                        _currentWindowTitle = _configService.DefaultCaptureMessage;
                        _currentFps = 0.0;
                        _currentFrameTime = 0.0;
                        _current1PercentLow = 0.0;
                        _frameTimeBuffer.Clear();
                        
                        // Fire event only when state actually changes
                        MetricsUpdated?.Invoke(0.0, 0.0, 0.0, _configService.DefaultCaptureMessage, 0);
                        _fileLogger?.LogInfo($"Metrics cleared - FPS: 0.0, 1% Low: 0.0, Title: {_configService.DefaultCaptureMessage}");
                    }
                    else
                    {
                        // State is already cleared, but still fire periodic clear events to ensure UI consistency
                        // This helps with InfoPanel UI caching issues
                        if (_loopCounter % 250 == 0) // Every ~4 seconds at 16ms intervals
                        {
                            _fileLogger?.LogDebug("Sending periodic sensor clear to ensure UI consistency");
                            MetricsUpdated?.Invoke(0.0, 0.0, 0.0, _configService.DefaultCaptureMessage, 0);
                        }
                        
                        _fileLogger?.LogDebug("No RTSS shared memory found or no hooked processes");
                    }
                }
            }
        }

        /// <summary>
        /// Scans RTSS shared memory for any currently hooked processes
        /// </summary>
        private async Task<(int pid, double fps)?> FindRTSSHookedProcessAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try RTSS shared memory V2 only
                    _fileLogger?.LogDebug("Scanning for RTSS shared memory...");
                    
                    var result = TryReadRTSSSharedMemory("RTSSSharedMemoryV2");
                    if (result.HasValue) 
                    {
                        _fileLogger?.LogInfo($"Found RTSS data: PID {result.Value.pid}, FPS {result.Value.fps:F1}");
                        return result;
                    }
                    
                    _fileLogger?.LogDebug("No RTSS shared memory found or no hooked processes");
                    return null;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogDebug($"Error scanning RTSS shared memory: {ex.Message}");
                    return null;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to read RTSS shared memory for a specific version
        /// </summary>
        private (int pid, double fps)? TryReadRTSSSharedMemory(string memoryName)
        {
            try
            {
                _fileLogger?.LogDebug($"Attempting to open RTSS shared memory: {memoryName}");
                var fileMapping = Kernel32.OpenFileMapping(Kernel32.FILE_MAP.FILE_MAP_READ, false, memoryName);
                if (fileMapping.IsInvalid) 
                {
                    _fileLogger?.LogDebug($"Failed to open {memoryName} - shared memory not found");
                    return null;
                }

                var mapView = Kernel32.MapViewOfFile(fileMapping, Kernel32.FILE_MAP.FILE_MAP_READ, 0, 0, IntPtr.Zero);
                if (mapView == IntPtr.Zero)
                {
                    fileMapping.Dispose();
                    return null;
                }

                try
                {
                    // Read RTSS header
                    var signature = Marshal.ReadInt32(mapView, 0);
                    if (signature != 0x52545353) // "RTSS" in little-endian format
                    {
                        _fileLogger?.LogDebug($"{memoryName} has invalid signature: 0x{signature:X8}, expected 0x52545353 (RTSS)");
                        return null;
                    }

                    var version = Marshal.ReadInt32(mapView, 4);
                    var appEntrySize = Marshal.ReadInt32(mapView, 8);  // dwAppEntrySize
                    var appArrOffset = Marshal.ReadInt32(mapView, 12); // dwAppArrOffset
                    var appArrSize = Marshal.ReadInt32(mapView, 16);   // dwAppArrSize
                    
                    _fileLogger?.LogDebug($"{memoryName} opened successfully - Version: 0x{version:X8}, AppEntrySize: {appEntrySize}, AppArrOffset: {appArrOffset}, AppArrSize: {appArrSize}");

                    // Scan through app entries to find hooked processes
                    for (int i = 0; i < appArrSize; i++)
                    {
                        int entryOffset = appArrOffset + (i * appEntrySize);
                        
                        var processId = Marshal.ReadInt32(mapView, entryOffset + 0); // dwProcessID
                        if (processId <= 0) continue;
                        
                        _fileLogger?.LogDebug($"Found RTSS entry {i}: PID {processId}");
                        
                        // Check if process still exists
                        if (!IsProcessRunning(processId)) 
                        {
                            _fileLogger?.LogDebug($"PID {processId} is not running, skipping");
                            continue;
                        }
                        
                        // Read timing data for proper FPS calculation
                        var time0 = Marshal.ReadInt32(mapView, entryOffset + 268); // dwTime0
                        var time1 = Marshal.ReadInt32(mapView, entryOffset + 272); // dwTime1
                        var frames = Marshal.ReadInt32(mapView, entryOffset + 276); // dwFrames
                        
                        _fileLogger?.LogDebug($"PID {processId} - Time0: {time0}, Time1: {time1}, Frames: {frames}");
                        
                        // Validate timing data
                        if (time0 <= 0 || time1 <= 0 || frames <= 0 || time1 <= time0) continue;
                        
                        // Calculate FPS using RTSS formula: 1000.0 * dwFrames / (dwTime1 - dwTime0)
                        double fps = 1000.0 * frames / (time1 - time0);
                        if (fps > 0 && fps < 1000) // Sanity check
                        {
                            _fileLogger?.LogInfo($"RTSS hook found: PID {processId}, FPS {fps:F1}");
                            return (processId, fps);
                        }
                        else
                        {
                            _fileLogger?.LogDebug($"PID {processId} invalid FPS calculated: {fps:F1}");
                        }
                    }

                    return null;
                }
                finally
                {
                    Kernel32.UnmapViewOfFile(mapView);
                    fileMapping.Dispose();
                }
            }
            catch (Exception ex)
            {
                _fileLogger?.LogDebug($"Error reading {memoryName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the window title for a specific process ID
        /// </summary>
        private string GetWindowTitleForPid(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                
                // Try to get main window title
                if (!string.IsNullOrEmpty(process.MainWindowTitle))
                {
                    return process.MainWindowTitle;
                }
                
                // Fallback to process name
                return process.ProcessName;
            }
            catch (Exception ex)
            {
                _fileLogger?.LogDebug($"Error getting window title for PID {pid}: {ex.Message}");
                return "Unknown Process";
            }
        }

        /// <summary>
        /// Checks if a process is still running
        /// </summary>
        private bool IsProcessRunning(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates the frame time buffer for 1% low calculation
        /// </summary>
        private void UpdateFrameTimeBuffer(double frameTime)
        {
            if (frameTime <= 0) return;
            
            _frameTimeBuffer.Enqueue(frameTime);
            
            // Keep buffer at fixed size
            while (_frameTimeBuffer.Count > FrameBufferSize)
            {
                _frameTimeBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Calculates 1% low FPS from frame time buffer
        /// </summary>
        private double Calculate1PercentLow()
        {
            if (_frameTimeBuffer.Count < 10) return 0.0;
            
            var frameTimes = new List<double>(_frameTimeBuffer);
            frameTimes.Sort();
            
            // Get 99th percentile (1% low means worst 1% of frames)
            int index = (int)(frameTimes.Count * 0.99);
            if (index >= frameTimes.Count) index = frameTimes.Count - 1;
            
            double worstFrameTime = frameTimes[index];
            return worstFrameTime > 0 ? 1000.0 / worstFrameTime : 0.0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _fileLogger?.LogInfo("RTSSOnlyMonitoringService disposed");
                _disposed = true;
            }
        }
    }
}