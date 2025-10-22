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
        private readonly PluginSensor _fpsSensor;
        private readonly PluginSensor _onePercentLowFpsSensor;
        private readonly PluginSensor _currentFrameTimeSensor;
        private readonly PluginSensor _avgFpsSensor;       // RTSS built-in average FPS
        private readonly PluginSensor _minFpsSensor;       // RTSS built-in min FPS (1% low equivalent)
        private readonly PluginSensor _maxFpsSensor;       // RTSS built-in max FPS
        private readonly PluginText _windowTitleSensor;
        private readonly PluginText _resolutionSensor;
        private readonly PluginSensor _refreshRateSensor;
        private readonly PluginText _gpuNameSensor;
        
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
        public SensorManagementService(ConfigurationService? configService = null)
        {
            _configService = configService;
            
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

            _avgFpsSensor = new PluginSensor(
                "avg-fps",
                "Average FPS",
                0,
                SensorConstants.FpsUnit
            );

            _minFpsSensor = new PluginSensor(
                "min-fps",
                "Min FPS (1% Low)",
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
                SensorConstants.DefaultWindowTitle
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

            Console.WriteLine("Sensor management service initialized with all sensors");
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
            container.Entries.Add(_avgFpsSensor);
            container.Entries.Add(_minFpsSensor);
            container.Entries.Add(_maxFpsSensor);
            container.Entries.Add(_onePercentLowFpsSensor);
            container.Entries.Add(_currentFrameTimeSensor);
            container.Entries.Add(_windowTitleSensor);
            container.Entries.Add(_resolutionSensor);
            container.Entries.Add(_refreshRateSensor);
            container.Entries.Add(_gpuNameSensor);

            containers.Add(container);
            
            Console.WriteLine($"Registered {container.Entries.Count} sensors in RTSS container");
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
                    
                    // Update RTSS built-in statistics sensors
                    _avgFpsSensor.Value = state.Performance.AverageFps;
                    _minFpsSensor.Value = state.Performance.MinFps;
                    _maxFpsSensor.Value = state.Performance.MaxFps;
                }
                else
                {
                    // Reset performance sensors when not monitoring
                    _fpsSensor.Value = 0;
                    _currentFrameTimeSensor.Value = 0;
                    _onePercentLowFpsSensor.Value = 0;
                    _avgFpsSensor.Value = 0;
                    _minFpsSensor.Value = 0;
                    _maxFpsSensor.Value = 0;
                    // Add logging for debugging
                    Console.WriteLine("SensorManagementService: Reset all FPS sensors to 0");
                }

                // Update window information with caching to prevent flickering
                // ONLY show window title for the process that RTSS is actively monitoring
                if (state.IsMonitoring)
                {
                    // Get the PID that RTSS is actually monitoring (providing FPS data)
                    uint monitoredPid = state.Performance.MonitoredProcessId;
                    
                    // DEBUG: Always log the state to understand what's happening
                    Console.WriteLine($"[SENSOR DEBUG] IsMonitoring={state.IsMonitoring}, MonitoredPID={monitoredPid}, WindowPID={state.Window?.ProcessId ?? 0}, WindowTitle='{state.Window?.WindowTitle ?? "null"}'");
                    
                    // Update cached title ONLY if window PID matches RTSS monitored PID
                    if (monitoredPid > 0 && 
                        state.Window != null &&
                        state.Window.ProcessId == monitoredPid && 
                        !string.IsNullOrWhiteSpace(state.Window.WindowTitle))
                    {
                        if (_lastValidWindowTitle != state.Window.WindowTitle)
                        {
                            Console.WriteLine($"Window title cached: '{state.Window.WindowTitle}' (PID: {monitoredPid})");
                            _lastValidWindowTitle = state.Window.WindowTitle;
                        }
                    }
                    else
                    {
                        // Debug: Log why caching didn't happen
                        Console.WriteLine($"Title NOT cached - MonitoredPID: {monitoredPid}, WindowPID: {state.Window?.ProcessId ?? 0}, Title: '{state.Window?.WindowTitle ?? "null"}', IsWhitespace: {string.IsNullOrWhiteSpace(state.Window?.WindowTitle)}");
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
                    _windowTitleSensor.Value = SensorConstants.DefaultWindowTitle;
                }

                // Update system information (always available)
                _resolutionSensor.Value = state.System.Resolution;
                _refreshRateSensor.Value = state.System.RefreshRate;
                _gpuNameSensor.Value = state.System.GpuName;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating sensors: {ex}");
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
                    _avgFpsSensor.Value = 0;
                    _minFpsSensor.Value = 0;
                    _maxFpsSensor.Value = 0;

                    // Reset information sensors to defaults
                    _windowTitleSensor.Value = SensorConstants.DefaultWindowTitle;
                    _resolutionSensor.Value = SensorConstants.DefaultResolution;
                    _refreshRateSensor.Value = 0;
                    _gpuNameSensor.Value = SensorConstants.DefaultGpuName;
                    
                    // Clear cached window title
                    _lastValidWindowTitle = string.Empty;

                    Console.WriteLine("All sensors reset to default values");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error resetting sensors: {ex}");
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
                    Console.WriteLine($"Performance sensors updated - FPS: {metrics.Fps}, FrameTime: {metrics.FrameTime:F2}ms, 1%Low: {metrics.OnePercentLowFps}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating performance sensors: {ex}");
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
                        Console.WriteLine($"[DEBUG] UpdateWindowSensor - IsValid: {windowInfo.IsValid}, PID: {windowInfo.ProcessId}, Handle: {windowInfo.WindowHandle}, Fullscreen: {windowInfo.IsFullscreen}, Title: '{windowInfo.WindowTitle}'");
                    }
                    
                    if (windowInfo.IsValid)
                    {
                        var newTitle = !string.IsNullOrWhiteSpace(windowInfo.WindowTitle) 
                            ? windowInfo.WindowTitle 
                            : "Untitled";
                        
                        if (debugEnabled)
                        {
                            Console.WriteLine($"[DEBUG] Setting title to: '{newTitle}' (from '{windowInfo.WindowTitle}')");
                        }
                        
                        // Preserve existing good titles - don't overwrite with generic defaults
                        if (newTitle != "Untitled" || _windowTitleSensor.Value == SensorConstants.NoCapture || _windowTitleSensor.Value == SensorConstants.DefaultWindowTitle)
                        {
                            _windowTitleSensor.Value = newTitle;
                            if (debugEnabled)
                            {
                                Console.WriteLine($"[DEBUG] Title sensor updated to: '{_windowTitleSensor.Value}'");
                            }
                        }
                        else
                        {
                            if (debugEnabled)
                            {
                                Console.WriteLine($"[DEBUG] Title NOT updated - preserving existing: '{_windowTitleSensor.Value}'");
                            }
                        }
                    }
                    else
                    {
                        // Reset to NoCapture when window becomes invalid
                        if (debugEnabled)
                        {
                            Console.WriteLine($"[DEBUG] Window invalid - setting to NoCapture");
                        }
                        _windowTitleSensor.Value = SensorConstants.NoCapture;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating window sensor: {ex}");
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
                    Console.WriteLine($"Error updating system sensors: {ex}");
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
                    Console.WriteLine($"Window title sensor updated to: '{title}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating window title sensor: {ex}");
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
    }
}