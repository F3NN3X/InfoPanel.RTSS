// InfoPanel.RTSS v1.1.4 - RTSS-Only FPS Monitoring Plugin
using InfoPanel.Plugins;
using InfoPanel.RTSS.Services;
using InfoPanel.RTSS.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.RTSS
{
    /// <summary>
    /// Simplified RTSS-only FPS monitoring plugin.
    /// Eliminates complex fullscreen detection in favor of direct RTSS shared memory scanning.
    /// </summary>
    public class InfoPanelFPS : BasePlugin
    {
        #region BasePlugin Properties

        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(1);
        
        public override string? ConfigFilePath => "InfoPanel.RTSS.ini";

        #endregion

        #region Private Fields

        private readonly RTSSOnlyMonitoringService _rtssMonitoringService;
        private readonly SensorManagementService _sensorService;
        private readonly SystemInformationService _systemInfoService;
        private readonly ConfigurationService _configService;
        private readonly FileLoggingService _fileLogger;
        private CancellationTokenSource? _cancellationTokenSource;
        
        #endregion

        #region Constructor

        public InfoPanelFPS() : base("InfoPanel.RTSS", "InfoPanel RTSS Monitor", "RTSS-based FPS monitoring plugin for InfoPanel")
        {
            try
            {
                _configService = new ConfigurationService();
                _configService.LogCurrentSettings(); // Log settings after config is loaded
                _fileLogger = new FileLoggingService(_configService);
                _systemInfoService = new SystemInformationService();
                _sensorService = new SensorManagementService();
                
                // Initialize RTSS-only monitoring service with event handling
                _rtssMonitoringService = new RTSSOnlyMonitoringService(_fileLogger);
                _rtssMonitoringService.MetricsUpdated += OnMetricsUpdated;

                _fileLogger.LogInfo("InfoPanel.RTSS plugin constructed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error constructing InfoPanel.RTSS plugin: {ex}");
            }
        }

        #endregion

        #region BasePlugin Overrides

        /// <summary>
        /// Loads sensors into the InfoPanel framework.
        /// </summary>
        public override void Load(List<InfoPanel.Plugins.IPluginContainer> containers)
        {
            try
            {
                _sensorService.CreateAndRegisterSensors(containers);
                Console.WriteLine("RTSS sensors loaded successfully");
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
        /// Simplified update method - RTSS monitoring service handles everything internally.
        /// </summary>
        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Update system sensors (GPU, resolution, etc.)
                var systemInfo = _systemInfoService.GetSystemInformation();
                _sensorService.UpdateSystemSensors(systemInfo);

                await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Basic throttling

                // RTSS monitoring service handles all monitoring logic internally
                // Sensors are updated via the OnMetricsUpdated event handler
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateAsync error: {ex}");
                _fileLogger.LogError("UpdateAsync error", ex);
            }
        }

        /// <summary>
        /// Initializes the simplified RTSS-only plugin and starts monitoring.
        /// </summary>
        public override void Initialize()
        {
            try
            {
                _fileLogger.LogInfo("=== RTSS Plugin Initialize() called ===");
                Console.WriteLine("=== RTSS Plugin Initialize() called ===");

                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize system information
                var systemInfo = _systemInfoService.GetSystemInformation();
                _fileLogger.LogSystemInfo("Information", $"GPU: {systemInfo.GpuName}, Display: {systemInfo.Resolution}@{systemInfo.RefreshRate}Hz");

                // Start simplified RTSS monitoring
                _ = Task.Run(async () => await _rtssMonitoringService.StartMonitoringAsync(_cancellationTokenSource.Token).ConfigureAwait(false));
                _fileLogger.LogInfo("RTSS-only monitoring task started");

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
        /// Closes the plugin by disposing resources.
        /// </summary>
        public override void Close() => Dispose();

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles metrics updates from the RTSS monitoring service.
        /// </summary>
        private void OnMetricsUpdated(double fps, double frameTime, double onePercentLow, string windowTitle, int processId)
        {
            try
            {
                // Create performance metrics object
                var performance = new PerformanceMetrics
                {
                    Fps = (float)fps,
                    FrameTime = (float)frameTime,
                    OnePercentLowFps = (float)onePercentLow
                };

                // Create window information object using the actual RTSS-monitored process ID
                var window = new WindowInformation
                {
                    ProcessId = (uint)Math.Max(0, processId), // Use actual process ID from RTSS
                    WindowTitle = windowTitle,
                    WindowHandle = processId > 0 ? new IntPtr(1) : IntPtr.Zero, // Valid when we have a PID
                    IsFullscreen = processId > 0 // Valid when RTSS is monitoring a process
                };

                _sensorService.UpdatePerformanceSensors(performance);
                _sensorService.UpdateWindowSensor(window);
                
                _fileLogger.LogInfo($"Metrics updated - FPS: {fps:F1}, 1% Low: {onePercentLow:F1}, Title: {windowTitle}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnMetricsUpdated: {ex}");
                _fileLogger.LogError("Error in OnMetricsUpdated", ex);
            }
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _cancellationTokenSource?.Cancel();
                        _rtssMonitoringService?.Dispose();
                        _cancellationTokenSource?.Dispose();
                        
                        _fileLogger?.LogInfo("InfoPanel.RTSS plugin disposed successfully");
                        _fileLogger?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during disposal: {ex}");
                    }
                }
                _disposed = true;
            }
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
