using InfoPanel.RTSS.Constants;
using InfoPanel.RTSS.Interfaces;
using InfoPanel.RTSS.Models;
using InfoPanel.RTSS.Services;
using InfoPanel.Plugins;
using System.Diagnostics;
using System.IO;
using System.Linq;

/*
 * File: InfoPanel.RTSS.cs
 * Plugin: InfoPanel.RTSS
 * Version: 1.0.0
 * Author: F3NN3X
 * Description: An optimized InfoPanel plugin using RTSS shared memory to monitor game performance. Reads FPS directly from RivaTuner Statistics Server for pixel-perfect accuracy and anti-cheat compatibility. Tracks FPS, frame time, 1% low FPS over time, window title, display resolution, refresh rate, and GPU name in the UI. Features thread-safe sensor updates and universal game support without hardcoded logic.
 * Changelog (Recent):
 *   - v1.0.0 (October 19, 2025): Initial release of InfoPanel.RTSS - Complete rebranding and architecture from InfoPanel.FPS.
 *     - Rebranded from InfoPanel.FPS to InfoPanel.RTSS to reflect RTSS-focused approach.
 *     - Direct FPS reading from RTSS Frames field (offset 276) for pixel-perfect accuracy.
 *     - Anti-cheat compatible implementation using passive shared memory reading.
 *     - Service-based architecture with dedicated monitoring services.
 *     - Thread-safe sensor updates with lock synchronization.
 *     - Universal game support through RTSS without hardcoded game logic.
 *     - Enhanced title caching with PID-based filtering and alt-tab support.
 *     - Clean codebase with comprehensive error handling and logging.
 * Note: Full history in CHANGELOG.md. A benign log error ("Array is variable sized and does not follow prefix convention") may appear but does not impact functionality.
 */

namespace InfoPanel.RTSS
{
    /// <summary>
    /// Main plugin class that coordinates between services to monitor fullscreen application performance.
    /// Uses dependency injection pattern with dedicated services for better separation of concerns.
    /// </summary>
    public class RTSSPlugin : BasePlugin, IDisposable
    {
        private readonly IPerformanceMonitoringService _performanceService;
        private readonly StableFullscreenDetectionService _fullscreenDetectionService;
        private readonly ISystemInformationService _systemInfoService;
        private readonly ISensorManagementService _sensorService;
        private readonly FileLoggingService _fileLogger;

        private readonly MonitoringState _currentState = new();
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _disposed;

        /// <summary>
        /// Initializes the plugin with its metadata and creates service instances.
        /// </summary>
        public RTSSPlugin()
            : base(
                "rtss-plugin",
                "InfoPanel.RTSS",
                "Simple FPS plugin showing FPS, frame time, 1% low FPS, window title, resolution, and refresh rate using RTSS"
            )
        {
            // Initialize file logging first for debugging
            _fileLogger = new FileLoggingService();
            _fileLogger.LogInfo("=== InfoPanel.RTSS Constructor Called ===");
            
            // Critical: Add early logging for debugging
            System.Diagnostics.Debug.WriteLine("=== InfoPanel.RTSS Constructor Called ===");
            Console.WriteLine("=== InfoPanel.RTSS Constructor Called ===");

            try
            {
                // Initialize services - in a real DI scenario, these would be injected
                _performanceService = new PerformanceMonitoringService(_fileLogger);
                _fullscreenDetectionService = new StableFullscreenDetectionService(_fileLogger);
                _systemInfoService = new SystemInformationService();
                _sensorService = new SensorManagementService();

                _fileLogger.LogInfo("All services initialized successfully");

                // Subscribe to service events
                _performanceService.MetricsUpdated += OnPerformanceMetricsUpdated;
                _performanceService.DXGIService.RTSSHooked += OnRTSSHooked;

                _fileLogger.LogInfo("Event subscriptions completed");
                Console.WriteLine("RTSS Plugin initialized with all services");
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Failed to initialize plugin services", ex);
                throw;
            }
        }

        /// <summary>
        /// No configuration file is used.
        /// </summary>
        public override string? ConfigFilePath => null;

        /// <summary>
        /// Updates occur every 1 second for stable UI refreshes.
        /// </summary>
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(MonitoringConstants.UiUpdateIntervalSeconds);

        /// <summary>
        /// Initializes the plugin and starts monitoring services.
        /// </summary>
        public override void Initialize()
        {
            try
            {
                _fileLogger.LogInfo("=== RTSS Plugin Initialize() called ===");
                Console.WriteLine("=== RTSS Plugin Initialize() called ===");

                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize system information
                _currentState.System = _systemInfoService.GetSystemInformation();
                _fileLogger.LogSystemInfo("Information", $"GPU: {_currentState.System.GpuName}, Display: {_currentState.System.Resolution}@{_currentState.System.RefreshRate}Hz");

                // Start continuous monitoring loop (like the original implementation)
                _ = Task.Run(async () => await StartContinuousMonitoringAsync(_cancellationTokenSource.Token).ConfigureAwait(false));
                _fileLogger.LogInfo("Continuous monitoring task started");

                // Perform initial window check
                _ = Task.Run(async () => await PerformInitialWindowCheckAsync().ConfigureAwait(false));
                _fileLogger.LogInfo("Initial window check task started");

                _fileLogger.LogInfo("RTSS Plugin initialization completed successfully");
                Console.WriteLine("RTSS Plugin initialization completed");
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Error during plugin initialization", ex);
                Console.WriteLine($"Error during plugin initialization: {ex}");
            }
        }

        /// <summary>
        /// Registers sensors with InfoPanel's UI container.
        /// </summary>
        public override void Load(List<IPluginContainer> containers)
        {
            try
            {
                _fileLogger.LogInfo("=== RTSS Plugin Load() called ===");
                Console.WriteLine("=== RTSS Plugin Load() called ===");

                _sensorService.CreateAndRegisterSensors(containers);
                _fileLogger.LogInfo("Sensors created and registered with InfoPanel");
                
                // Update sensors with initial system information
                _sensorService.UpdateSystemSensors(_currentState.System);
                _fileLogger.LogInfo("System sensors updated with initial information");
                
                Console.WriteLine("Sensors loaded and registered with InfoPanel");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sensors: {ex}");
            }
        }

        /// <summary>
        /// Not implemented; UpdateAsync is used instead.
        /// </summary>
        public override void Update() => throw new NotImplementedException();

        /// <summary>
        /// Periodic update method that updates sensors with current state.
        /// RTSS-first approach: Check what PID RTSS is monitoring, then get window title for that PID.
        /// </summary>
        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // RTSS-FIRST APPROACH: Check what PID RTSS is currently monitoring
                uint rtssMonitoredPid = _performanceService.GetRTSSMonitoredProcessId();
                
                // Traditional window detection (fallback for games RTSS can't hook)
                var currentWindow = await _fullscreenDetectionService.DetectFullscreenProcessAsync().ConfigureAwait(false);
                uint windowPid = currentWindow?.ProcessId ?? 0;
                
                uint targetPid = 0;
                string detectionMethod = "";
                
                // Priority 1: Use RTSS monitored PID if available AND it passes blacklist check
                if (rtssMonitoredPid > 0)
                {
                    // CRITICAL FIX: Check if RTSS monitored process is blacklisted
                    var isRtssProcessBlacklisted = await _fullscreenDetectionService.IsProcessBlacklistedAsync(rtssMonitoredPid).ConfigureAwait(false);
                    
                    if (!isRtssProcessBlacklisted)
                    {
                        targetPid = rtssMonitoredPid;
                        detectionMethod = "RTSS";
                        
                        // Get window title for the RTSS monitored process
                        string rtssWindowTitle = "";
                        try
                        {
                        using var process = System.Diagnostics.Process.GetProcessById((int)rtssMonitoredPid);
                        rtssWindowTitle = GetProcessWindowTitle(process);
                    }
                    catch (ArgumentException)
                    {
                        // Process not found
                        rtssWindowTitle = "";
                    }
                    if (!string.IsNullOrWhiteSpace(rtssWindowTitle))
                    {
                        // Create a WindowInformation object for the RTSS monitored process
                        _currentState.Window = new WindowInformation
                        {
                            ProcessId = rtssMonitoredPid,
                            WindowTitle = rtssWindowTitle,
                            WindowHandle = IntPtr.Zero, // We don't have the handle, but that's OK for RTSS-based detection
                            IsFullscreen = true // Assume fullscreen since RTSS is monitoring it
                        };
                        Console.WriteLine($"UpdateAsync: Using RTSS monitored PID {rtssMonitoredPid} with title '{rtssWindowTitle}'");
                    }
                    else
                    {
                        Console.WriteLine($"UpdateAsync: RTSS monitoring PID {rtssMonitoredPid} but no window title found");
                    }
                    }
                    else
                    {
                        Console.WriteLine($"UpdateAsync: RTSS monitoring blacklisted process {rtssMonitoredPid}, skipping");
                    }
                }
                // Priority 2: Fall back to traditional window detection if RTSS has no active monitoring
                else if (windowPid > 0 && currentWindow != null)
                {
                    targetPid = windowPid;
                    detectionMethod = "Window";
                    _currentState.Window = currentWindow;
                    Console.WriteLine($"UpdateAsync: Using window detection PID {windowPid} with title '{currentWindow.WindowTitle}'");
                }
                
                // Start monitoring if we found a target PID and aren't monitoring it yet
                if (targetPid > 0 && !_performanceService.IsMonitoring)
                {
                    Console.WriteLine($"UpdateAsync detected new app via {detectionMethod} (PID: {targetPid}); starting DXGI monitoring");
                    _currentState.IsMonitoring = true;
                    await StartMonitoringAsync(targetPid).ConfigureAwait(false);
                }
                // Switch monitoring if target PID changed
                else if (targetPid > 0 && _performanceService.IsMonitoring && _currentState.Performance.MonitoredProcessId != targetPid)
                {
                    Console.WriteLine($"UpdateAsync detected PID change via {detectionMethod}: monitoring {_currentState.Performance.MonitoredProcessId} but found {targetPid}; switching monitoring");
                    await StopMonitoringAsync().ConfigureAwait(false);
                    _currentState.IsMonitoring = true;
                    await StartMonitoringAsync(targetPid).ConfigureAwait(false);
                }
                // Stop monitoring if no target found but we're still monitoring something
                else if (targetPid == 0 && _performanceService.IsMonitoring)
                {
                    Console.WriteLine($"UpdateAsync: No target PID found but still monitoring PID {_currentState.Performance.MonitoredProcessId}; stopping monitoring");
                    await StopMonitoringAsync().ConfigureAwait(false);
                }
                // Check if monitored process still exists
                else if (targetPid == 0 && _performanceService.IsMonitoring)
                {
                    uint monitoredPid = _currentState.Performance.MonitoredProcessId;
                    Console.WriteLine($"UpdateAsync: No target found but still monitoring - checking if monitored PID {monitoredPid} still exists");
                    
                    bool monitoredProcessExists = false;
                    
                    // Only check if we have a valid monitored PID
                    if (monitoredPid > 0)
                    {
                        try
                        {
                            using var process = System.Diagnostics.Process.GetProcessById((int)monitoredPid);
                            monitoredProcessExists = !process.HasExited;
                        }
                        catch (ArgumentException)
                        {
                            monitoredProcessExists = false;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"UpdateAsync: Error checking if RTSS monitored process {monitoredPid} exists: {ex}");
                            monitoredProcessExists = false;
                        }
                    }
                    else
                    {
                        // If monitored PID is 0, this indicates a state problem - stop monitoring
                        Console.WriteLine($"UpdateAsync: Monitored PID is 0 but still monitoring - stopping to reset state");
                        monitoredProcessExists = false;
                    }

                    if (monitoredProcessExists)
                    {
                        Console.WriteLine($"UpdateAsync: RTSS monitored process {monitoredPid} still exists (backgrounded/alt-tabbed), continuing monitoring");
                        // Process still exists - keep monitoring even though not fullscreen
                        // Don't update _currentState.Window since we don't have new window data
                    }
                    else
                    {
                        Console.WriteLine($"UpdateAsync: RTSS monitored process {monitoredPid} no longer exists, stopping monitoring");
                        await StopMonitoringAsync().ConfigureAwait(false);
                    }
                }
                // Check if monitored process still exists (additional safety check)
                else if (_performanceService.IsMonitoring && _currentState.Window.ProcessId != 0)
                {
                    bool processExists = false;
                    try
                    {
                        using var process = System.Diagnostics.Process.GetProcessById((int)_currentState.Window.ProcessId);
                        processExists = !process.HasExited;
                    }
                    catch (ArgumentException)
                    {
                        processExists = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"UpdateAsync: Error checking process {_currentState.Window.ProcessId}: {ex}");
                        processExists = false;
                    }

                    if (!processExists)
                    {
                        Console.WriteLine($"UpdateAsync: Monitored process {_currentState.Window.ProcessId} no longer exists; stopping monitoring");
                        await StopMonitoringAsync().ConfigureAwait(false);
                    }
                }

                // Ensure performance data is cleared if not monitoring
                if (!_performanceService.IsMonitoring)
                {
                    _currentState.Performance = new PerformanceMetrics(); // Force reset to 0s
                    _currentState.Window = new WindowInformation { WindowTitle = "Nothing to capture" };
                }

                // Update sensors with current state
                _sensorService.UpdateSensors(_currentState);

                _currentState.LastUpdate = DateTime.Now;

                // Log current state for debugging
                Console.WriteLine($"UpdateAsync - Monitoring: {_performanceService.IsMonitoring}, " +
                                $"Window PID: {_currentState.Window.ProcessId}, " +
                                $"Target PID: {targetPid}, " +
                                $"FPS: {_currentState.Performance.Fps:F1}, " +
                                $"Title: {_currentState.Window.WindowTitle}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateAsync: {ex}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Determines if a process is a system/non-gaming process that should be ignored.
        /// </summary>
        private bool IsSystemProcess(string processName)
        {
            var systemProcesses = new[]
            {
                "explorer", "dwm", "winlogon", "csrss", "services", "svchost", "lsass", "smss", "wininit",
                "taskmgr", "taskhostw", "rundll32", "dllhost", "conhost", "fontdrvhost", "WUDFHost",
                "spoolsv", "RuntimeBroker", "SearchIndexer", "audiodg", "SecurityHealthSystray",
                "Adobe Desktop Service", "TextInputHost", "WaveLink", "Photoshop", "Files", "InfoPanel",
                "Code", "notepad", "calc", "cmd", "powershell", "pwsh", "WindowsTerminal", "devenv"
            };
            
            return systemProcesses.Any(sp => processName.Contains(sp, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Closes the plugin by disposing resources.
        /// </summary>
        public override void Close() => Dispose();

        /// <summary>
        /// Performs initial window check asynchronously without blocking Initialize.
        /// </summary>
        private async Task PerformInitialWindowCheckAsync()
        {
            try
            {
                await Task.Delay(500).ConfigureAwait(false); // Small delay for stabilization
                
                var currentWindow = await _fullscreenDetectionService.DetectFullscreenProcessAsync().ConfigureAwait(false);
                if (currentWindow != null)
                {
                    _currentState.Window = currentWindow;
                    await StartMonitoringAsync(currentWindow.ProcessId).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in initial window check: {ex}");
            }
        }

        /// <summary>
        /// Continuously monitors fullscreen apps and only logs when RTSS successfully hooks.
        /// Simplified approach: only monitor when RTSS successfully hooks processes.
        /// </summary>
        private async Task StartContinuousMonitoringAsync(CancellationToken cancellationToken)
        {
            _fileLogger?.LogInfo("=== SIMPLIFIED CONTINUOUS MONITORING STARTED ===");
            
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Use stable fullscreen detection service
                    var currentWindow = await _fullscreenDetectionService.DetectFullscreenProcessAsync().ConfigureAwait(false);
                    uint pid = currentWindow?.ProcessId ?? 0;

                    if (pid > 0 && !_performanceService.IsMonitoring)
                    {
                        // Found a fullscreen app - attempt RTSS monitoring
                        _fileLogger?.LogInfo($"📱 New fullscreen app detected: PID {pid}");
                        _currentState.Window = currentWindow!;
                        _currentState.System = _systemInfoService.GetSystemInformation();
                        _currentState.IsMonitoring = true;
                        _currentState.Performance = new PerformanceMetrics();
                        
                        await StartMonitoringAsync(pid).ConfigureAwait(false);
                    }
                    else if (pid == 0 && _performanceService.IsMonitoring)
                    {
                        // No fullscreen app detected - but check if current monitored process still exists
                        uint currentMonitoredPid = _currentState.Performance.MonitoredProcessId;
                        bool currentProcessStillExists = false;
                        
                        if (currentMonitoredPid > 0)
                        {
                            try
                            {
                                using var process = System.Diagnostics.Process.GetProcessById((int)currentMonitoredPid);
                                currentProcessStillExists = !process.HasExited;
                            }
                            catch { currentProcessStillExists = false; }
                        }
                        
                        if (!currentProcessStillExists)
                        {
                            // Current monitored process is dead - stop monitoring
                            _fileLogger?.LogInfo($"❌ Monitored process {currentMonitoredPid} no longer exists, stopping monitoring");
                            await StopMonitoringAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            // Current process still exists, keep monitoring (probably just blacklisted processes detected)
                            _fileLogger?.LogInfo($"⏸️ No new fullscreen apps but PID {currentMonitoredPid} still running, continuing monitoring");
                        }
                    }
                    else if (pid > 0 && _performanceService.IsMonitoring && _currentState.Window.ProcessId != pid)
                    {
                        // Different fullscreen app detected - but check if current monitoring is working first
                        var currentMonitoredPid = _currentState.Performance.MonitoredProcessId;
                        bool currentProcessStillExists = false;
                        
                        if (currentMonitoredPid > 0)
                        {
                            try
                            {
                                using var process = System.Diagnostics.Process.GetProcessById((int)currentMonitoredPid);
                                currentProcessStillExists = !process.HasExited;
                            }
                            catch { currentProcessStillExists = false; }
                        }
                        
                        // Only switch if current process is dead OR if we have a high-priority game process
                        if (!currentProcessStillExists)
                        {
                            _fileLogger?.LogInfo($"🔄 Current process dead, switching monitoring: {_currentState.Window.ProcessId} → {pid}");
                            await StopMonitoringAsync().ConfigureAwait(false);
                            _currentState.Window = currentWindow!;
                            _currentState.Performance = new PerformanceMetrics();
                            await StartMonitoringAsync(pid).ConfigureAwait(false);
                        }
                        else if (IsHigherPriorityProcess(currentWindow?.ProcessId ?? 0, _currentState.Window.ProcessId))
                        {
                            _fileLogger?.LogInfo($"🔄 Higher priority process detected, switching monitoring: {_currentState.Window.ProcessId} → {pid}");
                            await StopMonitoringAsync().ConfigureAwait(false);
                            _currentState.Window = currentWindow!;
                            _currentState.Performance = new PerformanceMetrics();
                            await StartMonitoringAsync(pid).ConfigureAwait(false);
                        }
                        else
                        {
                            _fileLogger?.LogInfo($"⏸️ Different process detected but staying with current: {_currentState.Window.ProcessId} (ignoring {pid})");
                        }
                    }
                    else if (pid > 0 && _performanceService.IsMonitoring && _currentState.Window.ProcessId == pid)
                    {
                        // Same process is still fullscreen and we're already monitoring - update info but preserve title
                        _currentState.System = _systemInfoService.GetSystemInformation();
                        
                        // Update window title if current detection has a better title
                        if (!string.IsNullOrWhiteSpace(currentWindow?.WindowTitle) && 
                            currentWindow.WindowTitle != "[No Window]" &&
                            (string.IsNullOrWhiteSpace(_currentState.Window.WindowTitle) || 
                             _currentState.Window.WindowTitle == "[No Window]"))
                        {
                            _currentState.Window.WindowTitle = currentWindow.WindowTitle;
                            _fileLogger?.LogInfo($"📝 Updated window title: {currentWindow.WindowTitle}");
                        }
                        // Don't restart monitoring!
                    }

                    // Check every 3 seconds (reduced frequency for stability)
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                _fileLogger?.LogInfo("Continuous monitoring cancelled");
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError("Error in continuous monitoring", ex);
            }
            finally
            {
                _fileLogger?.LogInfo("=== CONTINUOUS MONITORING STOPPED ===");
            }
        }

        /// <summary>
        /// Starts performance monitoring for the specified process.
        /// </summary>
        private async Task StartMonitoringAsync(uint processId)
        {
            try
            {
                var processInfo = GetDetailedProcessInfo(processId);
                _fileLogger?.LogInfo($"=== RTSS MONITORING START REQUEST ===");
                _fileLogger?.LogInfo($"Target Process: {processInfo}");
                
                // Performance service has its own guard to prevent duplicate starts
                // Don't stop/reset here as it clears the frame time buffer needed for 1% low calculation
                Console.WriteLine($"StartMonitoringAsync: Starting monitoring for process ID: {processId}");

                // Start performance monitoring (service will skip if already monitoring same PID)
                var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
                
                _fileLogger?.LogInfo($"Starting RTSS monitoring for: {processInfo}");
                Console.WriteLine($"StartMonitoringAsync: Calling _performanceService.StartMonitoringAsync for PID: {processId}");
                await _performanceService.StartMonitoringAsync(processId, cancellationToken).ConfigureAwait(false);

                _fileLogger?.LogInfo($"Performance service monitoring state: {_performanceService.IsMonitoring}");
                Console.WriteLine($"StartMonitoringAsync: Performance service IsMonitoring: {_performanceService.IsMonitoring}");
            }
            catch (Exception ex)
            {
                var processInfo = GetDetailedProcessInfo(processId);
                _fileLogger?.LogError($"Error starting monitoring for: {processInfo}", ex);
                Console.WriteLine($"StartMonitoringAsync: Error starting monitoring for PID {processId}: {ex}");
                _currentState.IsMonitoring = false;
            }
        }

        /// <summary>
        /// Stops performance monitoring and resets state.
        /// </summary>
        private async Task StopMonitoringAsync()
        {
            try
            {
                Console.WriteLine("Stopping performance monitoring");

                _performanceService.StopMonitoring();
                _currentState.IsMonitoring = false;
                
                // Reset state - clear window info and performance
                _currentState.Window = new WindowInformation { WindowTitle = "-" };
                _currentState.Performance = new PerformanceMetrics(); // Reset to 0s

                // Update sensors immediately to show reset state
                _sensorService.UpdateSensors(_currentState);
                _fileLogger?.LogInfo("🔄 Sensors reset to zero values");

                Console.WriteLine("Performance monitoring stopped and state reset");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping monitoring: {ex}");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles performance metrics updates from the monitoring service.
        /// </summary>
        private void OnPerformanceMetricsUpdated(PerformanceMetrics metrics)
        {
            try
            {
                _currentState.Performance = metrics;
                _sensorService.UpdatePerformanceSensors(metrics);
                
                Console.WriteLine($"Performance metrics updated - FPS: {metrics.Fps:F1}, " +
                                $"Frame Time: {metrics.FrameTime:F2}ms, " +
                                $"1% Low: {metrics.OnePercentLowFps:F1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling performance metrics update: {ex}");
            }
        }

        /// <summary>
        /// Handles RTSS hook success events - updates window title for the hooked process.
        /// This ensures we only show window titles for processes that RTSS is actually monitoring.
        /// </summary>
        private void OnRTSSHooked(uint processId, string windowTitle)
        {
            try
            {
                Console.WriteLine($"[RTSS HOOKED] PID {processId} with title: '{windowTitle}'");
                _fileLogger?.LogInfo($"RTSS successfully hooked PID {processId}, updating window title: '{windowTitle}'");
                
                // Update the current state with the RTSS-confirmed window title
                _currentState.Window = new WindowInformation
                {
                    ProcessId = processId,
                    WindowTitle = windowTitle,
                    WindowHandle = IntPtr.Zero,
                    IsFullscreen = true
                };
                
                // Also ensure Performance.MonitoredProcessId matches for sensor logic
                _currentState.Performance.MonitoredProcessId = processId;
                
                // Debug logging
                Console.WriteLine($"[RTSS HOOKED DEBUG] Window PID: {_currentState.Window.ProcessId}, Performance PID: {_currentState.Performance.MonitoredProcessId}, Title: '{_currentState.Window.WindowTitle}'");
                
                // Force sensor update with the new title
                _sensorService.UpdateSensors(_currentState);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling RTSS hooked event: {ex}");
            }
        }



        /// <summary>
        /// Gets detailed information about a process for logging.
        /// </summary>
        private string GetDetailedProcessInfo(uint pid)
        {
            try
            {
                var process = Process.GetProcessById((int)pid);
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
        /// Gets the window title for a process with fallback methods.
        /// </summary>
        private string GetProcessWindowTitle(Process process)
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
                if (!string.IsNullOrWhiteSpace(processName) && IsLikelyGameProcess(processName))
                {
                    // Give games a brief moment to create their main window
                    for (int retry = 0; retry < 3; retry++)
                    {
                        process.Refresh();
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            var title = process.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(title) && title.Length > 1)
                            {
                                return title;
                            }
                        }
                        if (retry < 2) Thread.Sleep(100); // Brief wait before retry
                    }
                    
                    // Fall back to game process name
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
        /// Determines if a process name looks like a game executable.
        /// </summary>
        private bool IsLikelyGameProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            
            // Common game process patterns
            var gameIndicators = new[] { "game", "bf", "cod", "cs", "apex", "valorant", "fortnite", "minecraft", "steam" };
            var lowerName = processName.ToLowerInvariant();
            
            return gameIndicators.Any(indicator => lowerName.Contains(indicator)) ||
                   processName.Length < 10; // Short names are often games (e.g., "bf6", "cod", "cs2")
        }
        
        /// <summary>
        /// Determines if the new process should take priority over the current one.
        /// </summary>
        private bool IsHigherPriorityProcess(uint newPid, uint currentPid)
        {
            try
            {
                using var newProcess = System.Diagnostics.Process.GetProcessById((int)newPid);
                using var currentProcess = System.Diagnostics.Process.GetProcessById((int)currentPid);
                
                var newProcessName = newProcess.ProcessName.ToLowerInvariant();
                var currentProcessName = currentProcess.ProcessName.ToLowerInvariant();
                
                // Steam overlay should never take priority over actual games
                if (newProcessName.Contains("gameoverlayui") || newProcessName.Contains("steam"))
                    return false;
                
                // If current is Steam overlay, any real game should take priority
                if (currentProcessName.Contains("gameoverlayui") || currentProcessName.Contains("steam"))
                    return true;
                
                // Prefer actual game processes over system processes
                var newIsGame = IsLikelyGameProcess(newProcessName);
                var currentIsGame = IsLikelyGameProcess(currentProcessName);
                
                return newIsGame && !currentIsGame;
            }
            catch
            {
                return false; // If we can't determine, don't switch
            }
        }

        /// <summary>
        /// Gets a shortened executable path for a process.
        /// </summary>
        private string GetProcessExecutablePath(Process process)
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
        /// Disposes resources, both managed and unmanaged.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Unsubscribe from events
                        _performanceService.MetricsUpdated -= OnPerformanceMetricsUpdated;
                        _performanceService.DXGIService.RTSSHooked -= OnRTSSHooked;

                        // Stop and dispose services
                        _performanceService?.Dispose();
                        _fullscreenDetectionService?.Dispose();

                        // Reset sensors
                        _sensorService?.ResetSensors();

                        // Cancel any ongoing operations
                        _cancellationTokenSource?.Cancel();
                        _cancellationTokenSource?.Dispose();

                        // Dispose file logger last
                        _fileLogger?.Dispose();

                        Console.WriteLine("RTSSPlugin disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during plugin disposal: {ex}");
                    }
                }

                _disposed = true;
            }
        }



        /// <summary>
        /// Public entry point for IDisposable.Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer ensures cleanup if Dispose isn't called.
        /// </summary>
        ~RTSSPlugin()
        {
            Dispose(false);
        }
    }
}