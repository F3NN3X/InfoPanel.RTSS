using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using InfoPanel.RTSS.Models;
using InfoPanel.RTSS.Analysis;
using InfoPanel.RTSS.Statistics;

namespace InfoPanel.RTSS.Services
{


    /// <summary>
    /// RTSS monitoring service that continuously scans RTSS shared memory
    /// and monitors processes that RTSS has successfully hooked.
    /// Provides comprehensive FPS monitoring with session-wide statistical accuracy.
    /// </summary>
    public class RTSSMonitoringService : IDisposable
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
        
        // Focus state tracking for smart filtering
        private bool _lastKnownFocusState = true;
        private DateTime _lastFocusChange = DateTime.UtcNow;
        private int _focusChangeCount = 0;
        
        // Frame time tracking for 1% low calculation
        private readonly Queue<double> _frameTimeBuffer = new Queue<double>();
        private const int FrameBufferSize = 100;
        
        // Time-based frame buffer for industry-standard 1% low calculation
        private readonly Queue<TimedFrameData> _timedFrameBuffer = new();
        private readonly object _frameBufferLock = new object();
        private static readonly TimeSpan MinBufferDuration = TimeSpan.FromSeconds(60);
        private int _onePercentLowCalculationCount = 0;
        
        // Session-wide statistics for long gaming sessions
        private readonly SessionStatistics _sessionStats = new();
        private readonly object _sessionStatsLock = new object();
        
        // Loop counter for periodic operations
        private int _loopCounter = 0;
        private DateTime _lastDebugLog = DateTime.MinValue;
        
        // Periodic status logging
        private DateTime _lastStatusLog = DateTime.MinValue;
        private const int STATUS_LOG_INTERVAL_MS = 1000; // Log status every second

        // Events for sensor updates
        public event Action<double, double, double, string, int>? MetricsUpdated;
        public event Action<RTSSCandidate>? EnhancedMetricsUpdated;

        public RTSSMonitoringService(ConfigurationService configService, FileLoggingService? fileLogger = null)
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
                        
                        // Debug logging every 500ms instead of every 16ms loop
                        var now = DateTime.Now;
                        if (now - _lastDebugLog >= TimeSpan.FromMilliseconds(500))
                        {
                            _lastDebugLog = now;
                            _fileLogger?.LogRTSSPolling("RTSS monitoring loop active - scanning shared memory");
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
                    if (_currentMonitoredPid != hookedProcess.ProcessId)
                    {
                        _fileLogger?.LogInfo($"RTSS hook detected: switching from PID {_currentMonitoredPid} to PID {hookedProcess.ProcessId}");
                        _currentMonitoredPid = hookedProcess.ProcessId;
                        
                        // Reset session statistics for new game/process
                        lock (_sessionStatsLock)
                        {
                            _sessionStats.Reset();
                            _fileLogger?.LogInfo("Session statistics reset for new process");
                        }
                        
                        // Get window title for the new process
                        _currentWindowTitle = GetWindowTitleForPid(hookedProcess.ProcessId);
                        
                        _fileLogger?.LogInfo($"Now monitoring: PID {_currentMonitoredPid} - '{_currentWindowTitle}'");
                    }
                    
                    // Update FPS metrics
                    _currentFps = hookedProcess.Fps;
                    _currentFrameTime = _currentFps > 0 ? 1000.0 / _currentFps : 0.0;
                    
                    // Track focus state for smart filtering
                    bool currentFocusState = IsProcessForeground(_currentMonitoredPid);
                    bool focusStateChanged = currentFocusState != _lastKnownFocusState;
                    
                    if (focusStateChanged)
                    {
                        _lastFocusChange = DateTime.UtcNow;
                        _focusChangeCount++;
                        _fileLogger?.LogInfo($"[FOCUS CHANGE] Process {_currentMonitoredPid} focus: {_lastKnownFocusState} -> {currentFocusState} (change #{_focusChangeCount})");
                        _lastKnownFocusState = currentFocusState;
                        
                        // Optional aggressive recovery: clear unfocused frames when focus is regained
                        if (currentFocusState && _configService.EnableFocusFiltering && _configService.AggressiveRecovery)
                        {
                            ClearUnfocusedFramesFromBuffer();
                            _fileLogger?.LogInfo("[AGGRESSIVE RECOVERY] Cleared unfocused frames from buffer after focus regained");
                        }
                    }
                    
                    // Update 1% low calculation using enhanced focus-aware approach
                    UpdateFrameTimeBufferWithFocus(_currentFrameTime, currentFocusState);
                    _current1PercentLow = CalculateEnhanced1PercentLow();
                    
                    // Update enhanced RTSSCandidate with calculated metrics
                    hookedProcess.OnePercentLowFps = _current1PercentLow;
                    hookedProcess.FrameTimeMs = _currentFrameTime;
                    
                    // Fire both events - legacy for backward compatibility and enhanced for new features
                    MetricsUpdated?.Invoke(_currentFps, _currentFrameTime, _current1PercentLow, _currentWindowTitle, _currentMonitoredPid);
                    EnhancedMetricsUpdated?.Invoke(hookedProcess);
                    
                    // Periodic status logging instead of per-update logging
                    var now = DateTime.Now;
                    if ((now - _lastStatusLog).TotalMilliseconds >= STATUS_LOG_INTERVAL_MS)
                    {
                        _fileLogger?.LogPeriodicStatus(_currentWindowTitle, _currentFps, hookedProcess.GraphicsAPI, true, hookedProcess.Architecture);
                        _lastStatusLog = now;
                    }
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
                        
                        // Clear time-based buffer as well
                        lock (_frameBufferLock)
                        {
                            _timedFrameBuffer.Clear();
                        }
                        
                        // Reset session statistics when no valid hooks
                        lock (_sessionStatsLock)
                        {
                            _sessionStats.Reset();
                        }
                        
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
                            _fileLogger?.LogDebugThrottled("Sending periodic sensor clear to ensure UI consistency", "periodic_clear");
                            MetricsUpdated?.Invoke(0.0, 0.0, 0.0, _configService.DefaultCaptureMessage, 0);
                        }
                        
                        _fileLogger?.LogDebugThrottled("No RTSS shared memory found or no hooked processes", "no_rtss_processes");
                    }
                    
                    // Periodic status logging for idle state (outside the hadData check but inside the lock)
                    var now = DateTime.Now;
                    if ((now - _lastStatusLog).TotalMilliseconds >= STATUS_LOG_INTERVAL_MS)
                    {
                        _fileLogger?.LogPeriodicStatus(_configService.DefaultCaptureMessage, 0.0, "None", false);
                        _lastStatusLog = now;
                    }
                }
            }
        }

        /// <summary>
        /// Scans RTSS shared memory for any currently hooked processes with enhanced metrics
        /// </summary>
        private async Task<RTSSCandidate?> FindRTSSHookedProcessAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try RTSS shared memory V2 only - use throttled logging since this runs every 16ms
                    _fileLogger?.LogRTSSOperation("Scanning", "Checking shared memory for hooked processes");
                    
                    var result = TryReadRTSSSharedMemory("RTSSSharedMemoryV2");
                    if (result != null) 
                    {
                        _fileLogger?.LogDebug($"Found RTSS data: PID {result.ProcessId}, FPS {result.Fps:F1}");
                        return result;
                    }
                    
                    _fileLogger?.LogDebugThrottled("No RTSS shared memory found or no hooked processes", "no_rtss_data");
                    return null;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogRTSSOperation("Scanning", $"Error: {ex.Message}", false);
                    return null;
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Enhanced RTSS shared memory reader that extracts comprehensive gaming metrics
        /// Reads 50+ RTSS fields including graphics API, resolution, frame timing, and performance data
        /// </summary>
        private RTSSCandidate? TryReadRTSSSharedMemory(string memoryName)
        {
            try
            {
                _fileLogger?.LogDebugThrottled($"Attempting to open RTSS shared memory: {memoryName}", "rtss_open_attempt");
                var fileMapping = Kernel32.OpenFileMapping(Kernel32.FILE_MAP.FILE_MAP_READ, false, memoryName);
                if (fileMapping.IsInvalid) 
                {
                    _fileLogger?.LogDebugThrottled($"Failed to open {memoryName} - shared memory not found", "rtss_not_found");
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
                        _fileLogger?.LogDebugThrottled($"{memoryName} has invalid signature: 0x{signature:X8}, expected 0x52545353 (RTSS)", "invalid_signature");
                        return null;
                    }

                    var version = Marshal.ReadInt32(mapView, 4);
                    var appEntrySize = Marshal.ReadInt32(mapView, 8);  // dwAppEntrySize
                    var appArrOffset = Marshal.ReadInt32(mapView, 12); // dwAppArrOffset
                    var appArrSize = Marshal.ReadInt32(mapView, 16);   // dwAppArrSize
                    
                    _fileLogger?.LogDebugThrottled($"{memoryName} opened successfully - Version: 0x{version:X8}, AppEntrySize: {appEntrySize}, AppArrOffset: {appArrOffset}, AppArrSize: {appArrSize}", "rtss_opened");

                    // Collect all valid RTSS candidates for smart prioritization
                    var candidates = new List<RTSSCandidate>();
                    
                    // Scan through app entries to find hooked processes
                    int validEntries = 0;
                    for (int i = 0; i < appArrSize; i++)
                    {
                        int entryOffset = appArrOffset + (i * appEntrySize);
                        
                        var processId = Marshal.ReadInt32(mapView, entryOffset + 0); // dwProcessID
                        if (processId <= 0) continue;
                        
                        validEntries++;
                        
                        // Check if process still exists
                        if (!IsProcessRunning(processId)) 
                        {
                            _fileLogger?.LogDebugThrottled($"PID {processId} is not running, skipping", $"pid_not_running_{processId}");
                            continue;
                        }

                        // Apply ignored processes filter early
                        var processName = GetSafeProcessName(processId);
                        var ignoredProcesses = _configService.IgnoredProcesses
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim().ToLowerInvariant())
                            .ToHashSet();
                        
                        if (ignoredProcesses.Contains(processName.ToLowerInvariant()))
                        {
                            _fileLogger?.LogDebugThrottled($"PID {processId} ({processName}) is in ignored list - skipping", $"ignored_{processId}");
                            continue;
                        }
                        
                        // Read timing data for proper FPS calculation
                        var time0 = Marshal.ReadInt32(mapView, entryOffset + 268); // dwTime0
                        var time1 = Marshal.ReadInt32(mapView, entryOffset + 272); // dwTime1
                        var frames = Marshal.ReadInt32(mapView, entryOffset + 276); // dwFrames
                        
                        // Apply configuration-based FPS filtering early
                        if (time0 > 0 && time1 > 0 && frames > 0 && time1 > time0)
                        {
                            double preliminaryFps = 1000.0 * frames / (time1 - time0);
                            if (preliminaryFps < _configService.MinimumFpsThreshold)
                            {
                                _fileLogger?.LogDebugThrottled($"PID {processId} has FPS ({preliminaryFps:F1}) below threshold ({_configService.MinimumFpsThreshold}) - skipping", $"low_fps_{processId}");
                                continue;
                            }
                        }
                        
                        // Only log if there's actual FPS data, otherwise just count them
                        if (time0 > 0 && time1 > 0 && frames > 0)
                        {
                            _fileLogger?.LogDebugThrottled($"Active RTSS entry {i}: PID {processId} - Time0: {time0}, Time1: {time1}, Frames: {frames}", $"active_entry_{processId}");
                        }
                        else
                        {
                            _fileLogger?.LogDebugThrottled($"Found inactive RTSS entry: PID {processId} (no FPS data)", "inactive_entries");
                        }
                        
                        // Validate timing data
                        if (time0 <= 0 || time1 <= 0 || frames <= 0 || time1 <= time0) continue;
                        
                        // Calculate FPS using RTSS formula: 1000.0 * dwFrames / (dwTime1 - dwTime0)
                        double fps = 1000.0 * frames / (time1 - time0);
                        if (fps > 0 && fps < 1000) // Sanity check
                        {
                            // Read enhanced RTSS shared memory fields using documented offsets
                            var rtssFlags = (uint)Marshal.ReadInt32(mapView, entryOffset + 264);    // dwFlags (offset 264)
                            var frameTimeUs = Marshal.ReadInt32(mapView, entryOffset + 280);        // dwFrameTime in microseconds (offset 280)
                            
                            // RTSS native percentile calculations (v2.13+ offsets - approximate)
                            var stat1PercentLow = Marshal.ReadInt32(mapView, entryOffset + 544);    // dwStatFramerate1Dot0PercentLow (millihertz)
                            var stat0Point1PercentLow = Marshal.ReadInt32(mapView, entryOffset + 548); // dwStatFramerate0Dot1PercentLow (millihertz)
                            
                            // RESOLUTION DATA: Removed due to inconsistent behavior between display modes
                            // Borderless fullscreen shows display resolution instead of game render resolution
                            // This was confusing for users, so resolution detection has been removed
                            
                            // GPU frame timing: Also using estimated offset, may not be reliable
                            // Using documented offset 679 from RTSS documentation instead
                            int gpuFrameTimeUs = 0;
                            try
                            {
                                // Try reading GPU frame time from documented offset (if available)
                                gpuFrameTimeUs = Marshal.ReadInt32(mapView, entryOffset + 679); // dwGpuFrameTime (documented v2.21+)
                            }
                            catch
                            {
                                // GPU frame time not available in this RTSS version
                                gpuFrameTimeUs = 0;
                            }
                            
                            // Calculate frame statistics with proper conversions
                            double frameTimeMs = fps > 0 ? 1000.0 / fps : 0.0;
                            double gpuFrameTimeMs = gpuFrameTimeUs > 0 ? gpuFrameTimeUs / 1000.0 : 0.0;
                            
                            // Convert RTSS native percentile calculations (millihertz to FPS)
                            double onePercentLowNative = stat1PercentLow > 0 ? stat1PercentLow / 1000.0 : 0.0;
                            double zeroPointOnePercentLow = stat0Point1PercentLow > 0 ? stat0Point1PercentLow / 1000.0 : 0.0;
                            
                            // Create enhanced candidate with comprehensive RTSS data
                            var candidate = new RTSSCandidate 
                            { 
                                ProcessId = processId, 
                                Fps = fps,
                                FrameTimeMs = frameTimeMs,
                                GpuFrameTimeMs = gpuFrameTimeMs,
                                
                                OnePercentLowFpsNative = onePercentLowNative,
                                ZeroPointOnePercentLowFps = zeroPointOnePercentLow,
                                
                                IsFullscreen = _configService.PreferFullscreen && IsProcessFullscreen(processId),
                                IsForeground = IsProcessForeground(processId),
                                ProcessName = processName,
                                RTSSFlags = rtssFlags,
                                RTSSEngineVersion = 0, // TODO: Read actual engine version when offset is confirmed
                                // Resolution detection removed - was inconsistent between display modes
                                RefreshRate = 0.0, // TODO: Read refresh rate when offset is confirmed
                                LastUpdate = DateTime.Now
                            };
                            
                            candidates.Add(candidate);
                            _fileLogger?.LogDebugThrottled($"RTSS candidate: PID {processId} ({candidate.ProcessName}) - FPS: {fps:F1}, Fullscreen: {candidate.IsFullscreen}, Foreground: {candidate.IsForeground}", $"candidate_{processId}");
                        }
                        else
                        {
                            _fileLogger?.LogDebugThrottled($"PID {processId} invalid FPS calculated: {fps:F1}", $"invalid_fps_{processId}");
                        }
                    }

                    // Now select the best candidate using smart prioritization
                    var bestCandidate = SelectBestGamingCandidate(candidates);
                    if (bestCandidate != null)
                    {
                        // Populate enhanced metrics using specialized analyzers with validation
                        bestCandidate.GraphicsAPI = GraphicsAPIDetector.GetGraphicsAPI(bestCandidate.RTSSFlags);
                        bestCandidate.Architecture = GraphicsAPIDetector.GetArchitecture(bestCandidate.GraphicsAPI, bestCandidate.RTSSFlags);
                        bestCandidate.GameCategory = GameCategorizer.GetGameCategory(bestCandidate.ProcessName, bestCandidate.GraphicsAPI, _configService);
                        
                        // Enhanced logging for API detection
                        uint apiValue = bestCandidate.RTSSFlags & 0x0000FFFF; // APPFLAG_API_USAGE_MASK
                        _fileLogger?.LogAPIDetection(bestCandidate.ProcessName, bestCandidate.RTSSFlags, apiValue, bestCandidate.GraphicsAPI, bestCandidate.Architecture);
                        bestCandidate.FrameTimeMs = bestCandidate.Fps > 0 ? 1000.0 / bestCandidate.Fps : 0.0;
                        bestCandidate.WindowTitle = GetWindowTitleForPid(bestCandidate.ProcessId);
                        
                        // Enhanced analysis with specialized detectors
                        bestCandidate.VSync = GraphicsAPIDetector.GetVSyncStatus(bestCandidate.RTSSFlags, bestCandidate.Fps, bestCandidate.RefreshRate);
                        bestCandidate.DisplayMode = WindowModeDetector.GetEnhancedDisplayMode(bestCandidate.ProcessId, bestCandidate.IsFullscreen, bestCandidate.RefreshRate, _fileLogger);
                        
                        // Resolution detection removed - was inconsistent and confusing for users
                        // Borderless fullscreen mode reported display resolution instead of game render resolution
                        
                        // FPS statistics now properly handled during RTSS data reading
                        // Statistics are set to current FPS when RTSS recording is not active
                        
                        _fileLogger?.LogDebug($"Selected enhanced gaming candidate: PID {bestCandidate.ProcessId} ({bestCandidate.ProcessName}) - FPS: {bestCandidate.Fps:F1}, API: {bestCandidate.GraphicsAPI}");
                        return bestCandidate;
                    }

                    // Summary log instead of individual entry logs
                    if (validEntries > 0)
                    {
                        _fileLogger?.LogDebugThrottled($"Scanned {validEntries} RTSS entries, no suitable gaming candidates found", "rtss_scan_summary");
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
                _fileLogger?.LogDebugThrottled($"Error reading {memoryName}: {ex.Message}", "rtss_read_error");
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
                _fileLogger?.LogDebugThrottled($"Error getting window title for PID {pid}: {ex.Message}", $"window_title_error_{pid}");
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
            
            // Update legacy frame buffer (for compatibility)
            _frameTimeBuffer.Enqueue(frameTime);
            while (_frameTimeBuffer.Count > FrameBufferSize)
            {
                _frameTimeBuffer.Dequeue();
            }
            
            // Update time-based buffer for enhanced 1% low calculation
            AddFrameDataToBuffer(frameTime);
            
            // Update session statistics for long-term tracking
            UpdateSessionStatistics(frameTime);
        }

        /// <summary>
        /// Focus-aware frame time buffer update with smart filtering capabilities.
        /// Implements configurable focus filtering to prevent 1% low corruption from alt-tab/overlay events.
        /// </summary>
        private void UpdateFrameTimeBufferWithFocus(double frameTime, bool isFocused)
        {
            if (frameTime <= 0) return;
            
            // Check focus filtering configuration
            bool shouldExcludeUnfocused = _configService.EnableFocusFiltering && _configService.ExcludeUnfocusedFrames;
            
            if (!isFocused && shouldExcludeUnfocused)
            {
                // Log unfocused frame exclusion (throttled to avoid spam)
                _fileLogger?.LogDebugThrottled($"Excluding unfocused frame time: {frameTime:F2}ms (FPS: {(1000.0/frameTime):F1})", "unfocused_frame_excluded");
                
                // Still update session stats to track all frames, but skip buffer updates
                UpdateSessionStatistics(frameTime);
                return;
            }
            
            // Always update legacy frame buffer for backward compatibility
            _frameTimeBuffer.Enqueue(frameTime);
            while (_frameTimeBuffer.Count > FrameBufferSize)
            {
                _frameTimeBuffer.Dequeue();
            }
            
            // Add to time-based buffer with focus state metadata
            AddFrameDataToBufferWithFocus(frameTime, isFocused);
            
            // Update session statistics
            UpdateSessionStatistics(frameTime);
            
            // Debug logging for focus-aware filtering
            if (_configService.EnableFocusFiltering)
            {
                _fileLogger?.LogDebugThrottled($"Frame added to buffer: {frameTime:F2}ms, Focused: {isFocused}, Buffer count: {_timedFrameBuffer.Count}", "focus_aware_update");
            }
        }

        /// <summary>
        /// Clears unfocused frames from the time-based buffer for aggressive recovery.
        /// Used when focus is regained and aggressive recovery is enabled.
        /// </summary>
        private void ClearUnfocusedFramesFromBuffer()
        {
            lock (_frameBufferLock)
            {
                int originalCount = _timedFrameBuffer.Count;
                var focusedFrames = _timedFrameBuffer.Where(f => f.WasFocused).ToArray();
                
                _timedFrameBuffer.Clear();
                foreach (var frame in focusedFrames)
                {
                    _timedFrameBuffer.Enqueue(frame);
                }
                
                int removedCount = originalCount - _timedFrameBuffer.Count;
                _fileLogger?.LogInfo($"Aggressive recovery: Removed {removedCount} unfocused frames, kept {_timedFrameBuffer.Count} focused frames");
            }
        }

        /// <summary>
        /// Add frame data to time-based buffer with automatic cleanup of old data
        /// Follows CapFrameX methodology for time-weighted 1% low calculation
        /// </summary>
        private void AddFrameDataToBuffer(double frameTimeMs)
        {
            // Default to focused state for backward compatibility
            AddFrameDataToBufferWithFocus(frameTimeMs, true);
        }

        /// <summary>
        /// Add frame data to time-based buffer with focus state metadata.
        /// Enables smart filtering for focus-aware 1% low calculations.
        /// </summary>
        private void AddFrameDataToBufferWithFocus(double frameTimeMs, bool wasFocused)
        {
            var now = DateTime.UtcNow;
            var frameData = new TimedFrameData(frameTimeMs, now, wasFocused);
            
            lock (_frameBufferLock)
            {
                _timedFrameBuffer.Enqueue(frameData);
                
                // Remove frames older than 60 seconds
                var cutoffTime = now.Subtract(MinBufferDuration);
                while (_timedFrameBuffer.Count > 0 && _timedFrameBuffer.Peek().Timestamp < cutoffTime)
                {
                    _timedFrameBuffer.Dequeue();
                }
                
                // Also remove excess frames to prevent memory growth (keep max 10,000 frames)
                while (_timedFrameBuffer.Count > 10000)
                {
                    _timedFrameBuffer.Dequeue();
                }
            }
        }

        /// <summary>
        /// Update session-wide statistics for memory-efficient long-term tracking.
        /// Maintains worst frame times without storing all frames using statistical aggregation.
        /// </summary>
        private void UpdateSessionStatistics(double frameTimeMs)
        {
            lock (_sessionStatsLock)
            {
                _sessionStats.TotalFrameCount++;
                
                // Only track frames worse than current tracked minimums to maintain efficiency
                bool shouldTrack = _sessionStats.WorstFrameTimes.Count < _sessionStats.MaxWorstFramesTracked;
                
                if (!shouldTrack && _sessionStats.WorstFrameTimes.Count > 0)
                {
                    // Check if this frame is worse than our best tracked worst frame
                    var bestWorstFrame = _sessionStats.WorstFrameTimes.Keys.Last(); // Last = smallest (best) due to descending sort
                    shouldTrack = frameTimeMs > bestWorstFrame;
                }
                
                if (shouldTrack)
                {
                    // Add or increment this frame time
                    if (_sessionStats.WorstFrameTimes.ContainsKey(frameTimeMs))
                    {
                        _sessionStats.WorstFrameTimes[frameTimeMs]++;
                    }
                    else
                    {
                        _sessionStats.WorstFrameTimes[frameTimeMs] = 1;
                        
                        // If we exceed our limit, remove the best (smallest) worst frame
                        if (_sessionStats.WorstFrameTimes.Count > _sessionStats.MaxWorstFramesTracked)
                        {
                            var bestWorstFrame = _sessionStats.WorstFrameTimes.Keys.Last();
                            _sessionStats.WorstFrameTimes.Remove(bestWorstFrame);
                        }
                    }
                    
                    _sessionStats.TotalWorstFramesProcessed++;
                }
            }
        }

        /// <summary>
        /// Calculate 1% low FPS using time-weighted method following CapFrameX approach.
        /// Now includes focus-aware filtering to exclude alt-tab/overlay frame times.
        /// Returns the average frame rate during the worst 1% of focused gameplay time.
        /// </summary>
        private double Calculate1PercentLow()
        {
            lock (_frameBufferLock)
            {
                // Get all frame data
                var allFrameData = _timedFrameBuffer.ToArray();
                
                // Apply focus filtering if enabled
                var frameData = allFrameData;
                if (_configService.EnableFocusFiltering)
                {
                    var focusedFrames = allFrameData.Where(f => f.WasFocused).ToArray();
                    var unfocusedCount = allFrameData.Length - focusedFrames.Length;
                    
                    if (unfocusedCount > 0)
                    {
                        _fileLogger?.LogDebugThrottled($"Focus filtering: Using {focusedFrames.Length} focused frames, excluded {unfocusedCount} unfocused frames", "focus_filtering_stats");
                    }
                    
                    frameData = focusedFrames;
                }
                
                // Need sufficient data for meaningful calculation
                if (frameData.Length < 60) // At least 1 second at 60 FPS
                {
                    _fileLogger?.LogDebugThrottled($"Insufficient frame data for 1% low calculation: {frameData.Length} frames (need 60+)", "insufficient_frames");
                    return 0.0;
                }

                try
                {
                    var totalDuration = frameData.Max(f => f.Timestamp) - frameData.Min(f => f.Timestamp);
                    
                    // Check minimum focused buffer duration if focus filtering is enabled
                    double requiredDurationSeconds = _configService.EnableFocusFiltering ? 
                        _configService.MinFocusedBufferSeconds : 10;
                    
                    if (totalDuration.TotalSeconds < requiredDurationSeconds)
                    {
                        _fileLogger?.LogDebugThrottled($"Insufficient buffer duration: {totalDuration.TotalSeconds:F1}s (need {requiredDurationSeconds}s)", "insufficient_duration");
                        return 0.0;
                    }

                    // Calculate 1% of total time duration
                    var onePercentDuration = totalDuration.TotalMilliseconds * 0.01;
                    
                    // Sort frames by frame time (worst frames first)
                    var sortedFrames = frameData.OrderByDescending(f => f.FrameTimeMs).ToArray();
                    
                    // Accumulate worst frame times until we reach 1% of total time
                    double accumulatedTime = 0;
                    var worstFrames = new List<TimedFrameData>();
                    
                    foreach (var frame in sortedFrames)
                    {
                        worstFrames.Add(frame);
                        accumulatedTime += frame.FrameTimeMs;
                        
                        if (accumulatedTime >= onePercentDuration)
                            break;
                    }
                    
                    // Calculate average frame time of worst 1% of time
                    var averageWorstFrameTime = worstFrames.Average(f => f.FrameTimeMs);
                    var onePercentLowFps = averageWorstFrameTime > 0 ? 1000.0 / averageWorstFrameTime : 0.0;
                    
                    // Enhanced logging every 100 calculations
                    _onePercentLowCalculationCount++;
                    if (_onePercentLowCalculationCount % 100 == 0)
                    {
                        string filterInfo = _configService.EnableFocusFiltering ? 
                            $" (focus-filtered: {frameData.Length}/{allFrameData.Length} frames)" : "";
                        _fileLogger?.LogInfo($"1% Low Calculation: {frameData.Length} frames over {totalDuration.TotalSeconds:F1}s, worst {worstFrames.Count} frames, avg worst: {averageWorstFrameTime:F2}ms = {onePercentLowFps:F1} FPS{filterInfo}");
                    }
                    
                    return onePercentLowFps;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"Error calculating focus-aware 1% low: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Enhanced 1% low calculation combining real-time (60s window) and session-wide statistics.
        /// Provides both immediate responsiveness and long-term session accuracy.
        /// </summary>
        private double CalculateEnhanced1PercentLow()
        {
            // Calculate real-time 1% low from 60-second buffer
            var realTime1PercentLow = Calculate1PercentLow();
            
            // Calculate session-wide 1% low from statistical aggregation
            var session1PercentLow = CalculateSession1PercentLow();
            
            // Hybrid approach: Use real-time for short sessions, session-wide for longer sessions
            lock (_sessionStatsLock)
            {
                var sessionMinutes = _sessionStats.TotalSessionDuration.TotalMinutes;
                
                // For sessions under 10 minutes, primarily use real-time calculation
                if (sessionMinutes < 10)
                {
                    return realTime1PercentLow;
                }
                
                // For longer sessions, blend real-time and session-wide calculations
                // Weight shifts from real-time to session-wide as session gets longer
                var sessionWeight = Math.Min(0.8, sessionMinutes / 60.0); // Max 80% session weight at 1 hour
                var realTimeWeight = 1.0 - sessionWeight;
                
                // If either calculation is invalid, use the valid one
                if (realTime1PercentLow <= 0) return session1PercentLow;
                if (session1PercentLow <= 0) return realTime1PercentLow;
                
                // Weighted blend of both calculations
                var enhancedResult = (realTime1PercentLow * realTimeWeight) + (session1PercentLow * sessionWeight);
                
                // Enhanced logging every 100 calculations
                if (_onePercentLowCalculationCount % 100 == 0)
                {
                    _fileLogger?.LogInfo($"Enhanced 1% Low: Session {sessionMinutes:F1}min, Real-time: {realTime1PercentLow:F1} FPS, Session: {session1PercentLow:F1} FPS, Blended: {enhancedResult:F1} FPS (weights: {realTimeWeight:F2}/{sessionWeight:F2})");
                }
                
                return enhancedResult;
            }
        }

        /// <summary>
        /// Calculate session-wide 1% low from statistical aggregation without storing all frames.
        /// Uses worst frame time tracking for memory-efficient long session analysis.
        /// </summary>
        private double CalculateSession1PercentLow()
        {
            lock (_sessionStatsLock)
            {
                // Need sufficient session data for meaningful calculation
                if (_sessionStats.TotalFrameCount < 1000 || _sessionStats.WorstFrameTimes.Count == 0)
                {
                    return 0.0;
                }
                
                try
                {
                    // Calculate 1% of total frames processed
                    var onePercentFrameCount = Math.Max(1, (long)(_sessionStats.TotalFrameCount * 0.01));
                    
                    // Accumulate worst frames until we reach 1% of total frame count
                    long accumulatedFrames = 0;
                    double totalWorstFrameTime = 0;
                    
                    foreach (var kvp in _sessionStats.WorstFrameTimes)
                    {
                        var frameTime = kvp.Key;
                        var count = kvp.Value;
                        
                        var framesToAdd = Math.Min(count, onePercentFrameCount - accumulatedFrames);
                        totalWorstFrameTime += frameTime * framesToAdd;
                        accumulatedFrames += framesToAdd;
                        
                        if (accumulatedFrames >= onePercentFrameCount)
                            break;
                    }
                    
                    // Calculate average worst frame time and convert to FPS
                    if (accumulatedFrames > 0)
                    {
                        var averageWorstFrameTime = totalWorstFrameTime / accumulatedFrames;
                        return averageWorstFrameTime > 0 ? 1000.0 / averageWorstFrameTime : 0.0;
                    }
                    
                    return 0.0;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"Error calculating session 1% low: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Selects the best gaming candidate using simple configuration-based filtering.
        /// </summary>
        private RTSSCandidate? SelectBestGamingCandidate(List<RTSSCandidate> candidates)
        {
            if (!candidates.Any()) return null;

            // Get configuration settings
            var ignoredProcesses = _configService.IgnoredProcesses
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim().ToLowerInvariant())
                .ToHashSet();
            var minFpsThreshold = _configService.MinimumFpsThreshold;
            var preferFullscreen = _configService.PreferFullscreen;

            // Apply configuration-based filtering
            var filteredCandidates = candidates.Where(c => 
            {
                // Skip ignored processes
                if (ignoredProcesses.Contains(c.ProcessName.ToLowerInvariant()))
                {
                    _fileLogger?.LogDebugThrottled($"Filtering out ignored process: {c.ProcessName}", $"ignored_{c.ProcessId}");
                    return false;
                }

                // Apply minimum FPS threshold
                if (c.Fps < minFpsThreshold)
                {
                    _fileLogger?.LogDebugThrottled($"Filtering out low FPS process: {c.ProcessName} ({c.Fps:F1} FPS < {minFpsThreshold})", $"low_fps_{c.ProcessId}");
                    return false;
                }

                return true;
            }).ToList();

            if (!filteredCandidates.Any())
            {
                _fileLogger?.LogDebugThrottled("All candidates filtered out by configuration", "all_filtered");
                return null;
            }

            // Simple selection logic
            RTSSCandidate? selected;

            if (preferFullscreen)
            {
                // Prefer fullscreen applications, then highest FPS
                selected = filteredCandidates
                    .OrderByDescending(c => c.IsFullscreen)
                    .ThenByDescending(c => c.Fps)
                    .First();
            }
            else
            {
                // Just select highest FPS
                selected = filteredCandidates
                    .OrderByDescending(c => c.Fps)
                    .First();
            }

            _fileLogger?.LogDebug($"Selected candidate: {selected.ProcessName} (PID {selected.ProcessId}) with {selected.Fps:F1} FPS" +
                               (selected.IsFullscreen ? " [Fullscreen]" : ""));
            return selected;
        }

        /// <summary>
        /// Checks if a process is running in fullscreen mode
        /// </summary>
        private bool IsProcessFullscreen(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero) return false;

                // Get window rectangle and screen rectangle
                if (User32.GetWindowRect(handle, out var windowRect))
                {
                    var monitor = User32.MonitorFromWindow(handle, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
                    var monitorInfo = new User32.MONITORINFO();
                    if (User32.GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        var screenRect = monitorInfo.rcMonitor;
                        return windowRect.left <= screenRect.left && 
                               windowRect.top <= screenRect.top &&
                               windowRect.right >= screenRect.right && 
                               windowRect.bottom >= screenRect.bottom;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Checks if a process is the foreground window
        /// </summary>
        private bool IsProcessForeground(int processId)
        {
            try
            {
                var foregroundWindow = User32.GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero) return false;

                User32.GetWindowThreadProcessId(foregroundWindow, out var foregroundPid);
                return foregroundPid == processId;
            }
            catch { }
            return false;
        }

        // Resolution detection methods removed - were causing confusion for borderless fullscreen users
        // RTSS resolution data was inconsistent between display modes

        /// <summary>
        /// Safely gets process name without throwing exceptions
        /// </summary>
        private string GetSafeProcessName(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                return process.ProcessName;
            }
            catch
            {
                return $"PID{processId}";
            }
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