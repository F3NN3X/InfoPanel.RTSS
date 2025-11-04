using InfoPanel.RTSS.Constants;
using InfoPanel.RTSS.Interfaces;
using InfoPanel.RTSS.Models;
using InfoPanel.Plugins;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Service responsible for managing InfoPanel sensors and their updates.
    /// Handles creation, registration, and updating of all performance and system sensors.
    /// </summary>
    public class SensorManagementService : ISensorManagementService
    {
        private readonly ConfigurationService? _configService;
        private readonly FileLoggingService? _fileLogger;
        private readonly PluginSensor _fpsSensor;
        private readonly PluginSensor _onePercentLowFpsSensor;
        private readonly PluginSensor _currentFrameTimeSensor;
        
        // Native RTSS Statistics sensors (from dwStat* fields)
        private readonly PluginSensor _minFpsSensor;
        private readonly PluginSensor _avgFpsSensor;
        private readonly PluginSensor _maxFpsSensor;
        
        private readonly PluginText _windowTitleSensor;
        private readonly PluginText _resolutionSensor;
        private readonly PluginSensor _refreshRateSensor;
        private readonly PluginText _gpuNameSensor;
        
        // Enhanced RTSS sensors for advanced metrics
        private readonly PluginText _graphicsApiSensor;
        private readonly PluginText _architectureSensor;
        private readonly PluginText _gameCategorySensor;
        // Game resolution sensor removed - was confusing in borderless fullscreen mode
        private readonly PluginText _displayModeSensor;
        
        // Auto-Benchmark Mode sensor (v1.2.0 feature)
        private readonly PluginText _benchmarkModeSensor;
        
        /// <summary>
        /// Cached window title to prevent flickering when window validation temporarily fails.
        /// Window.IsValid can become false during normal operation (e.g., when fullscreen state briefly changes,
        /// during alt-tab, or window events) even though monitoring is still active and FPS is being captured.
        /// By caching the last known good window title, we prevent the sensor from showing "Nothing to capture"
        /// during these temporary validation failures.
        /// </summary>
        private string _lastValidWindowTitle = string.Empty;
        
        /// <summary>
        /// Lock object for thread-safe sensor updates to prevent collection modification exceptions.
        /// </summary>
        private readonly object _sensorLock = new object();

        /// <summary>
        /// Initializes a new instance of the SensorManagementService.
        /// </summary>
        /// <param name="configService">Configuration service for accessing debug settings.</param>
        /// <param name="fileLogger">File logging service for debug output.</param>
        public SensorManagementService(ConfigurationService? configService = null, FileLoggingService? fileLogger = null)
        {
            _configService = configService;
            _fileLogger = fileLogger;
            
            // Initialize performance sensors
            _fpsSensor = new PluginSensor(
                SensorConstants.FpsSensorId,
                SensorConstants.FpsSensorDisplayName,
                0,
                SensorConstants.FpsUnit
            );

            _onePercentLowFpsSensor = new PluginSensor(
                SensorConstants.OnePercentLowFpsSensorId,
                SensorConstants.OnePercentLowFpsSensorDisplayName,
                0,
                SensorConstants.FpsUnit
            );

            _currentFrameTimeSensor = new PluginSensor(
                SensorConstants.CurrentFrameTimeSensorId,
                SensorConstants.CurrentFrameTimeSensorDisplayName,
                0,
                SensorConstants.FrameTimeUnit
            );

            // Initialize native RTSS statistics sensors
            _minFpsSensor = new PluginSensor(
                "min-fps",
                "Min FPS",
                0,
                SensorConstants.FpsUnit
            );

            _avgFpsSensor = new PluginSensor(
                "avg-fps", 
                "Average FPS",
                0,
                SensorConstants.FpsUnit
            );

            _maxFpsSensor = new PluginSensor(
                "max-fps",
                "Max FPS", 
                0,
                SensorConstants.FpsUnit
            );

            // Initialize text sensors
            _windowTitleSensor = new PluginText(
                SensorConstants.WindowTitleSensorId,
                SensorConstants.WindowTitleSensorDisplayName,
                _configService?.DefaultCaptureMessage ?? "Nothing to capture"
            );

            _resolutionSensor = new PluginText(
                SensorConstants.ResolutionSensorId,
                SensorConstants.ResolutionSensorDisplayName,
                SensorConstants.DefaultResolution
            );

            _refreshRateSensor = new PluginSensor(
                SensorConstants.RefreshRateSensorId,
                SensorConstants.RefreshRateSensorDisplayName,
                0,
                SensorConstants.RefreshRateUnit
            );

            _gpuNameSensor = new PluginText(
                SensorConstants.GpuNameSensorId,
                SensorConstants.GpuNameSensorDisplayName,
                SensorConstants.DefaultGpuName
            );

            // Initialize enhanced RTSS sensors
            _graphicsApiSensor = new PluginText(
                "graphics-api",
                "Graphics API",
                "Unknown"
            );

            _architectureSensor = new PluginText(
                "architecture",
                "Architecture",
                "Unknown"
            );

            _gameCategorySensor = new PluginText(
                "game-category",
                "Game Category",
                "Unknown"
            );

            // Game resolution sensor removed - was confusing in borderless mode

            _displayModeSensor = new PluginText(
                "display-mode",
                "Display Mode",
                "Unknown"
            );

            _benchmarkModeSensor = new PluginText(
                "benchmark-mode",
                "Benchmark Mode",
                "Initializing..."
            );

            _fileLogger?.LogInfo("Sensor management service initialized with all sensors");
        }

        /// <summary>
        /// Creates and registers all sensors with the provided container.
        /// </summary>
        /// <param name="containers">List of plugin containers to add sensors to.</param>
        public void CreateAndRegisterSensors(List<IPluginContainer> containers)
        {
            var container = new PluginContainer("RTSS");
            
            // Add all sensors to the container
            container.Entries.Add(_fpsSensor);
            container.Entries.Add(_onePercentLowFpsSensor);
            container.Entries.Add(_currentFrameTimeSensor);
            
            // Add native RTSS statistics sensors
            container.Entries.Add(_minFpsSensor);
            container.Entries.Add(_avgFpsSensor);
            container.Entries.Add(_maxFpsSensor);
            
            container.Entries.Add(_windowTitleSensor);
            container.Entries.Add(_resolutionSensor);
            container.Entries.Add(_refreshRateSensor);
            container.Entries.Add(_gpuNameSensor);
            
            // Add enhanced RTSS sensors
            container.Entries.Add(_graphicsApiSensor);
            container.Entries.Add(_architectureSensor);
            container.Entries.Add(_gameCategorySensor);
            // Game resolution sensor removed
            container.Entries.Add(_displayModeSensor);
            
            // Add auto-benchmark mode sensor (v1.2.0)
            container.Entries.Add(_benchmarkModeSensor);

            containers.Add(container);
            
            _fileLogger?.LogInfo($"Registered {container.Entries.Count} sensors in RTSS container");
        }

        /// <summary>
        /// Updates all sensors with the current monitoring state.
        /// </summary>
        /// <param name="state">Current monitoring state containing all metrics.</param>
        public void UpdateSensors(MonitoringState state)
        {
            lock (_sensorLock)
            {
                try
                {
                    // Update performance sensors
                    if (state.Performance.IsValid && state.IsMonitoring)
                {
                    _fpsSensor.Value = state.Performance.Fps;
                    _currentFrameTimeSensor.Value = state.Performance.FrameTime;
                    _onePercentLowFpsSensor.Value = state.Performance.OnePercentLowFps;
                }
                else
                {
                    // Reset performance sensors when not monitoring
                    _fpsSensor.Value = 0;
                    _currentFrameTimeSensor.Value = 0;
                    _onePercentLowFpsSensor.Value = 0;
                    // Add logging for debugging
                    _fileLogger?.LogInfo("SensorManagementService: Reset all FPS sensors to 0");
                }

                // Update window information with caching to prevent flickering
                // ONLY show window title for the process that RTSS is actively monitoring
                if (state.IsMonitoring)
                {
                    // Get the PID that RTSS is actually monitoring (providing FPS data)
                    uint monitoredPid = state.Performance.MonitoredProcessId;
                    
                    // DEBUG: Always log the state to understand what's happening
                    _fileLogger?.LogInfo($"[SENSOR DEBUG] IsMonitoring={state.IsMonitoring}, MonitoredPID={monitoredPid}, WindowPID={state.Window?.ProcessId ?? 0}, WindowTitle='{state.Window?.WindowTitle ?? "null"}'");
                    
                    // Update cached title ONLY if window PID matches RTSS monitored PID
                    if (monitoredPid > 0 && 
                        state.Window != null &&
                        state.Window.ProcessId == monitoredPid && 
                        !string.IsNullOrWhiteSpace(state.Window.WindowTitle))
                    {
                        if (_lastValidWindowTitle != state.Window.WindowTitle)
                        {
                            _fileLogger?.LogInfo($"Window title cached: '{state.Window.WindowTitle}' (PID: {monitoredPid})");
                            _lastValidWindowTitle = state.Window.WindowTitle;
                        }
                    }
                    else
                    {
                        // Debug: Log why caching didn't happen
                        _fileLogger?.LogInfo($"Title NOT cached - MonitoredPID: {monitoredPid}, WindowPID: {state.Window?.ProcessId ?? 0}, Title: '{state.Window?.WindowTitle ?? "null"}', IsWhitespace: {string.IsNullOrWhiteSpace(state.Window?.WindowTitle)}");
                    }
                    
                    // Use cached title if we have one, otherwise show NoCapture
                    _windowTitleSensor.Value = !string.IsNullOrEmpty(_lastValidWindowTitle) 
                        ? _lastValidWindowTitle 
                        : SensorConstants.NoCapture;
                }
                else
                {
                    // When not monitoring, reset cache and show default
                    _lastValidWindowTitle = string.Empty;
                    _windowTitleSensor.Value = _configService?.DefaultCaptureMessage ?? "Nothing to capture";
                }

                // Update system information (always available)
                _resolutionSensor.Value = state.System.Resolution;
                _refreshRateSensor.Value = state.System.RefreshRate;
                _gpuNameSensor.Value = state.System.GpuName;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating sensors", ex);
                }
            }
        }

        /// <summary>
        /// Resets all sensors to their default values.
        /// </summary>
        public void ResetSensors()
        {
            lock (_sensorLock)
            {
                try
                {
                    // Reset performance sensors
                    _fpsSensor.Value = 0;
                    _onePercentLowFpsSensor.Value = 0;
                    _currentFrameTimeSensor.Value = 0;
                    
                    // Reset native RTSS statistics sensors
                    _minFpsSensor.Value = 0;
                    _avgFpsSensor.Value = 0;
                    _maxFpsSensor.Value = 0;

                    // Reset information sensors to defaults
                    _windowTitleSensor.Value = _configService?.DefaultCaptureMessage ?? "Nothing to capture";
                    _resolutionSensor.Value = SensorConstants.DefaultResolution;
                    _refreshRateSensor.Value = 0;
                    _gpuNameSensor.Value = SensorConstants.DefaultGpuName;
                    
                    // Reset enhanced RTSS sensors to defaults
                    _graphicsApiSensor.Value = "Unknown";
                    _architectureSensor.Value = "Unknown";
                    _gameCategorySensor.Value = "Unknown";
                    // Game resolution sensor removed
                    _displayModeSensor.Value = "Unknown";
                    
                    // Clear cached window title
                    _lastValidWindowTitle = string.Empty;

                    _fileLogger?.LogInfo("All sensors reset to default values (including enhanced RTSS sensors)");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error resetting sensors", ex);
                }
            }
        }

        /// <summary>
        /// Resets only the enhanced RTSS sensors to their default values (called when game quits).
        /// </summary>
        public void ResetEnhancedSensors()
        {
            lock (_sensorLock)
            {
                try
                {
                    // Reset enhanced RTSS sensors to defaults
                    _graphicsApiSensor.Value = "Unknown";
                    _architectureSensor.Value = "Unknown";
                    _gameCategorySensor.Value = "Unknown";
                    // Game resolution sensor removed
                    _displayModeSensor.Value = "Unknown";
                    
                    // ⭐ CRITICAL FIX: Reset Min/Avg/Max FPS sensors to prevent stuck values
                    _minFpsSensor.Value = 0;
                    _avgFpsSensor.Value = 0;
                    _maxFpsSensor.Value = 0;

                    _fileLogger?.LogInfo("Enhanced RTSS sensors reset to default values (game quit detected)");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error resetting enhanced sensors", ex);
                }
            }
        }

        /// <summary>
        /// Updates only the performance sensors with new metrics.
        /// </summary>
        /// <param name="metrics">Performance metrics to apply.</param>
        public void UpdatePerformanceSensors(PerformanceMetrics metrics)
        {
            lock (_sensorLock)
            {
                try
                {
                    // Always update sensors regardless of IsValid to allow clearing to 0
                    _fpsSensor.Value = metrics.Fps;
                    _currentFrameTimeSensor.Value = metrics.FrameTime;
                    _onePercentLowFpsSensor.Value = metrics.OnePercentLowFps;
                    
                    // Log sensor updates for debugging
                    _fileLogger?.LogSensorUpdate("Performance", $"FPS: {metrics.Fps}, FrameTime: {metrics.FrameTime:F2}ms, 1%Low: {metrics.OnePercentLowFps}");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating performance sensors", ex);
                }
            }
        }

        /// <summary>
        /// Updates only the window information sensor.
        /// </summary>
        /// <param name="windowInfo">Window information to apply.</param>
        public void UpdateWindowSensor(WindowInformation windowInfo)
        {
            lock (_sensorLock)
            {
                try
                {
                    // Only show debug logs if debug is enabled in configuration
                    bool debugEnabled = _configService?.IsDebugEnabled ?? false;
                    
                    if (debugEnabled)
                    {
                        _fileLogger?.LogWindowDetection("Detection", windowInfo.WindowTitle ?? "Unknown", (int)windowInfo.ProcessId, windowInfo.IsFullscreen);
                    }
                    
                    if (windowInfo.IsValid)
                    {
                        var newTitle = !string.IsNullOrWhiteSpace(windowInfo.WindowTitle) 
                            ? windowInfo.WindowTitle 
                            : "Untitled";
                        
                        var currentTitle = _windowTitleSensor.Value;
                        var defaultMessage = _configService?.DefaultCaptureMessage ?? "Nothing to capture";
                        
                        // Preserve existing good titles - don't overwrite with generic defaults
                        if (newTitle != "Untitled" || currentTitle == SensorConstants.NoCapture || currentTitle == defaultMessage)
                        {
                            // Only log and update if the title actually changed
                            if (newTitle != currentTitle)
                            {
                                _fileLogger?.LogStateChange("Window Title", currentTitle, newTitle, $"PID: {windowInfo.ProcessId}");
                                _windowTitleSensor.Value = newTitle;
                            }
                            // No logging for identical updates - eliminates spam
                        }
                    }
                    else
                    {
                        // Reset to NoCapture when window becomes invalid - only if changed
                        var currentTitle = _windowTitleSensor.Value;
                        if (currentTitle != SensorConstants.NoCapture)
                        {
                            _fileLogger?.LogStateChange("Window Title", currentTitle, SensorConstants.NoCapture, "Window invalid");
                            _windowTitleSensor.Value = SensorConstants.NoCapture;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating window sensor", ex);
                }
            }
        }

        /// <summary>
        /// Updates only the system information sensors.
        /// </summary>
        /// <param name="systemInfo">System information to apply.</param>
        public void UpdateSystemSensors(SystemInformation systemInfo)
        {
            lock (_sensorLock)
            {
                try
                {
                    _resolutionSensor.Value = systemInfo.Resolution;
                    _refreshRateSensor.Value = systemInfo.RefreshRate;
                    _gpuNameSensor.Value = systemInfo.GpuName;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating system sensors", ex);
                }
            }
        }

        /// <summary>
        /// Updates only the window title sensor with a direct value.
        /// </summary>
        /// <param name="title">The title to set.</param>
        public void UpdateWindowTitle(string title)
        {
            lock (_sensorLock)
            {
                try
                {
                    _windowTitleSensor.Value = title;
                    _fileLogger?.LogInfo($"Window title sensor updated to: '{title}'");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating window title sensor", ex);
                }
            }
        }

        /// <summary>
        /// Updates enhanced sensors with comprehensive RTSSCandidate data.
        /// </summary>
        /// <param name="candidate">The RTSSCandidate containing enhanced gaming metrics.</param>
        public void UpdateEnhancedSensors(RTSSCandidate candidate)
        {
            lock (_sensorLock)
            {
                try
                {
                    // Update performance sensors
                    _fpsSensor.Value = (float)candidate.Fps;
                    _onePercentLowFpsSensor.Value = (float)candidate.OnePercentLowFps;
                    _currentFrameTimeSensor.Value = (float)candidate.FrameTimeMs;
                    
                    // Update native RTSS statistics sensors  
                    _minFpsSensor.Value = (float)candidate.MinFps;
                    _avgFpsSensor.Value = (float)candidate.AvgFps;
                    _maxFpsSensor.Value = (float)candidate.MaxFps;
                    
                    // Update enhanced text sensors with RTSSCandidate data
                    _graphicsApiSensor.Value = candidate.GraphicsAPI ?? "Unknown";
                    _architectureSensor.Value = candidate.ArchitectureString ?? "Unknown";
                    _gameCategorySensor.Value = "Gaming"; // Simplified for now
                    _displayModeSensor.Value = candidate.WindowMode ?? "Unknown";
                    
                    // Update window title sensor
                    _windowTitleSensor.Value = candidate.WindowTitle ?? (_configService?.DefaultCaptureMessage ?? "Nothing to capture");
                    
                    _fileLogger?.LogDebug($"Enhanced sensors updated - FPS: {candidate.Fps:F1}, 1% Low: {candidate.OnePercentLowFps:F1}, API: {candidate.GraphicsAPI}, Resolution: {candidate.ResolutionString}");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating enhanced sensors", ex);
                }
            }
        }

        /// <summary>
        /// Gets the current values of all sensors for debugging purposes.
        /// </summary>
        /// <returns>A dictionary containing sensor IDs and their current values.</returns>
        public Dictionary<string, object> GetSensorValues()
        {
            return new Dictionary<string, object>
            {
                [SensorConstants.FpsSensorId] = _fpsSensor.Value,
                [SensorConstants.OnePercentLowFpsSensorId] = _onePercentLowFpsSensor.Value,
                [SensorConstants.CurrentFrameTimeSensorId] = _currentFrameTimeSensor.Value,
                [SensorConstants.WindowTitleSensorId] = _windowTitleSensor.Value,
                [SensorConstants.ResolutionSensorId] = _resolutionSensor.Value,
                [SensorConstants.RefreshRateSensorId] = _refreshRateSensor.Value,
                [SensorConstants.GpuNameSensorId] = _gpuNameSensor.Value
            };
        }

        /// <summary>
        /// Updates the benchmark mode sensor based on RTSSMonitoringService status (v1.2.0 feature).
        /// </summary>
        /// <param name="hasWriteAccess">Whether BenchmarkModeManager has write access to RTSS shared memory.</param>
        /// <param name="isInitialized">Whether BenchmarkModeManager is initialized.</param>
        public void UpdateBenchmarkModeSensor(bool hasWriteAccess, bool isInitialized)
        {
            lock (_sensorLock)
            {
                try
                {
                    if (!isInitialized)
                    {
                        _benchmarkModeSensor.Value = "Failed (RTSS Not Running)";
                    }
                    else if (!hasWriteAccess)
                    {
                        _benchmarkModeSensor.Value = "✗ Disabled (Run as Administrator)";
                    }
                    else
                    {
                        _benchmarkModeSensor.Value = "✓ Enabled";
                    }
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError("Error updating benchmark mode sensor", ex);
                }
            }
        }
    }
}