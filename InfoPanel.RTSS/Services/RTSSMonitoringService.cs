using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using InfoPanel.RTSS.Models;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Enhanced RTSS monitoring service based on working C++ implementation
    /// Monitors ALL RTSS-hooked applications regardless of focus state
    /// Provides native RTSS statistics including proper 1% low FPS calculations
    /// Direct port from BackgroundMonitor.cpp with frame-based polling
    /// </summary>
    public class RTSSMonitoringService : IDisposable
    {
        private readonly FileLoggingService? _fileLogger;
        private readonly ConfigurationService _configService;
        private readonly object _lock = new object();
        private bool _disposed = false;
        
        // RTSS Shared Memory Access (like C++ version)
        private Kernel32.SafeHSECTION? _hMapRTSS;
        private IntPtr _pRTSSMemory = IntPtr.Zero;
        private unsafe RTSS_SHARED_MEMORY* _pRTSSHeader;
        
        // Auto-Benchmark Mode Manager (v1.2.0 feature)
        private BenchmarkModeManager? _benchmarkManager;
        
        // Public property to check benchmark mode status
        public bool HasBenchmarkModeWriteAccess => _benchmarkManager?.HasWriteAccess ?? false;
        public bool IsBenchmarkManagerInitialized => _benchmarkManager?.IsInitialized ?? false;
        
        // Current monitoring state (ported from C++)
        private uint _lastOSDFrame = 0;
        private uint _updateCount = 0;
        private readonly List<RTSSCandidate> _activeApplications = new();
        
        // Monitoring control
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitoringTask;
        
        // Events for sensor updates
        public event Action<RTSSCandidate>? PrimaryApplicationChanged;
        public event Action<List<RTSSCandidate>>? ApplicationsUpdated;
        public event Action? NoApplicationsDetected;
        
        // Legacy event for backward compatibility
        public event Action<double, double, double, string, int>? MetricsUpdated;
        public event Action<RTSSCandidate>? EnhancedMetricsUpdated;
        
        public RTSSMonitoringService(ConfigurationService configService, FileLoggingService? fileLogger = null)
        {
            _configService = configService;
            _fileLogger = fileLogger;
            
            // Initialize auto-benchmark mode manager (v1.2.0 feature)
            _benchmarkManager = new BenchmarkModeManager(fileLogger);
            
            _fileLogger?.LogInfo("Enhanced RTSS monitoring service initialized - Direct C++ port");
            _fileLogger?.LogInfo($"Auto-Benchmark Mode: {(_benchmarkManager.HasWriteAccess ? "✓ Enabled (Write Access)" : "✗ Disabled (Read-Only)")}");
        }
        
        /// <summary>
        /// Start monitoring all RTSS-hooked applications (like C++ MonitorAllApps)
        /// </summary>
        public async Task<bool> StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return false;
            
            lock (_lock)
            {
                if (_monitoringTask != null) return true;
                
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }
            
            // Initialize RTSS shared memory access (like C++ version)
            if (!InitializeRTSSMemory())
            {
                _fileLogger?.LogError("Failed to initialize RTSS shared memory - ensure RTSS is running!");
                return false;
            }
            
            _fileLogger?.LogInfo("=== RTSS Background Monitor Starting ===");
            _fileLogger?.LogInfo("Monitoring ALL applications (foreground and background)");
            
            // Start monitoring task (C++ monitoring loop ported to async)
            _monitoringTask = Task.Run(async () => await MonitoringLoopAsync(_cancellationTokenSource.Token));
            
            return true;
        }
        
        /// <summary>
        /// Stop monitoring and cleanup resources
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_disposed) return;
            
            _fileLogger?.LogInfo("Stopping RTSS monitoring...");
            
            lock (_lock)
            {
                _cancellationTokenSource?.Cancel();
            }
            
            if (_monitoringTask != null)
            {
                try
                {
                    await _monitoringTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
            }
            
            CleanupRTSSMemory();
            
            lock (_lock)
            {
                _activeApplications.Clear();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _monitoringTask = null;
            }
            
            // Notify that no applications are being monitored
            NoApplicationsDetected?.Invoke();
            
            _fileLogger?.LogInfo("RTSS monitoring stopped");
        }
        
        /// <summary>
        /// Initialize RTSS shared memory access (ported from C++ version)
        /// </summary>
        private bool InitializeRTSSMemory()
        {
            try
            {
                _fileLogger?.LogInfo("Opening RTSSSharedMemoryV2...");
                
                // Open RTSS shared memory (exact same as C++)
                _hMapRTSS = Kernel32.OpenFileMapping(Kernel32.FILE_MAP.FILE_MAP_READ, false, "RTSSSharedMemoryV2");
                if (_hMapRTSS?.IsInvalid != false)
                {
                    _fileLogger?.LogError("Cannot open RTSSSharedMemoryV2 - ensure RTSS is running!");
                    return false;
                }
                
                // Map view of file (exact same as C++)
                _pRTSSMemory = Kernel32.MapViewOfFile(_hMapRTSS, Kernel32.FILE_MAP.FILE_MAP_READ, 0, 0, IntPtr.Zero);
                if (_pRTSSMemory == IntPtr.Zero)
                {
                    _fileLogger?.LogError("Cannot map RTSS shared memory view");
                    return false;
                }
                
                // Get header pointer and validate signature (exact same as C++)
                unsafe
                {
                    _pRTSSHeader = (RTSS_SHARED_MEMORY*)_pRTSSMemory.ToPointer();
                    
                    // Validate signature (exact same check as C++)
                    if (_pRTSSHeader->dwSignature != 0x52545353) // 'RTSS'
                    {
                        _fileLogger?.LogError($"Invalid RTSS signature: expected 0x52545353 (RTSS), got 0x{_pRTSSHeader->dwSignature:X8}");
                        return false;
                    }
                    
                    // Log initialization info (like C++ version)
                    _fileLogger?.LogInfo($"RTSS Version: 0x{_pRTSSHeader->dwVersion:X8} (v{(_pRTSSHeader->dwVersion >> 16) & 0xFFFF}.{_pRTSSHeader->dwVersion & 0xFFFF})");
                    _fileLogger?.LogInfo($"App Array Offset: 0x{_pRTSSHeader->dwAppArrOffset:X8} ({_pRTSSHeader->dwAppArrOffset} bytes)");
                    _fileLogger?.LogInfo($"App Array Entries: {_pRTSSHeader->dwAppArrSize} (Entry Size: {_pRTSSHeader->dwAppEntrySize} bytes, Total: {_pRTSSHeader->dwAppArrSize * _pRTSSHeader->dwAppEntrySize} bytes)");
                    
                    // Quick initial scan like C++ version
                    PerformInitialScan();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error initializing RTSS memory: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Perform initial scan to show ALL entries (like C++ debug scan)
        /// </summary>
        private unsafe void PerformInitialScan()
        {
            try
            {
                if (_pRTSSHeader == null) return;
                
                // Calculate max apps - dwAppArrSize is now the COUNT of entries
                uint maxApps = Math.Min(_pRTSSHeader->dwAppArrSize, 256); // Safety limit like C++
                
                uint foundApps = 0;
                _fileLogger?.LogInfo("=== Initial RTSS Application Scan ===");
                _fileLogger?.LogInfo($"Scanning {maxApps} potential app entries...");
                
                // Debug: Let's check the first few entries in detail
                _fileLogger?.LogInfo("=== Detailed Memory Scan (first 10 entries) ===");
                uint debugEntries = Math.Min(maxApps, 10);
                for (uint i = 0; i < debugEntries; i++)
                {
                    // Calculate app array base like C++: pAppArray = (LPBYTE)pMem + pMem->dwAppArrOffset
                    IntPtr appArrayBase = _pRTSSMemory + (int)_pRTSSHeader->dwAppArrOffset;
                    
                    // Then calculate individual app like C++: pApp = (LPBYTE)pAppArray + (i * pMem->dwAppEntrySize)
                    IntPtr appPtr = appArrayBase + (int)(i * _pRTSSHeader->dwAppEntrySize);
                    
                    // Try both methods: Marshal.ReadInt32 and Marshal.PtrToStructure
                    uint processIdManual = (uint)Marshal.ReadInt32(appPtr, 0);
                    
                    try 
                    {
                        var appEntry = Marshal.PtrToStructure<RTSS_SHARED_MEMORY_APP_ENTRY>(appPtr);
                        _fileLogger?.LogInfo($"  Entry[{i}]: Manual PID={processIdManual} | Struct PID={appEntry.dwProcessID} | Flags=0x{appEntry.dwFlags:X8}");
                        
                        if (appEntry.dwProcessID != 0)
                        {
                            _fileLogger?.LogInfo($"    -> FOUND APP: PID={appEntry.dwProcessID}, Name='{appEntry.szName}', Flags=0x{appEntry.dwFlags:X8}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _fileLogger?.LogError($"  Entry[{i}]: Structure read failed: {ex.Message}");
                        // Fall back to raw data dump
                        byte[] rawData = new byte[16];
                        Marshal.Copy(appPtr, rawData, 0, 16);
                        string hexData = string.Join(" ", rawData.Select(b => $"{b:X2}"));
                        _fileLogger?.LogInfo($"    Raw data: {hexData}");
                    }
                }
                _fileLogger?.LogInfo("=== End Detailed Scan ===");
                
                // Now scan all entries for non-zero PIDs (using two-step calculation like C++)
                IntPtr scanArrayBase = _pRTSSMemory + (int)_pRTSSHeader->dwAppArrOffset;
                
                for (uint i = 0; i < maxApps; i++)
                {
                    IntPtr appPtr = scanArrayBase + (int)(i * _pRTSSHeader->dwAppEntrySize);
                    uint processId = (uint)Marshal.ReadInt32(appPtr, 0); // dwProcessID is at offset 0
                    
                    if (processId != 0)
                    {
                        string processName = GetProcessName((int)processId);
                        // CRITICAL: dwFlags is at offset 264 (after dwProcessID=4 + szName=260)
                        uint flags = (uint)Marshal.ReadInt32(appPtr, 264); // dwFlags is at offset 264, NOT 4!
                        _fileLogger?.LogInfo($"  [{i}] PID: {processId}, Name: {processName}, Flags: 0x{flags:X8}");
                        foundApps++;
                    }
                }
                
                if (foundApps == 0)
                {
                    _fileLogger?.LogInfo("  WARNING: No applications found in RTSS array!");
                    _fileLogger?.LogInfo("  This could mean:");
                    _fileLogger?.LogInfo("    1. No games are running with RTSS hooks loaded");
                    _fileLogger?.LogInfo("    2. RTSS is not injecting into the game");
                    _fileLogger?.LogInfo("    3. The game is using an API RTSS doesn't support");
                }
                else
                {
                    _fileLogger?.LogInfo($"  Found {foundApps} hooked application(s)");
                }
                
                _fileLogger?.LogInfo("=== End Initial Scan ===");
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error in initial scan: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Main monitoring loop - frame-based polling with time-based fallback
        /// </summary>
        private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
        {
            const int POLLING_INTERVAL_MS = 16; // ~60Hz polling
            const int FORCE_SCAN_INTERVAL_MS = 1000; // Force scan every 1 second even if no frame updates
            
            DateTime lastForceScan = DateTime.UtcNow;
            
            try
            {
                _fileLogger?.LogInfo("=== Monitoring loop started ===");
                
                while (!cancellationToken.IsCancellationRequested)
                {
                    uint currentFrame;
                    uint foregroundPID;
                    
                    unsafe
                    {
                        if (_pRTSSHeader == null) break;
                        
                        currentFrame = _pRTSSHeader->dwOSDFrame;
                        foregroundPID = _pRTSSHeader->dwLastForegroundAppProcessID;
                    }
                    
                    // Check if we should force a scan (time-based fallback)
                    // This ensures we detect when games close even if RTSS stops updating frames
                    bool forceScan = (DateTime.UtcNow - lastForceScan).TotalMilliseconds >= FORCE_SCAN_INTERVAL_MS;
                    bool frameUpdated = currentFrame != _lastOSDFrame;
                    
                    // Only scan if frame updated OR force scan interval reached
                    if (!frameUpdated && !forceScan)
                    {
                        await Task.Delay(POLLING_INTERVAL_MS, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    
                    if (frameUpdated)
                    {
                        _lastOSDFrame = currentFrame;
                        _updateCount++;
                    }
                    
                    if (forceScan)
                    {
                        lastForceScan = DateTime.UtcNow;
                        _fileLogger?.LogInfo($"[ForceScan] Scanning applications (no frame update for 1s)");
                    }
                    
                    // Scan all applications
                    var applications = ScanAllApplications();
                    
                    lock (_lock)
                    {
                        _activeApplications.Clear();
                        _activeApplications.AddRange(applications);
                    }
                    
                    // Find primary application (foreground 3D app like C++)
                    var primaryApp = applications.FirstOrDefault(app => 
                        app.IsForeground && app.HasValid3DData);
                    
                    // Notify subscribers
                    if (primaryApp != null)
                    {
                        PrimaryApplicationChanged?.Invoke(primaryApp);
                        
                        // Legacy events for backward compatibility
                        MetricsUpdated?.Invoke(primaryApp.Fps, primaryApp.FrameTimeMs, 
                            primaryApp.OnePercentLowFps, primaryApp.ProcessName, primaryApp.ProcessId);
                        EnhancedMetricsUpdated?.Invoke(primaryApp);
                    }
                    else
                    {
                        // No primary 3D application detected - reset sensors
                        NoApplicationsDetected?.Invoke();
                        // Legacy event
                        MetricsUpdated?.Invoke(0.0, 0.0, 0.0, "Nothing to capture", 0);
                    }
                    
                    // Notify about all applications list (even if no 3D apps)
                    if (applications.Any())
                    {
                        ApplicationsUpdated?.Invoke(applications);
                    }
                    
                    // Summary logging every 60 frames (~1 second at 60Hz) - exact same as C++
                    if (_updateCount % 60 == 0 && _fileLogger != null)
                    {
                        var total3DApps = applications.Count(app => app.HasValid3DData);
                        var totalApps = applications.Count;
                        
                        _fileLogger.LogInfo($"=== Summary [Frame {_lastOSDFrame}] === Total Apps: {totalApps} | 3D Apps: {total3DApps} | Foreground PID: {foregroundPID}");
                    }
                    
                    await Task.Delay(POLLING_INTERVAL_MS, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error in monitoring loop: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Scan all RTSS application entries and extract performance data (ported from C++)
        /// </summary>
        private List<RTSSCandidate> ScanAllApplications()
        {
            var applications = new List<RTSSCandidate>();
            
            try
            {
                unsafe
                {
                    if (_pRTSSHeader == null) return applications;
                    
                    // Calculate max apps like C++: dwAppArrSize is total bytes, divide by entry size  
                    // Calculate max apps - dwAppArrSize is now the COUNT of entries
                    uint maxApps = Math.Min(_pRTSSHeader->dwAppArrSize, 256); // Safety limit like C++
                    uint foregroundPID = _pRTSSHeader->dwLastForegroundAppProcessID;
                    uint currentFrame = _pRTSSHeader->dwOSDFrame;
                    
                    // Get app array base like C++
                    IntPtr monitorArrayBase = _pRTSSMemory + (int)_pRTSSHeader->dwAppArrOffset;
                    
                    for (uint i = 0; i < maxApps; i++)
                    {
                        IntPtr appPtr = monitorArrayBase + (int)(i * _pRTSSHeader->dwAppEntrySize);
                        
                        // Read basic app entry data using Marshal for safe access
                        uint processId = (uint)Marshal.ReadInt32(appPtr, 0);
                        
                        // Skip empty entries (exact same check as C++)
                        if (processId == 0) continue;
                        
                        var candidate = CreateRTSSCandidate(appPtr, foregroundPID, i, currentFrame);
                        if (candidate != null)
                        {
                            applications.Add(candidate);
                            
                            // Auto-enable benchmark mode for 3D apps (v1.2.0 feature)
                            // This ensures frame time statistics are available
                            // Pass index and offsets so BenchmarkModeManager can calculate pointer from its OWN writable mapping
                            if (candidate.HasValid3DData && _benchmarkManager != null)
                            {
                                _benchmarkManager.EnableBenchmarkMode(
                                    i, 
                                    _pRTSSHeader->dwAppEntrySize, 
                                    _pRTSSHeader->dwAppArrOffset,
                                    candidate.ProcessName, 
                                    processId
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error scanning applications: {ex.Message}");
            }
            
            return applications;
        }
        
        /// <summary>
        /// Create RTSSCandidate from RTSS application entry (ported from C++)
        /// </summary>
        private RTSSCandidate? CreateRTSSCandidate(IntPtr appPtr, uint foregroundPID, uint slotIndex, uint currentFrame)
        {
            try
            {
                // Use Marshal.PtrToStructure to read the complete structure correctly
                var appEntry = Marshal.PtrToStructure<RTSS_SHARED_MEMORY_APP_ENTRY>(appPtr);
                
                uint processId = appEntry.dwProcessID;
                
                // CRITICAL FIX v1.2.0: Validate process still exists to avoid stuck values
                // RTSS keeps stale entries in shared memory even after process exits
                if (!IsProcessRunning((int)processId))
                {
                    return null; // Process no longer running, skip this stale entry
                }
                
                uint flags = appEntry.dwFlags;
                
                // Timing data
                uint time0 = appEntry.dwTime0;
                uint time1 = appEntry.dwTime1;
                uint frames = appEntry.dwFrames;
                uint frameTimeUs = appEntry.dwFrameTime;
                
                // Resolution
                uint resX = appEntry.dwResolutionX;
                uint resY = appEntry.dwResolutionY;
                
                // RTSS native statistics (stored as DWORD * 10)
                uint statMin = appEntry.dwStatFramerateMin;
                uint statAvg = appEntry.dwStatFramerateAvg;
                uint statMax = appEntry.dwStatFramerateMax;
                uint stat1PctLow = appEntry.dwStatFramerate1Dot0PercentLow; // ⭐ Native RTSS 1% low!
                
                // Get process name (same logic as C++)
                string processName = string.IsNullOrEmpty(appEntry.szName) 
                    ? GetProcessName((int)processId) 
                    : Path.GetFileNameWithoutExtension(appEntry.szName);
                
                if (string.IsNullOrEmpty(processName))
                {
                    processName = $"PID_{processId}";
                }
                
                // Calculate current FPS (exact same calculation as C++)
                float fps = RTSSCalculations.CalculateFramerate(time0, time1, frames);
                float frameTimeMs = frameTimeUs / 1000.0f; // µs to ms (exact same as C++)
                
                // Extract RTSS native statistics (exact same as C++ - stored as DWORD * 10)
                float minFps = RTSSCalculations.ConvertRTSSStatistic(statMin);
                float avgFps = RTSSCalculations.ConvertRTSSStatistic(statAvg);
                float maxFps = RTSSCalculations.ConvertRTSSStatistic(statMax);
                float onePercentLow = RTSSCalculations.ConvertRTSSStatistic(stat1PctLow); // ⭐ Native RTSS 1% low!
                
                // Determine if this is a 3D application (exact same logic as C++)
                bool is3DApp = RTSSCalculations.Is3DApplication(time0, time1, frames);
                
                // Get API name
                string apiName = RTSSFlags.GetAPIName(flags);
                
                // Check if foreground
                bool isForeground = (processId == foregroundPID);
                
                // Get window title from PID (for display purposes)
                string windowTitle = GetWindowTitleFromPid((int)processId);
                
                // Detect window mode (Borderless, Fullscreen, Windowed, etc.)
                string windowMode = Analysis.WindowModeDetector.GetEnhancedDisplayMode((int)processId, isForeground, 0, _fileLogger);
                
                // Log every 3D app update in C++ format (real-time, every frame)
                if (is3DApp)
                {
                    string status = isForeground ? "[FG]" : "[BG]";
                    _fileLogger?.LogInfo($"[Frame {currentFrame}] Slot={slotIndex} PID={processId} {processName} {status} | " +
                                        $"FPS={fps:F1} FrameTime={frameTimeMs:F2}ms | " +
                                        $"Min={minFps:F1} Avg={avgFps:F1} Max={maxFps:F1} 1%Low={onePercentLow:F1} | " +
                                        $"API={apiName} Res={resX}x{resY}");
                }
                
                var candidate = new RTSSCandidate
                {
                    ProcessId = (int)processId,
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    IsForeground = isForeground,
                    
                    // Current performance
                    Fps = fps,
                    FrameTimeMs = frameTimeMs,
                    
                    // RTSS native statistics (⭐ This is the key data we want!)
                    MinFps = minFps,
                    AvgFps = avgFps,
                    MaxFps = maxFps,
                    OnePercentLowFps = onePercentLow,
                    
                    // Technical details
                    GraphicsAPI = apiName,
                    WindowMode = windowMode,
                    ResolutionX = resX,
                    ResolutionY = resY,
                    IsX64 = RTSSFlags.IsX64(flags),
                    IsUWP = RTSSFlags.IsUWP(flags),
                    
                    // RTSS internal data
                    RTSSFlags = flags,
                    FrameCount = frames,
                    Time0 = time0,
                    Time1 = time1,
                    FrameTimeUs = frameTimeUs,
                    Is3DApplication = is3DApp,
                    
                    LastUpdated = DateTime.UtcNow
                };
                
                return candidate;
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error creating RTSS candidate: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Get process name from PID (same as C++ GetProcessName)
        /// </summary>
        private string GetProcessName(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
        
        /// <summary>
        /// Check if process is still running (v1.2.0 fix for stuck values)
        /// </summary>
        private bool IsProcessRunning(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                bool hasExited = process.HasExited;
                
                if (hasExited)
                {
                    _fileLogger?.LogInfo($"[ProcessCheck] PID {pid} has EXITED - will skip this RTSS entry");
                }
                
                return !hasExited;
            }
            catch (ArgumentException)
            {
                _fileLogger?.LogInfo($"[ProcessCheck] PID {pid} NOT FOUND - will skip this RTSS entry");
                return false; // Process not found
            }
            catch (Exception ex)
            {
                _fileLogger?.LogWarning($"[ProcessCheck] PID {pid} check failed: {ex.Message}");
                return false; // Assume not running on error
            }
        }
        
        /// <summary>
        /// Get window title from process ID by enumerating all windows
        /// </summary>
        private string GetWindowTitleFromPid(int pid)
        {
            string? foundTitle = null;
            
            try
            {
                // Enumerate all windows and find the one matching our PID
                User32.EnumWindows((hWnd, lParam) =>
                {
                    // Get the process ID for this window
                    User32.GetWindowThreadProcessId(hWnd, out uint windowPid);
                    
                    if (windowPid == pid)
                    {
                        // Check if window is visible
                        if (User32.IsWindowVisible(hWnd))
                        {
                            // Get window title
                            int length = User32.GetWindowTextLength(hWnd);
                            if (length > 0)
                            {
                                var sb = new System.Text.StringBuilder(length + 1);
                                User32.GetWindowText(hWnd, sb, sb.Capacity);
                                string title = sb.ToString();
                                
                                // Only use non-empty titles
                                if (!string.IsNullOrWhiteSpace(title))
                                {
                                    foundTitle = title;
                                    return false; // Stop enumeration
                                }
                            }
                        }
                    }
                    
                    return true; // Continue enumeration
                }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError($"Error getting window title for PID {pid}: {ex.Message}");
            }
            
            return foundTitle ?? string.Empty;
        }
        
        /// <summary>
        /// Get current foreground application
        /// </summary>
        public RTSSCandidate? GetForegroundApplication()
        {
            lock (_lock)
            {
                return _activeApplications.FirstOrDefault(app => app.IsForeground && app.HasValid3DData);
            }
        }
        
        /// <summary>
        /// Get all active 3D applications
        /// </summary>
        public List<RTSSCandidate> GetAll3DApplications()
        {
            lock (_lock)
            {
                return _activeApplications.Where(app => app.HasValid3DData).ToList();
            }
        }
        
        /// <summary>
        /// Cleanup RTSS memory resources (same as C++)
        /// </summary>
        private void CleanupRTSSMemory()
        {
            if (_pRTSSMemory != IntPtr.Zero)
            {
                Kernel32.UnmapViewOfFile(_pRTSSMemory);
                _pRTSSMemory = IntPtr.Zero;
            }
            
            _hMapRTSS?.Dispose();
            _hMapRTSS = null;
            
            unsafe
            {
                _pRTSSHeader = null;
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            StopMonitoringAsync().GetAwaiter().GetResult();
            
            // Cleanup benchmark mode manager
            _benchmarkManager?.Dispose();
            _benchmarkManager = null;
            
            _disposed = true;
        }
    }
}