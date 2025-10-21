using System.Diagnostics;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Service for monitoring frame rates using GPU performance counters.
    /// Provides anti-cheat compatible FPS monitoring via Windows GPU counters.
    /// </summary>
    public class DXGIFrameMonitoringService : IDisposable
    {
        private readonly FileLoggingService? _fileLogger;
        
        private double _currentFps;
        private double _averageFrameTime;
        private uint _monitoredProcessId;
        private double _lastValidFps; // Hold last valid FPS to prevent flickering to 0
        private DateTime _lastDetailedLogTime = DateTime.MinValue;
        
        // RTSS built-in statistics
        private double _minFps;
        private double _avgFps;
        private double _maxFps;

        private CancellationTokenSource? _cts;
        private volatile bool _isMonitoring;

        /// <summary>
        /// Event fired when FPS data is updated (includes statistics).
        /// Parameters: (fps, frameTimeMs, minFps, avgFps, maxFps)
        /// </summary>
        public event Action<double, double, double, double, double>? FpsUpdated;
        
        /// <summary>
        /// Event fired when RTSS successfully hooks a process.
        /// Parameters: (processId, windowTitle)
        /// </summary>
        public event Action<uint, string>? RTSSHooked;
        
        /// <summary>
        /// Gets the process ID currently being monitored by RTSS.
        /// Returns 0 if not monitoring.
        /// </summary>
        public uint MonitoredProcessId => _monitoredProcessId;

        /// <summary>
        /// Gets the current FPS value.
        /// </summary>
        public double CurrentFps => _currentFps;

        /// <summary>
        /// Gets the current average frame time in milliseconds.
        /// </summary>
        public double AverageFrameTime => _averageFrameTime;

        /// <summary>
        /// Gets the minimum FPS from RTSS statistics (1% low equivalent).
        /// </summary>
        public double MinFps => _minFps;

        /// <summary>
        /// Gets the average FPS from RTSS statistics.
        /// </summary>
        public double AvgFps => _avgFps;

        /// <summary>
        /// Gets the maximum FPS from RTSS statistics.
        /// </summary>
        public double MaxFps => _maxFps;

        /// <summary>
        /// Initializes the service with optional file logging.
        /// </summary>
        public DXGIFrameMonitoringService(FileLoggingService? fileLogger = null)
        {
            _fileLogger = fileLogger;
            _fileLogger?.LogInfo("DXGIFrameMonitoringService initialized");
        }

        /// <summary>
        /// Gets detailed information about a process for logging.
        /// </summary>
        private string GetDetailedProcessInfo(uint pid)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById((int)pid);
                var windowTitle = GetProcessWindowTitle(process);
                var executablePath = GetProcessExecutablePath(process);
                var processName = process.ProcessName;
                var workingSet = process.WorkingSet64 / (1024 * 1024); // MB
                var startTime = process.StartTime;
                var uptime = DateTime.Now - startTime;
                
                return $"PID:{pid} | Name:'{processName}' | Window:'{windowTitle}' | " +
                       $"Executable:'{executablePath}' | Memory:{workingSet}MB | " +
                       $"Uptime:{uptime.TotalMinutes:F1}min | Started:{startTime:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                return $"PID:{pid} | ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the window title for a process with retry logic for games.
        /// </summary>
        private string GetProcessWindowTitle(System.Diagnostics.Process process)
        {
            try
            {
                // Method 1: Try MainWindowTitle first (with refresh for games that just started)
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    process.Refresh(); // Refresh process info to get latest window state
                    var title = process.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title) && title.Length > 1)
                    {
                        return title;
                    }
                }
                
                // Method 2: For games, try to wait a bit for window to be created
                var processName = process.ProcessName;
                var isGameProcess = IsLikelyGameProcess(processName);
                _fileLogger?.LogInfo($"🎮 Window Title Debug: ProcessName='{processName}', IsGameProcess={isGameProcess}");
                
                if (!string.IsNullOrWhiteSpace(processName) && isGameProcess)
                {
                    // Give games a brief moment to create their main window
                    for (int retry = 0; retry < 3; retry++)
                    {
                        process.Refresh();
                        var hasWindow = process.MainWindowHandle != IntPtr.Zero;
                        var windowTitle = hasWindow ? process.MainWindowTitle : "";
                        _fileLogger?.LogInfo($"🎮 Window Title Debug: Retry {retry + 1}/3, HasWindow={hasWindow}, WindowTitle='{windowTitle}'");
                        
                        if (hasWindow)
                        {
                            var title = process.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(title) && title.Length > 1)
                            {
                                _fileLogger?.LogInfo($"🎮 Window Title Debug: Found valid title: '{title}'");
                                return title;
                            }
                        }
                        if (retry < 2) Thread.Sleep(100); // Brief wait before retry
                    }
                    
                    // Fall back to game process name
                    _fileLogger?.LogInfo($"🎮 Window Title Debug: Using fallback: '{processName} (Game)'");
                    return $"{processName} (Game)";
                }
                
                return process.MainWindowHandle != IntPtr.Zero ? "[No Title]" : "[No Window]";
            }
            catch
            {
                return "[Title Error]";
            }
        }
        
        /// <summary>
        /// Determines if a process name likely represents a game executable.
        /// </summary>
        private static bool IsLikelyGameProcess(string processName)
        {
            var lowerName = processName.ToLowerInvariant();
            
            // Common game executables
            return lowerName.Contains("game") || 
                   lowerName.EndsWith(".exe") && (
                       lowerName.Contains("bf") ||        // Battlefield series
                       lowerName.Contains("nms") ||       // No Man's Sky
                       lowerName.Contains("cod") ||       // Call of Duty
                       lowerName.Contains("apex") ||      // Apex Legends
                       lowerName.Contains("valorant") ||  // Valorant
                       lowerName.Contains("csgo") ||      // CS:GO
                       lowerName.Contains("dota") ||      // Dota 2
                       lowerName.Contains("lol") ||       // League of Legends
                       lowerName.Contains("wow") ||       // World of Warcraft
                       lowerName.Contains("minecraft") || // Minecraft
                       lowerName.Contains("fortnite") ||  // Fortnite
                       lowerName.Contains("steam") ||     // Steam games
                       lowerName.Length <= 5              // Short names often games
                   );
        }

        /// <summary>
        /// Gets a shortened executable path for a process.
        /// </summary>
        private string GetProcessExecutablePath(System.Diagnostics.Process process)
        {
            try
            {
                var fullPath = process.MainModule?.FileName ?? "[Unknown Path]";
                var fileName = Path.GetFileName(fullPath);
                var directory = Path.GetDirectoryName(fullPath);
                
                // Show last 2 directory levels for context
                if (!string.IsNullOrEmpty(directory))
                {
                    var parts = directory.Split(Path.DirectorySeparatorChar);
                    var relevantParts = parts.Skip(Math.Max(0, parts.Length - 2)).ToArray();
                    var shortPath = string.Join("\\", relevantParts);
                    return $"{shortPath}\\{fileName}";
                }
                
                return fileName;
            }
            catch
            {
                return "[Path Error]";
            }
        }

        /// <summary>
        /// Checks if a process is still running.
        /// </summary>
        private bool IsProcessRunning(uint pid)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById((int)pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts monitoring FPS for the specified process.
        /// </summary>
        public async Task StartMonitoringAsync(uint processId, CancellationToken cancellationToken = default)
        {
            if (_isMonitoring)
            {
                _fileLogger?.LogInfo("Already monitoring, stopping current monitoring first");
                Console.WriteLine("DXGIFrameMonitoringService: Already monitoring, stopping first");
                await StopMonitoringAsync();
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _isMonitoring = true;
            _monitoredProcessId = processId;

            _fileLogger?.LogMonitoringState("STARTED", (int)processId);
            Console.WriteLine($"DXGIFrameMonitoringService: Starting frame monitoring for PID {processId}");

            try
            {
                // Always try RTSS first (with retry logic for when game isn't hooked yet)
                _fileLogger?.LogInfo("Attempting RTSS shared memory monitoring (will retry if not hooked yet)");
                Console.WriteLine("DXGIFrameMonitoringService: Attempting RTSS shared memory monitoring (will retry if not hooked yet)");
                await MonitorWithRTSSAsync(processId, _cts.Token);
                
                // If MonitorWithRTSSAsync exits (timeout or cancelled), fall back to GPU counters
                if (_cts.Token.IsCancellationRequested)
                {
                    return;
                }
                
                Console.WriteLine("DXGIFrameMonitoringService: RTSS monitoring ended, falling back to GPU counters");

                // Then try GPU performance counters
                var gpuCounterResult = TryGetGPUFrameRateCounter(processId);
                if (gpuCounterResult.HasValue)
                {
                    var (gpuPerfCounter, counterName) = gpuCounterResult.Value;
                    Console.WriteLine("DXGIFrameMonitoringService: Using GPU Performance Counter for frame rate");
                    await MonitorWithPerformanceCounterAsync(gpuPerfCounter!, counterName, _cts.Token);
                    return;
                }
                else
                {
                    Console.WriteLine("DXGIFrameMonitoringService: GPU Performance Counter not available, using timing estimation");
                    await MonitorWithTimingEstimationAsync(processId, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("DXGIFrameMonitoringService: Monitoring cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DXGIFrameMonitoringService: Error in monitoring loop: {ex.Message}");
            }
            finally
            {
                _isMonitoring = false;
                Console.WriteLine("DXGIFrameMonitoringService: Frame monitoring loop ended");
            }
        }

        /// <summary>
        /// Stops the current monitoring session.
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            _isMonitoring = false;
            _monitoredProcessId = 0; // Clear monitored PID
            await Task.Delay(100); // Allow cleanup
        }

        /// <summary>
        /// Attempts to create a GPU frame rate performance counter for the process.
        /// Returns null if not available (requires GPU driver support).
        /// </summary>
        private (PerformanceCounter?, string)? TryGetGPUFrameRateCounter(uint processId)
        {
            try
            {
                // First, specifically look for the target process in GPU Engine category
                // Since RTSS detects D3D12 for BF6, prioritize 3D engine instances
                var gpuEngineCategory = new PerformanceCounterCategory("GPU Engine");
                var allInstances = gpuEngineCategory.GetInstanceNames();

                Console.WriteLine($"DXGIFrameMonitoringService: Scanning {allInstances.Length} GPU Engine instances for process {processId}");

                // First pass: Look specifically for our target process's 3D engine instance
                foreach (var instance in allInstances)
                {
                    if (instance.Contains($"pid_{processId}") && instance.Contains("engtype_3D"))
                    {
                        Console.WriteLine($"DXGIFrameMonitoringService: Found process-specific 3D instance: {instance}");
                        var counters = gpuEngineCategory.GetCounters(instance);
                        Console.WriteLine($"DXGIFrameMonitoringService: Instance {instance} has {counters.Length} counters:");
                        foreach (var counter in counters)
                        {
                            Console.WriteLine($"    - {counter.CounterName}");
                        }

                        // Look for utilization counters that can estimate FPS
                        foreach (var counter in counters)
                        {
                            if (counter.CounterName.ToLower().Contains("utilization percentage") ||
                                counter.CounterName.ToLower().Contains("utilization") ||
                                counter.CounterName.ToLower().Contains("busy") ||
                                counter.CounterName.ToLower().Contains("percentage"))
                            {
                                Console.WriteLine($"DXGIFrameMonitoringService: Found utilization counter for process {processId}: {counter.CounterName}");
                                try
                                {
                                    var perfCounter = new PerformanceCounter("GPU Engine", counter.CounterName, instance, true);
                                    var testValue = perfCounter.NextValue();
                                    Console.WriteLine($"DXGIFrameMonitoringService: Counter test value: {testValue}");
                                    Console.WriteLine($"DXGIFrameMonitoringService: Using GPU utilization counter for FPS estimation");
                                    return (perfCounter, counter.CounterName);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"DXGIFrameMonitoringService: Counter test failed: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                // If no process-specific 3D counter found, try the broader search
                // Try to find GPU 3D performance counter for the process
                // This reads actual GPU presentation rate, not utilization estimation
                var category = new PerformanceCounterCategory("GPU Adapter Memory");
                var instanceNames = category.GetInstanceNames();

                Console.WriteLine($"DXGIFrameMonitoringService: Found {instanceNames.Length} GPU counter instances:");
                foreach (var instance in instanceNames)
                {
                    Console.WriteLine($"  - {instance}");
                }

                foreach (var instanceName in instanceNames)
                {
                    if (instanceName.Contains($"pid_{processId}"))
                    {
                        Console.WriteLine($"DXGIFrameMonitoringService: Found process-specific GPU counter: {instanceName}");
                        // Found process-specific GPU counter
                        return (new PerformanceCounter(
                            "GPU Adapter Memory",
                            "Local Usage",
                            instanceName,
                            true), "Local Usage");
                    }
                }

                // Also try other GPU counter categories that might have frame rate info
                var gpuCategories = new[] { "GPU Engine", "DXVA2", "Graphics", "GPU Adapter Memory", "GPU Process Memory", "GPU Non Local Adapter Memory" };
                foreach (var catName in gpuCategories)
                {
                    try
                    {
                        var cat = new PerformanceCounterCategory(catName);
                        var instances = cat.GetInstanceNames();
                        Console.WriteLine($"DXGIFrameMonitoringService: Checking {catName} category ({instances.Length} instances)");

                        // Look for instances that might contain frame rate data
                        // First priority: exact process match
                        foreach (var instance in instances)
                        {
                            if (instance.Contains($"pid_{processId}"))
                            {
                                var counters = cat.GetCounters(instance);
                                Console.WriteLine($"DXGIFrameMonitoringService: Process-specific instance {instance} has {counters.Length} counters:");
                                foreach (var counter in counters)
                                {
                                    Console.WriteLine($"    - {counter.CounterName}");
                                    if (counter.CounterName.ToLower().Contains("frame") ||
                                        counter.CounterName.ToLower().Contains("rate") ||
                                        counter.CounterName.ToLower().Contains("fps") ||
                                        counter.CounterName.ToLower().Contains("utilization") ||
                                        counter.CounterName.ToLower().Contains("running time") ||
                                        counter.CounterName.ToLower().Contains("activity") ||
                                        counter.CounterName.ToLower().Contains("busy") ||
                                        counter.CounterName.ToLower().Contains("percentage"))
                                    {
                                        Console.WriteLine($"DXGIFrameMonitoringService: Found process-specific counter: {catName}\\{instance}\\{counter.CounterName}");
                                        try
                                        {
                                            var testCounter = new PerformanceCounter(catName, counter.CounterName, instance, true);
                                            var testValue = testCounter.NextValue();
                                            Console.WriteLine($"DXGIFrameMonitoringService: Counter test value: {testValue}");

                                            // Prioritize actual FPS/frame rate counters over utilization
                                            if (counter.CounterName.ToLower().Contains("frame") ||
                                                counter.CounterName.ToLower().Contains("rate") ||
                                                counter.CounterName.ToLower().Contains("fps"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using process-specific FPS counter: {counter.CounterName}");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                            // Check for running time counters (can be used to calculate FPS)
                                            else if (counter.CounterName.ToLower().Contains("running time"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using process-specific running time counter: {counter.CounterName}");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                            // Then check for utilization/busy percentage as fallback
                                            else if (counter.CounterName.ToLower().Contains("utilization") || counter.CounterName.ToLower().Contains("busy") || counter.CounterName.ToLower().Contains("percentage"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using process-specific GPU utilization counter");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"DXGIFrameMonitoringService: Counter test failed: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }

                        // Second priority: 3D engine instances (if no process-specific found)
                        foreach (var instance in instances)
                        {
                            // Check if this instance contains 3D data but skip if we already have process-specific
                            if (!instance.Contains($"pid_{processId}") &&
                                (instance.Contains("3D") ||
                                 instance.Contains("engtype_3D") ||
                                 instance.Contains("DirectX") ||
                                 instance.Contains("D3D")))
                            {
                                var counters = cat.GetCounters(instance);
                                Console.WriteLine($"DXGIFrameMonitoringService: 3D instance {instance} has {counters.Length} counters:");
                                foreach (var counter in counters)
                                {
                                    Console.WriteLine($"    - {counter.CounterName}");
                                    if (counter.CounterName.ToLower().Contains("frame") ||
                                        counter.CounterName.ToLower().Contains("rate") ||
                                        counter.CounterName.ToLower().Contains("fps") ||
                                        counter.CounterName.ToLower().Contains("utilization") ||
                                        counter.CounterName.ToLower().Contains("running time") ||
                                        counter.CounterName.ToLower().Contains("activity") ||
                                        counter.CounterName.ToLower().Contains("busy") ||
                                        counter.CounterName.ToLower().Contains("percentage"))
                                    {
                                        Console.WriteLine($"DXGIFrameMonitoringService: Found 3D counter: {catName}\\{instance}\\{counter.CounterName}");
                                        try
                                        {
                                            var testCounter = new PerformanceCounter(catName, counter.CounterName, instance, true);
                                            var testValue = testCounter.NextValue();
                                            Console.WriteLine($"DXGIFrameMonitoringService: Counter test value: {testValue}");

                                            // Prioritize actual FPS/frame rate counters over utilization
                                            if (counter.CounterName.ToLower().Contains("frame") ||
                                                counter.CounterName.ToLower().Contains("rate") ||
                                                counter.CounterName.ToLower().Contains("fps"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using 3D FPS counter: {counter.CounterName}");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                            // Check for running time counters (can be used to calculate FPS)
                                            else if (counter.CounterName.ToLower().Contains("running time"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using 3D running time counter: {counter.CounterName}");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                            // Then check for utilization/busy percentage as fallback
                                            else if (counter.CounterName.ToLower().Contains("utilization") || counter.CounterName.ToLower().Contains("busy") || counter.CounterName.ToLower().Contains("percentage"))
                                            {
                                                Console.WriteLine($"DXGIFrameMonitoringService: Using 3D GPU utilization counter as fallback");
                                                return (new PerformanceCounter(catName, counter.CounterName, instance, true), counter.CounterName);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"DXGIFrameMonitoringService: Counter test failed: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"DXGIFrameMonitoringService: Error checking {catName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DXGIFrameMonitoringService: Could not create GPU counter: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to read FPS data from RTSS shared memory.
        /// RTSS provides accurate frame rate data that other applications can read.
        /// Uses pattern from working RTSSExtractor: reads Frames field directly as FPS.
        /// </summary>
        public unsafe double? TryReadRTSSFps(uint processId)
        {
            try
            {
                // Try all RTSS shared memory versions (V3 doesn't exist, only V2 and V1)
                string[] memoryNames = { "RTSSSharedMemoryV2", "RTSSSharedMemoryV1" };
                const uint RTSS_SIGNATURE = 0x52545353; // "RTSS"

                foreach (var memName in memoryNames)
                {
                    try
                    {
                        using var memoryMappedFile = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(memName);
                        using var accessor = memoryMappedFile.CreateViewAccessor();

                        // Read header
                        uint signature = accessor.ReadUInt32(0);
                        if (signature != RTSS_SIGNATURE)
                        {
                            Console.WriteLine($"DXGIFrameMonitoringService: {memName} signature mismatch");
                            continue;
                        }

                        uint appEntrySize = accessor.ReadUInt32(8);   // dwAppEntrySize
                        uint appArrOffset = accessor.ReadUInt32(12);  // dwAppArrOffset (v2) or infer from header size
                        uint appArrSize = accessor.ReadUInt32(16);    // dwAppArrSize or entry count

                        // For v2+, offset is at position 12; for v1, app array starts after header
                        long appEntriesStart = (appArrOffset > 0 && appArrOffset < accessor.Capacity) ? appArrOffset : 256;
                        
                        int maxEntries = 0;
                        if (appEntrySize > 0)
                        {
                            maxEntries = (int)Math.Min(appArrSize, (accessor.Capacity - appEntriesStart) / appEntrySize);
                        }

                        Console.WriteLine($"DXGIFrameMonitoringService: Using {memName}, entries={maxEntries}, entrySize={appEntrySize}, offset={appEntriesStart}");

                        // Scan app entries using RTSS shared memory structure
                        // AppEntry structure (per documentation):
                        // PID(0), Name(4-263), Flags(264), Time0(268), Time1(272), Frames(276), FrameTime(280)
                        // Stats: StatMin(304), StatAvg(308), StatMax(312)
                        const int OFF_PID = 0;
                        const int OFF_FRAMETIME = 280; // Instantaneous frame time in microseconds
                        const int OFF_STAT_MIN = 304;  // Min FPS (1% low) in millihertz
                        const int OFF_STAT_AVG = 308;  // Average FPS in millihertz
                        const int OFF_STAT_MAX = 312;  // Max FPS in millihertz

                        for (int i = 0; i < maxEntries; i++)
                        {
                            long entryOffset = appEntriesStart + (i * appEntrySize);
                            if (entryOffset + OFF_STAT_MAX + 4 > accessor.Capacity) break;

                            uint entryPid = accessor.ReadUInt32(entryOffset + OFF_PID);
                            if (entryPid == 0) continue;
                            
                            if (entryPid == processId)
                            {
                                // Use dwFrameTime for instantaneous FPS (matches RTSS display exactly)
                                // Formula per documentation: 1000000.0 / dwFrameTime
                                uint frameTimeMicroseconds = accessor.ReadUInt32(entryOffset + OFF_FRAMETIME);
                                
                                // Validate frame time is in reasonable range (1ms to 1000ms = 1-1000 FPS)
                                if (frameTimeMicroseconds >= 1000 && frameTimeMicroseconds <= 1000000)
                                {
                                    double instantaneousFps = 1000000.0 / frameTimeMicroseconds;
                                    _lastValidFps = instantaneousFps; // Store for persistence
                                    
                                    Console.WriteLine($"DXGIFrameMonitoringService: RTSS entry {i} PID {processId} - FrameTime={frameTimeMicroseconds}μs, FPS={instantaneousFps:F1}");
                                    return instantaneousFps;
                                }
                                else if (frameTimeMicroseconds > 0)
                                {
                                    // Invalid frame time detected, hold last valid value
                                    Console.WriteLine($"DXGIFrameMonitoringService: RTSS entry {i} PID {processId} - Invalid FrameTime={frameTimeMicroseconds}μs, holding last valid FPS={_lastValidFps:F1}");
                                    return _lastValidFps > 0 ? _lastValidFps : null;
                                }
                            }
                        }

                        Console.WriteLine($"DXGIFrameMonitoringService: PID {processId} not found in {memName}");
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        // Try next version
                        continue;
                    }
                }

                Console.WriteLine("DXGIFrameMonitoringService: RTSS shared memory not available (tried all versions)");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DXGIFrameMonitoringService: Error reading RTSS shared memory: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads comprehensive RTSS statistics including FPS, frame time, and built-in min/avg/max stats.
        /// Returns a tuple with (instantaneousFPS, minFPS, avgFPS, maxFPS) or null if not available.
        /// </summary>
        public unsafe (double fps, double minFps, double avgFps, double maxFps)? TryReadRTSSStats(uint processId)
        {
            try
            {
                // Try all RTSS shared memory versions
                string[] memoryNames = { "RTSSSharedMemoryV2", "RTSSSharedMemoryV1" };
                const uint RTSS_SIGNATURE = 0x52545353; // "RTSS"

                foreach (var memName in memoryNames)
                {
                    try
                    {
                        using var mmf = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(memName);
                        using var accessor = mmf.CreateViewAccessor();

                        uint signature = accessor.ReadUInt32(0);
                        if (signature != RTSS_SIGNATURE) continue;

                        uint appEntrySize = accessor.ReadUInt32(8);
                        uint appArrOffset = accessor.ReadUInt32(12);
                        uint appArrSize = accessor.ReadUInt32(16);
                        long appEntriesStart = (appArrOffset > 0 && appArrOffset < accessor.Capacity) ? appArrOffset : 256;

                        int maxEntries = 0;
                        if (appEntrySize > 0)
                        {
                            maxEntries = (int)Math.Min(appArrSize, (accessor.Capacity - appEntriesStart) / appEntrySize);
                        }

                        // Offsets per RTSS shared memory documentation
                        const int OFF_PID = 0;
                        const int OFF_FRAMETIME = 280;  // Instantaneous frame time in microseconds
                        const int OFF_STAT_MIN = 304;   // Min FPS (1% low) in millihertz
                        const int OFF_STAT_AVG = 308;   // Average FPS in millihertz
                        const int OFF_STAT_MAX = 312;   // Max FPS in millihertz

                        for (int i = 0; i < maxEntries; i++)
                        {
                            long entryOffset = appEntriesStart + (i * appEntrySize);
                            if (entryOffset + OFF_STAT_MAX + 4 > accessor.Capacity) break;

                            uint entryPid = accessor.ReadUInt32(entryOffset + OFF_PID);
                            if (entryPid == 0 || entryPid != processId) continue;

                            // Read instantaneous FPS from frame time
                            uint frameTimeMicroseconds = accessor.ReadUInt32(entryOffset + OFF_FRAMETIME);
                            if (frameTimeMicroseconds < 1000 || frameTimeMicroseconds > 1000000) continue; // Invalid range

                            double instantaneousFps = 1000000.0 / frameTimeMicroseconds;

                            // Read RTSS built-in statistics (stored in millihertz, need to divide by 1000)
                            uint statMinMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_MIN);
                            uint statAvgMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_AVG);
                            uint statMaxMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_MAX);

                            double minFps = statMinMillihertz / 1000.0;
                            double avgFps = statAvgMillihertz / 1000.0;
                            double maxFps = statMaxMillihertz / 1000.0;

                            Console.WriteLine($"DXGIFrameMonitoringService: RTSS Stats PID {processId} - FPS={instantaneousFps:F1}, Min={minFps:F1}, Avg={avgFps:F1}, Max={maxFps:F1}");

                            return (instantaneousFps, minFps, avgFps, maxFps);
                        }

                        Console.WriteLine($"DXGIFrameMonitoringService: PID {processId} not found in {memName}");
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        continue;
                    }
                }

                Console.WriteLine("DXGIFrameMonitoringService: RTSS shared memory not available for stats");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DXGIFrameMonitoringService: Error reading RTSS stats: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Monitors FPS using GPU performance counter (when available).
        /// </summary>
        private async Task MonitorWithPerformanceCounterAsync(
            PerformanceCounter counter,
            string counterName,
            CancellationToken cancellationToken)
        {
            var lastValue = 0f;
            var stableReadings = 0;
            var initialZeroCount = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var value = counter.NextValue();

                    // Handle initial zero readings from GPU counters
                    if (value == 0)
                    {
                        initialZeroCount++;
                        if (initialZeroCount < 10) // Allow up to 10 initial zeros
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                            continue;
                        }
                    }
                    else
                    {
                        initialZeroCount = 0; // Reset zero count when we get a non-zero value
                    }

                    // GPU counters sometimes return 0 initially, wait for stable non-zero readings
                    if (value > 0 && (Math.Abs(value - lastValue) > 0.1 || stableReadings == 0))
                    {
                        stableReadings++;
                        if (stableReadings > 2) // Wait for 3 stable readings
                        {
                            double fps;
                            Console.WriteLine($"DXGIFrameMonitoringService: Processing counter '{counterName}' with value {value:F1}");
                            Console.WriteLine($"DXGIFrameMonitoringService: Counter name length: {counterName.Length}");
                            Console.WriteLine($"DXGIFrameMonitoringService: Contains 'utilization': {counterName.Contains("utilization")}");
                            Console.WriteLine($"DXGIFrameMonitoringService: Contains 'percentage': {counterName.Contains("percentage")}");
                            Console.WriteLine($"DXGIFrameMonitoringService: Contains 'busy': {counterName.Contains("busy")}");
                            bool isUtilizationCounter = counterName.ToLower().Contains("utilization") || counterName.ToLower().Contains("busy") || counterName.ToLower().Contains("percentage");
                            Console.WriteLine($"DXGIFrameMonitoringService: Is utilization counter: {isUtilizationCounter}");
                            if (isUtilizationCounter)
                            {
                                // Estimate FPS based on GPU utilization with different strategies for different ranges
                                if (value > 70)
                                {
                                    // High utilization: GPU-bound games (like BF6) - use multiplier
                                    fps = Math.Min(300, Math.Max(60, value * 2.8));
                                    Console.WriteLine($"DXGIFrameMonitoringService: High GPU utilization {value:F1}%, estimated FPS={fps:F1} (GPU-bound game)");
                                }
                                else if (value > 30)
                                {
                                    // Medium utilization: Moderate GPU usage - use moderate multiplier
                                    fps = Math.Min(180, Math.Max(60, value * 2.2));
                                    Console.WriteLine($"DXGIFrameMonitoringService: Medium GPU utilization {value:F1}%, estimated FPS={fps:F1} (moderate GPU usage)");
                                }
                                else
                                {
                                    // Low utilization: Likely V-sync locked or CPU-bound - assume common refresh rates
                                    // Check if value suggests a specific refresh rate pattern
                                    if (value < 15)
                                    {
                                        fps = 60; // Assume 60 FPS for very low utilization
                                        Console.WriteLine($"DXGIFrameMonitoringService: Low GPU utilization {value:F1}%, assuming 60 FPS (V-sync or CPU-bound)");
                                    }
                                    else
                                    {
                                        fps = 120; // Assume 120 FPS for moderate low utilization
                                        Console.WriteLine($"DXGIFrameMonitoringService: Low GPU utilization {value:F1}%, assuming 120 FPS (V-sync locked)");
                                    }
                                }
                            }
                            else
                            {
                                // Direct FPS counter
                                fps = value;
                                Console.WriteLine($"DXGIFrameMonitoringService: FPS={fps:F1}, FrameTime={1000.0/fps:F2}ms");
                            }

                            _currentFps = fps;
                            _averageFrameTime = 1000.0 / _currentFps;
                            // GPU counters don't provide statistics, use 0 for min/avg/max
                            FpsUpdated?.Invoke(_currentFps, _averageFrameTime, 0, 0, 0);
                        }
                        lastValue = value;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DXGIFrameMonitoringService: Error reading performance counter: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Monitors FPS using timing estimation when GPU counters are not available.
        /// </summary>
        private async Task MonitorWithTimingEstimationAsync(uint processId, CancellationToken cancellationToken)
        {
            var stopwatch = new Stopwatch();
            var frameCount = 0;
            var lastUpdate = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Simple timing-based estimation - not very accurate but better than nothing
                    if (frameCount == 0)
                    {
                        stopwatch.Restart();
                    }

                    frameCount++;

                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    if (elapsed >= 1.0) // Update every second
                    {
                        var fps = frameCount / elapsed;
                        _currentFps = Math.Min(300, Math.Max(30, fps)); // Clamp to reasonable range
                        _averageFrameTime = 1000.0 / _currentFps;

                        Console.WriteLine($"DXGIFrameMonitoringService: Estimated FPS={_currentFps:F1} (timing-based, less accurate)");

                        // Timing estimation doesn't provide statistics, use 0 for min/avg/max
                        FpsUpdated?.Invoke(_currentFps, _averageFrameTime, 0, 0, 0);

                        // Reset for next measurement
                        frameCount = 0;
                        lastUpdate = DateTime.UtcNow;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken).ConfigureAwait(false); // ~60 FPS timing
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DXGIFrameMonitoringService: Error in timing estimation: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Gets the process ID that RTSS is currently monitoring (has active FPS data).
        /// Returns 0 if no process is being monitored.
        /// </summary>
        public uint GetRTSSMonitoredProcessId()
        {
            try
            {
                Console.WriteLine("DXGIFrameMonitoringService: Checking for RTSS monitored processes...");
                string[] memoryNames = { "RTSSSharedMemoryV3", "RTSSSharedMemoryV2", "RTSSSharedMemoryV1" };
                const uint RTSS_SIGNATURE = 0x52545353; // "RTSS"

                foreach (var memName in memoryNames)
                {
                    try
                    {
                        Console.WriteLine($"DXGIFrameMonitoringService: Trying to open {memName}...");
                        using var memoryMappedFile = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(memName);
                        using var accessor = memoryMappedFile.CreateViewAccessor();

                        uint signature = accessor.ReadUInt32(0);
                        Console.WriteLine($"DXGIFrameMonitoringService: {memName} signature: 0x{signature:X8}");
                        if (signature != RTSS_SIGNATURE) continue;

                        uint appEntrySize = accessor.ReadUInt32(8);
                        uint appArrOffset = accessor.ReadUInt32(12);
                        uint appArrSize = accessor.ReadUInt32(16);
                        long appEntriesStart = (appArrOffset > 0 && appArrOffset < accessor.Capacity) ? appArrOffset : 256;

                        Console.WriteLine($"DXGIFrameMonitoringService: {memName} appEntrySize={appEntrySize}, appArrOffset={appArrOffset}, appArrSize={appArrSize}");

                        int maxEntries = 0;
                        if (appEntrySize > 0)
                        {
                            maxEntries = (int)Math.Min(appArrSize, (accessor.Capacity - appEntriesStart) / appEntrySize);
                        }

                        Console.WriteLine($"DXGIFrameMonitoringService: {memName} scanning {maxEntries} entries...");

                        const int OFF_PID = 0;
                        const int OFF_FRAMES = 276;

                        uint candidatePidWithZeroFps = 0;
                        uint candidatePidTimestamp = 0;

                        // First pass: Look for active entries with valid FPS data
                        for (int i = 0; i < maxEntries; i++)
                        {
                            long entryOffset = appEntriesStart + (i * appEntrySize);
                            if (entryOffset + OFF_FRAMES + 4 > accessor.Capacity) break;

                            uint entryPid = accessor.ReadUInt32(entryOffset + OFF_PID);
                            if (entryPid == 0) continue;

                            uint framesValue = accessor.ReadUInt32(entryOffset + OFF_FRAMES);

                            Console.WriteLine($"DXGIFrameMonitoringService: Entry {i} - PID: {entryPid}, FPS: {framesValue}");

                            // Check if this PID has active FPS data (>0 and reasonable range)
                            if (framesValue > 0 && framesValue < 1000)
                            {
                                // Verify the process actually exists before returning it
                                try
                                {
                                    var process = System.Diagnostics.Process.GetProcessById((int)entryPid);
                                    Console.WriteLine($"DXGIFrameMonitoringService: Found RTSS monitored PID: {entryPid} (FPS: {framesValue})");
                                    return entryPid;
                                }
                                catch (ArgumentException)
                                {
                                    Console.WriteLine($"DXGIFrameMonitoringService: RTSS has stale entry for PID {entryPid} (FPS: {framesValue}) - process no longer exists");
                                    continue; // Process doesn't exist, skip this entry
                                }
                            }

                            // Store the most recent PID with FPS: 0 as a candidate for early hook detection
                            if (framesValue == 0)
                            {
                                // Try to get process timestamp to find the most recently started process
                                try
                                {
                                    var process = System.Diagnostics.Process.GetProcessById((int)entryPid);
                                    var startTime = process.StartTime;
                                    uint timestamp = (uint)((DateTimeOffset)startTime).ToUnixTimeSeconds();
                                    uint currentTime = (uint)((DateTimeOffset)DateTime.Now).ToUnixTimeSeconds();
                                    
                                    // Only consider processes started in the last 2 minutes for early detection
                                    uint maxAgeSeconds = 120; // 2 minutes
                                    if (currentTime - timestamp > maxAgeSeconds)
                                    {
                                        Console.WriteLine($"DXGIFrameMonitoringService: Skipping old candidate PID {entryPid} (started: {startTime}, age: {currentTime - timestamp}s)");
                                        continue; // Skip old processes
                                    }
                                    
                                    if (candidatePidWithZeroFps == 0 || timestamp > candidatePidTimestamp)
                                    {
                                        candidatePidWithZeroFps = entryPid;
                                        candidatePidTimestamp = timestamp;
                                        Console.WriteLine($"DXGIFrameMonitoringService: Updated candidate PID {entryPid} (started: {startTime})");
                                    }
                                }
                                catch 
                                {
                                    // Process might have exited, skip it
                                }
                            }
                        }

                        // If no active monitoring found but we have a recent candidate, use it for early detection
                        if (candidatePidWithZeroFps != 0)
                        {
                            Console.WriteLine($"DXGIFrameMonitoringService: No active FPS data found, but returning candidate PID {candidatePidWithZeroFps} for early hook detection");
                            return candidatePidWithZeroFps;
                        }

                        Console.WriteLine($"DXGIFrameMonitoringService: {memName} - no active entries found");
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        Console.WriteLine($"DXGIFrameMonitoringService: {memName} not found");
                        continue;
                    }
                }
                Console.WriteLine("DXGIFrameMonitoringService: No RTSS shared memory found or no active monitoring");
                return 0; // No active monitoring found
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DXGIFrameMonitoringService: Error checking RTSS monitored PID: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Reads FPS, FrameTime, and statistics from RTSS shared memory.
        /// Returns tuple with (fps, frameTimeMs, minFps, avgFps, maxFps) or null if not available.
        /// </summary>
        private (double fps, double frameTimeMs, double minFps, double avgFps, double maxFps)? TryReadRTSSData(uint processId)
        {
            try
            {
                string[] memoryNames = { "RTSSSharedMemoryV2", "RTSSSharedMemoryV1" };
                const uint RTSS_SIGNATURE = 0x52545353; // "RTSS"

                foreach (var memName in memoryNames)
                {
                    try
                    {
                        using var memoryMappedFile = System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting(memName);
                        using var accessor = memoryMappedFile.CreateViewAccessor();

                        uint signature = accessor.ReadUInt32(0);
                        if (signature != RTSS_SIGNATURE) continue;

                        uint appEntrySize = accessor.ReadUInt32(8);
                        uint appArrOffset = accessor.ReadUInt32(12);
                        uint appArrSize = accessor.ReadUInt32(16);
                        long appEntriesStart = (appArrOffset > 0 && appArrOffset < accessor.Capacity) ? appArrOffset : 256;
                        
                        int maxEntries = 0;
                        if (appEntrySize > 0)
                        {
                            maxEntries = (int)Math.Min(appArrSize, (accessor.Capacity - appEntriesStart) / appEntrySize);
                        }

                        // Offsets per RTSS shared memory documentation
                        const int OFF_PID = 0;
                        const int OFF_TIME0 = 268;       // Period start time in milliseconds
                        const int OFF_TIME1 = 272;       // Period end time in milliseconds
                        const int OFF_FRAMES = 276;      // Frame count in period
                        const int OFF_FRAMETIME = 280;   // Instantaneous frame time in microseconds
                        const int OFF_STAT_FLAGS = 284;  // Statistics flags (validation)
                        const int OFF_STAT_TIME0 = 288;  // Stats period start
                        const int OFF_STAT_TIME1 = 292;  // Stats period end
                        const int OFF_STAT_FRAMES = 296; // Total frames in stats period
                        const int OFF_STAT_COUNT = 300;  // Number of measurements
                        const int OFF_STAT_MIN = 304;    // Min FPS (1% low) in millihertz
                        const int OFF_STAT_AVG = 308;    // Average FPS in millihertz
                        const int OFF_STAT_MAX = 312;    // Max FPS in millihertz

                        for (int i = 0; i < maxEntries; i++)
                        {
                            long entryOffset = appEntriesStart + (i * appEntrySize);
                            if (entryOffset + OFF_STAT_MAX + 4 > accessor.Capacity) break;

                            uint entryPid = accessor.ReadUInt32(entryOffset + OFF_PID);
                            if (entryPid == 0 || entryPid != processId) continue;
                            
                            // Use period-based FPS for smooth display (matches RTSS display)
                            // Formula per documentation: 1000.0 * dwFrames / (dwTime1 - dwTime0)
                            uint time0 = accessor.ReadUInt32(entryOffset + OFF_TIME0);
                            uint time1 = accessor.ReadUInt32(entryOffset + OFF_TIME1);
                            uint frameCount = accessor.ReadUInt32(entryOffset + OFF_FRAMES);
                            uint frameTimeMicroseconds = accessor.ReadUInt32(entryOffset + OFF_FRAMETIME);
                            
                            // Calculate period-based FPS (averaged over time window)
                            uint timeDeltaMs = time1 - time0;
                            if (timeDeltaMs > 0 && frameCount > 0 && timeDeltaMs < 10000) // Max 10 second window
                            {
                                double periodFps = (1000.0 * frameCount) / timeDeltaMs;
                                
                                // Calculate frame time from period FPS for consistency
                                double frameTimeMs = 1000.0 / periodFps;
                                
                                // Validate FPS is in reasonable range
                                if (periodFps >= 1.0 && periodFps < 1000.0)
                                {
                                    _lastValidFps = periodFps; // Store for persistence

                                    // Read and validate RTSS built-in statistics
                                    uint statFlags = accessor.ReadUInt32(entryOffset + OFF_STAT_FLAGS);
                                    uint statCount = accessor.ReadUInt32(entryOffset + OFF_STAT_COUNT);
                                    
                                    double minFps = 0;
                                    double avgFps = 0;
                                    double maxFps = 0;
                                    
                                    // Only read statistics if they're valid (flags set and count > 0)
                                    if (statFlags != 0 && statCount > 0)
                                    {
                                        uint statMinMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_MIN);
                                        uint statAvgMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_AVG);
                                        uint statMaxMillihertz = accessor.ReadUInt32(entryOffset + OFF_STAT_MAX);
                                        
                                        // Validate statistics aren't 0xFFFFFFFF (uninitialized)
                                        if (statMinMillihertz != 0xFFFFFFFF && statMinMillihertz > 0)
                                        {
                                            minFps = statMinMillihertz / 1000.0;
                                        }
                                        if (statAvgMillihertz != 0xFFFFFFFF && statAvgMillihertz > 0)
                                        {
                                            avgFps = statAvgMillihertz / 1000.0;
                                        }
                                        if (statMaxMillihertz != 0xFFFFFFFF && statMaxMillihertz > 0)
                                        {
                                            maxFps = statMaxMillihertz / 1000.0;
                                        }
                                    }
                                    
                                    Console.WriteLine($"DXGIFrameMonitoringService: RTSS PID {processId} - PeriodFPS={periodFps:F1}, FrameTime={frameTimeMs:F2}ms, StatFlags={statFlags}, StatCount={statCount}, Min={minFps:F1}, Avg={avgFps:F1}, Max={maxFps:F1}");
                                    return (periodFps, frameTimeMs, minFps, avgFps, maxFps);
                                }
                                else
                                {
                                    // Period FPS out of range, but we have last valid value
                                    if (_lastValidFps > 0)
                                    {
                                        Console.WriteLine($"DXGIFrameMonitoringService: RTSS PID {processId} - Invalid PeriodFPS={periodFps:F1}, holding last valid FPS={_lastValidFps:F1}");
                                        return (_lastValidFps, 1000.0 / _lastValidFps, 0, 0, 0);
                                    }
                                }
                            }
                            else
                            {
                                // Invalid timing data
                                if (_lastValidFps > 0)
                                {
                                    Console.WriteLine($"DXGIFrameMonitoringService: RTSS PID {processId} - Invalid timing data (delta={timeDeltaMs}ms, frames={frameCount}), holding last valid FPS={_lastValidFps:F1}");
                                    return (_lastValidFps, 1000.0 / _lastValidFps, 0, 0, 0);
                                }
                            }
                        }
                    }
                    catch (System.IO.FileNotFoundException) { continue; }
                }
                return null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Monitors FPS using RTSS shared memory (most accurate).
        /// Continuously retries until RTSS hooks the game or timeout.
        /// </summary>
        private async Task MonitorWithRTSSAsync(uint processId, CancellationToken cancellationToken)
        {
            bool rtssDetected = false;
            int retryCount = 0;
            int consecutiveFailures = 0;
            const int maxRetries = 60; // Try for up to 60 seconds before giving up
            const int maxConsecutiveFailures = 10; // If RTSS disappears for 10 seconds, exit
            
            var processInfo = GetDetailedProcessInfo(processId);
            _fileLogger?.LogInfo($"=== RTSS DETECTION PHASE ===");
            _fileLogger?.LogInfo($"Searching for RTSS hook on: {processInfo}");
            _fileLogger?.LogInfo($"Will check RTSS shared memory versions: V2, V1");
            _fileLogger?.LogInfo($"Max retries: {maxRetries} seconds");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if process still exists
                    if (!IsProcessRunning(processId))
                    {
                        _fileLogger?.LogWarning($"Process no longer exists: {processInfo}");
                        Console.WriteLine($"[WARNING] Process no longer exists: PID {processId}");
                        return;
                    }

                    var data = TryReadRTSSData(processId);
                    if (data.HasValue)
                    {
                        if (!rtssDetected)
                        {
                            var hookMsg = $"✅ RTSS HOOK DETECTED after {retryCount} seconds!";
                            _fileLogger?.LogInfo(hookMsg);
                            _fileLogger?.LogInfo($"Hooked Process: {GetDetailedProcessInfo(processId)}");
                            _fileLogger?.LogInfo($"Initial FPS Reading: {data.Value.fps:F1} FPS, Frame Time: {data.Value.frameTimeMs:F2}ms");
                            Console.WriteLine($"[SUCCESS] {hookMsg}");
                            rtssDetected = true;
                            
                            // Get window title for the successfully hooked process
                            string windowTitle = "[No Window]";
                            try
                            {
                                using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                                _fileLogger?.LogInfo($"🔍 RTSS Hook Debug: Process Name='{process.ProcessName}', MainWindowHandle={process.MainWindowHandle != IntPtr.Zero}");
                                windowTitle = GetProcessWindowTitle(process);
                                _fileLogger?.LogInfo($"🔍 RTSS Hook Debug: GetProcessWindowTitle returned='{windowTitle}'");
                            }
                            catch (Exception ex) 
                            { 
                                _fileLogger?.LogInfo($"⚠️ RTSS Hook Debug: Exception getting window title: {ex.Message}");
                            }
                            
                            // Fire event to update main application state with RTSS-confirmed window title
                            RTSSHooked?.Invoke(processId, windowTitle);
                            
                            // Start continuous monitoring phase
                            _fileLogger?.LogInfo($"=== CONTINUOUS MONITORING PHASE ===");
                        }
                        
                        consecutiveFailures = 0; // Reset failure counter on success
                        _currentFps = data.Value.fps;
                        _averageFrameTime = data.Value.frameTimeMs;
                        _minFps = data.Value.minFps;
                        _avgFps = data.Value.avgFps;
                        _maxFps = data.Value.maxFps;
                        
                        // Log detailed statistics every 10 seconds
                        var now = DateTime.Now;
                        if (rtssDetected && (now - _lastDetailedLogTime).TotalSeconds >= 10)
                        {
                            _fileLogger?.LogInfo($"📊 FPS Statistics (10s update):");
                            _fileLogger?.LogInfo($"   Process: {GetDetailedProcessInfo(processId)}");
                            _fileLogger?.LogInfo($"   Current FPS: {_currentFps:F1}, Frame Time: {_averageFrameTime:F2}ms");
                            _fileLogger?.LogInfo($"   Statistics: Min={_minFps:F1}, Avg={_avgFps:F1}, Max={_maxFps:F1}");
                            _lastDetailedLogTime = now;
                        }
                        
                        FpsUpdated?.Invoke(_currentFps, _averageFrameTime, _minFps, _avgFps, _maxFps);
                    }
                    else
                    {
                        if (!rtssDetected)
                        {
                            // Still waiting for initial RTSS hook
                            retryCount++;
                            if (retryCount >= maxRetries)
                            {
                                var timeoutMsg = $"❌ RTSS hook timeout after {maxRetries} seconds for: {GetDetailedProcessInfo(processId)}";
                                _fileLogger?.LogError(timeoutMsg);
                                Console.WriteLine($"[ERROR] {timeoutMsg}");
                                break;
                            }
                            if (retryCount == 1 || retryCount % 10 == 0)
                            {
                                var waitMsg = $"⏳ Still waiting for RTSS hook... ({retryCount}/{maxRetries}s) - Target: {GetDetailedProcessInfo(processId)}";
                                _fileLogger?.LogInfo(waitMsg);
                                Console.WriteLine($"[INFO] {waitMsg}");
                            }
                        }
                        else
                        {
                            // RTSS was working but now unavailable
                            consecutiveFailures++;
                            if (consecutiveFailures >= maxConsecutiveFailures)
                            {
                                _fileLogger?.LogWarning($"RTSS data lost for {maxConsecutiveFailures} seconds, exiting monitoring");
                                Console.WriteLine($"DXGIFrameMonitoringService: RTSS data lost for {maxConsecutiveFailures} seconds, exiting");
                                break;
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DXGIFrameMonitoringService: Error in RTSS monitoring: {ex.Message}");
                    await Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Disposes of resources used by the service.
        /// </summary>
        public void Dispose()
        {
            StopMonitoringAsync().Wait();
        }
    }
}