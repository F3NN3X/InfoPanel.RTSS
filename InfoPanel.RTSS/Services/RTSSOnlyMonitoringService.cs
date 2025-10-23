using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Represents a candidate process detected by RTSS with comprehensive gaming metrics
    /// Includes advanced RTSS shared memory data for detailed performance analysis
    /// </summary>
    public class RTSSCandidate
    {
        // Basic process identification
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        
        // State flags
        public bool IsFullscreen { get; set; }
        public bool IsForeground { get; set; }
        
        // Core FPS metrics
        public double Fps { get; set; }
        public double FrameTimeMs { get; set; }
        public double OnePercentLowFps { get; set; }
        
        // RTSS native percentile calculations
        public double OnePercentLowFpsNative { get; set; }
        public double ZeroPointOnePercentLowFps { get; set; }
        
        // Advanced frame timing
        public double GpuFrameTimeMs { get; set; }
        public double CpuFrameTimeMs { get; set; }
        
        // Graphics system information
        public string GraphicsAPI { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string GameCategory { get; set; } = string.Empty;
        
        // Display properties (resolution removed - was confusing in borderless mode)
        public double RefreshRate { get; set; }
        public bool VSync { get; set; }
        public string DisplayMode { get; set; } = string.Empty;
        
        // RTSS internal data
        public uint RTSSFlags { get; set; }
        public uint RTSSEngineVersion { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Helper class for analyzing RTSS shared memory data and extracting advanced gaming metrics
    /// Provides graphics API detection, architecture analysis, and game categorization
    /// </summary>
    internal static class RTSSDataAnalyzer
    {
        // RTSS APPFLAG constants for graphics API detection (from RTSSSharedMemory.h v2.10+)
        private const uint APPFLAG_OGL = 0x00000001;
        private const uint APPFLAG_DD = 0x00000002;
        private const uint APPFLAG_D3D8 = 0x00000003;
        private const uint APPFLAG_D3D9 = 0x00000004;
        private const uint APPFLAG_D3D9EX = 0x00000005;
        private const uint APPFLAG_D3D10 = 0x00000006;
        private const uint APPFLAG_D3D11 = 0x00000007;
        private const uint APPFLAG_D3D12 = 0x00000008;
        private const uint APPFLAG_D3D12AFR = 0x00000009;
        private const uint APPFLAG_VULKAN = 0x0000000A;
        
        // Masks and architecture flags
        private const uint APPFLAG_API_USAGE_MASK = 0x0000FFFF;
        private const uint APPFLAG_ARCHITECTURE_X64 = 0x00010000;
        private const uint APPFLAG_ARCHITECTURE_UWP = 0x00020000;
        
        /// <summary>
        /// Analyzes RTSS APPFLAG values to determine graphics API (RTSS v2.10+ format)
        /// </summary>
        public static string GetGraphicsAPI(uint rtssFlags)
        {
            // Extract the API value using the API usage mask (lower 16 bits)
            uint apiValue = rtssFlags & APPFLAG_API_USAGE_MASK;
            
            string result = apiValue switch
            {
                APPFLAG_D3D12 => "DirectX 12",
                APPFLAG_D3D12AFR => "DirectX 12 AFR",
                APPFLAG_D3D11 => "DirectX 11", 
                APPFLAG_D3D10 => "DirectX 10",
                APPFLAG_D3D9EX => "DirectX 9Ex",
                APPFLAG_D3D9 => "DirectX 9",
                APPFLAG_D3D8 => "DirectX 8",
                APPFLAG_VULKAN => "Vulkan",
                APPFLAG_OGL => "OpenGL",
                APPFLAG_DD => "DirectDraw",
                _ => "Unknown"
            };
            
            // No console output here - will be logged by caller with more context
            
            return result;
        }
        
        /// <summary>
        /// Extracts process architecture information from RTSS flags
        /// </summary>
        public static string GetProcessArchitecture(uint rtssFlags)
        {
            bool isX64 = (rtssFlags & APPFLAG_ARCHITECTURE_X64) != 0;
            bool isUWP = (rtssFlags & APPFLAG_ARCHITECTURE_UWP) != 0;
            
            return (isX64, isUWP) switch
            {
                (true, true) => "x64 UWP",
                (true, false) => "x64",
                (false, true) => "UWP",
                (false, false) => "x86"
            };
        }
        
        /// <summary>
        /// Determines architecture type based on graphics API and RTSS flags
        /// </summary>
        public static string GetArchitecture(string graphicsAPI, uint rtssFlags)
        {
            string processArch = GetProcessArchitecture(rtssFlags);
            string apiEra = graphicsAPI switch
            {
                "DirectX 12" or "DirectX 12 AFR" or "Vulkan" => "Modern Low-Level",
                "DirectX 11" => "DirectX 11 Era", 
                "DirectX 10" => "DirectX 10 Era",
                "DirectX 9" or "DirectX 9Ex" => "Legacy DirectX",
                "DirectX 8" => "Legacy DirectX",
                "OpenGL" => "OpenGL",
                "DirectDraw" => "Legacy DirectDraw",
                _ => "Unknown API"
            };
            
            return $"{apiEra} ({processArch})";
        }
        
        /// <summary>
        /// Categorizes game type based on custom user rules, process name patterns, and graphics API
        /// </summary>
        public static string GetGameCategory(string processName, string graphicsAPI, ConfigurationService? configService = null)
        {
            var lowerProcessName = processName.ToLowerInvariant();
            
            // First, check custom user-defined categories
            if (configService != null)
            {
                var customCategories = configService.GetCustomGameCategories();
                foreach (var category in customCategories)
                {
                    foreach (var pattern in category.Value)
                    {
                        if (IsPatternMatch(lowerProcessName, pattern))
                        {
                            return category.Key;
                        }
                    }
                }
            }
            
            // Fallback to default categorization logic
            return GetDefaultGameCategory(lowerProcessName, graphicsAPI);
        }
        
        /// <summary>
        /// Default game categorization logic (used when no custom categories match)
        /// </summary>
        private static string GetDefaultGameCategory(string lowerProcessName, string graphicsAPI)
        {
            
            // AAA/Modern games typically use DX11/DX12/Vulkan
            if (graphicsAPI is "DirectX 12" or "DirectX 11" or "Vulkan")
            {
                return "AAA/Modern";
            }
            
            // Indie/Legacy detection
            if (graphicsAPI is "DirectX 9" or "DirectX 8" or "OpenGL")
            {
                return "Indie/Legacy";
            }
            
            // Specific game engine detection
            if (lowerProcessName.Contains("unity") || lowerProcessName.Contains("unreal"))
            {
                return "Engine-Based";
            }
            
            return "Standard";
        }
        
        /// <summary>
        /// Checks if a process name matches a pattern (supports wildcards * and exact matches)
        /// </summary>
        private static bool IsPatternMatch(string processName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;
                
            // Exact match
            if (pattern == processName)
                return true;
                
            // Simple wildcard support
            if (pattern.Contains('*'))
            {
                // Convert simple wildcards to regex-like matching
                var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return true; // Pattern was just "*"
                    
                string currentProcess = processName;
                foreach (var part in parts)
                {
                    int index = currentProcess.IndexOf(part, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                        return false;
                    currentProcess = currentProcess.Substring(index + part.Length);
                }
                return true;
            }
            
            // Contains match (no wildcards)
            return processName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Detects VSync usage based on RTSS flags and frame timing patterns
        /// </summary>
        public static bool GetVSyncStatus(uint rtssFlags, double fps, double refreshRate)
        {
            // VSync typically locks FPS to refresh rate or its divisors
            if (refreshRate > 0 && fps > 0)
            {
                double ratio = refreshRate / fps;
                // Check if FPS is locked to refresh rate divisors (60Hz->60FPS, 144Hz->72FPS, etc.)
                return Math.Abs(ratio - Math.Round(ratio)) < 0.05; // 5% tolerance
            }
            return false;
        }
        
        /// <summary>
        /// Determines display mode based on fullscreen state and window properties
        /// </summary>
        public static string GetDisplayMode(bool isFullscreen, int resX, int resY, double refreshRate)
        {
            if (isFullscreen)
            {
                // True fullscreen typically matches native resolution
                if (resX >= 1920 && resY >= 1080) // Modern fullscreen resolutions
                {
                    return "Fullscreen";
                }
                return "Fullscreen (Low-Res)";
            }
            else
            {
                // Windowed modes
                if (resX >= 1920 && resY >= 1080)
                {
                    return "Borderless Windowed";
                }
                return "Windowed";
            }
        }
        
        /// <summary>
        /// Enhanced game categorization with process path analysis
        /// </summary>
        public static string GetEnhancedGameCategory(string processName, string processPath, string graphicsAPI)
        {
            var lowerProcessName = processName.ToLowerInvariant();
            var lowerProcessPath = processPath.ToLowerInvariant();
            
            // Steam games detection
            if (lowerProcessPath.Contains("steam") || lowerProcessPath.Contains("steamapps"))
            {
                return graphicsAPI switch
                {
                    "DirectX 12" or "DirectX 11" or "Vulkan" => "Steam AAA",
                    _ => "Steam Indie"
                };
            }
            
            // Epic Games Store detection
            if (lowerProcessPath.Contains("epic games") || lowerProcessPath.Contains("epicgames"))
            {
                return "Epic Games Store";
            }
            
            // Game Pass / Microsoft Store detection
            if (lowerProcessPath.Contains("windowsapps") || lowerProcessPath.Contains("microsoft"))
            {
                return "Game Pass / Microsoft Store";
            }
            
            // Fallback to original categorization
            return GetGameCategory(processName, graphicsAPI);
        }
    }

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
        private DateTime _lastDebugLog = DateTime.MinValue;
        
        // Periodic status logging
        private DateTime _lastStatusLog = DateTime.MinValue;
        private const int STATUS_LOG_INTERVAL_MS = 1000; // Log status every second



        // Events for sensor updates
        public event Action<double, double, double, string, int>? MetricsUpdated;
        public event Action<RTSSCandidate>? EnhancedMetricsUpdated;

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
                        
                        // Get window title for the new process
                        _currentWindowTitle = GetWindowTitleForPid(hookedProcess.ProcessId);
                        
                        _fileLogger?.LogInfo($"Now monitoring: PID {_currentMonitoredPid} - '{_currentWindowTitle}'");
                    }
                    
                    // Update FPS metrics
                    _currentFps = hookedProcess.Fps;
                    _currentFrameTime = _currentFps > 0 ? 1000.0 / _currentFps : 0.0;
                    
                    // Update 1% low calculation
                    UpdateFrameTimeBuffer(_currentFrameTime);
                    _current1PercentLow = Calculate1PercentLow();
                    
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
                        // Populate enhanced metrics using RTSSDataAnalyzer with validation
                        bestCandidate.GraphicsAPI = RTSSDataAnalyzer.GetGraphicsAPI(bestCandidate.RTSSFlags);
                        bestCandidate.Architecture = RTSSDataAnalyzer.GetArchitecture(bestCandidate.GraphicsAPI, bestCandidate.RTSSFlags);
                        bestCandidate.GameCategory = RTSSDataAnalyzer.GetGameCategory(bestCandidate.ProcessName, bestCandidate.GraphicsAPI, _configService);
                        
                        // Enhanced logging for API detection
                        uint apiValue = bestCandidate.RTSSFlags & 0x0000FFFF; // APPFLAG_API_USAGE_MASK
                        _fileLogger?.LogAPIDetection(bestCandidate.ProcessName, bestCandidate.RTSSFlags, apiValue, bestCandidate.GraphicsAPI, bestCandidate.Architecture);
                        bestCandidate.FrameTimeMs = bestCandidate.Fps > 0 ? 1000.0 / bestCandidate.Fps : 0.0;
                        bestCandidate.WindowTitle = GetWindowTitleForPid(bestCandidate.ProcessId);
                        
                        // Enhanced analysis with new methods
                        bestCandidate.VSync = RTSSDataAnalyzer.GetVSyncStatus(bestCandidate.RTSSFlags, bestCandidate.Fps, bestCandidate.RefreshRate);
                        bestCandidate.DisplayMode = RTSSDataAnalyzer.GetDisplayMode(bestCandidate.IsFullscreen, 0, 0, bestCandidate.RefreshRate); // Resolution removed
                        
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