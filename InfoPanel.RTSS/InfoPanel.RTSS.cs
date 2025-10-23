// InfoPanel.RTSS v1.1.5 - RTSS-Only FPS Monitoring Plugin
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
    /// RTSS-only performance monitoring plugin for InfoPanel.
    /// Provides real-time FPS, frame times, and comprehensive gaming metrics through RTSS shared memory integration.
    /// Supports enhanced metrics including graphics API detection, game categorization, and advanced performance statistics.
    /// </summary>
    public class InfoPanelRTSS : BasePlugin
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

        public InfoPanelRTSS() : base("InfoPanel.RTSS", "InfoPanel RTSS Monitor", "Advanced RTSS-based performance monitoring plugin for InfoPanel")
        {
            try
            {
                _configService = new ConfigurationService();
                _configService.LogCurrentSettings(); // Log settings after config is loaded
                _fileLogger = new FileLoggingService(_configService);
                _systemInfoService = new SystemInformationService(_fileLogger);
                _sensorService = new SensorManagementService(_configService, _fileLogger);
                
                // Initialize RTSS-only monitoring service with event handling
                _rtssMonitoringService = new RTSSOnlyMonitoringService(_configService, _fileLogger);
                _rtssMonitoringService.MetricsUpdated += OnMetricsUpdated;
                _rtssMonitoringService.EnhancedMetricsUpdated += OnEnhancedMetricsUpdated;

                _fileLogger.LogInfo("InfoPanel.RTSS plugin constructed successfully");
            }
            catch (Exception ex)
            {
                _fileLogger?.LogError("Error constructing InfoPanel.RTSS plugin", ex);
            }
        }

        #endregion

        #region BasePlugin Overrides

        /// <summary>
        /// Loads comprehensive RTSS sensors into the InfoPanel framework.
        /// </summary>
        public override void Load(List<InfoPanel.Plugins.IPluginContainer> containers)
        {
            try
            {
                _sensorService.CreateAndRegisterSensors(containers);
                _fileLogger.LogInfo("RTSS sensors loaded successfully");
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Error loading sensors", ex);
            }
        }

        /// <summary>
        /// Not implemented; UpdateAsync is used instead for async operations.
        /// </summary>
        public override void Update() => throw new NotImplementedException();

        /// <summary>
        /// Updates system information sensors while RTSS monitoring runs independently.
        /// </summary>
        public override async Task UpdateAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Update system sensors (GPU, resolution, etc.)
                var systemInfo = _systemInfoService.GetSystemInformation();
                _sensorService.UpdateSystemSensors(systemInfo);

                await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Basic throttling

                // RTSS monitoring service handles all performance monitoring independently  
                // Performance sensors are updated via event handlers
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("UpdateAsync error", ex);
            }
        }

        /// <summary>
        /// Initializes the RTSS performance monitoring plugin and starts background monitoring.
        /// </summary>
        public override void Initialize()
        {
            try
            {
                _fileLogger.LogInfo("=== RTSS Performance Monitoring Plugin Initialize() ===");

                _cancellationTokenSource = new CancellationTokenSource();

                // Initialize system information
                var systemInfo = _systemInfoService.GetSystemInformation();
                _fileLogger.LogSystemInfo("Information", $"GPU: {systemInfo.GpuName}, Display: {systemInfo.Resolution}@{systemInfo.RefreshRate}Hz");

                // Start RTSS shared memory monitoring
                _ = Task.Run(async () => await _rtssMonitoringService.StartMonitoringAsync(_cancellationTokenSource.Token).ConfigureAwait(false));
                _fileLogger.LogInfo("RTSS shared memory monitoring started");

                _fileLogger.LogInfo("RTSS Plugin initialization completed successfully");
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Error during plugin initialization", ex);
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

                _sensorService.UpdatePerformanceSensors(performance);
                
                // Handle window title updates based on whether we have an active game
                if (processId > 0)
                {
                    // Game is running - use normal window sensor update
                    var window = new WindowInformation
                    {
                        ProcessId = (uint)processId,
                        WindowTitle = windowTitle,
                        WindowHandle = new IntPtr(1), // Valid when we have a PID
                        IsFullscreen = true // Valid when RTSS is monitoring a process
                    };
                    _sensorService.UpdateWindowSensor(window);
                }
                else
                {
                    // No game running - directly set the configured default message
                    _sensorService.UpdateWindowTitle(windowTitle);
                    
                    // Reset enhanced sensors when no game is running to prevent stuck values
                    _sensorService.ResetEnhancedSensors();
                }
                
                _fileLogger.LogDebug($"Performance metrics updated - FPS: {fps:F1}, 1% Low: {onePercentLow:F1}, Game: {windowTitle}");
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Error in OnMetricsUpdated", ex);
            }
        }

        /// <summary>
        /// Handles enhanced metrics updates from the RTSS monitoring service with comprehensive gaming data.
        /// </summary>
        private void OnEnhancedMetricsUpdated(RTSSCandidate candidate)
        {
            try
            {
                // Comprehensive enhanced metrics logging (Debug level - too frequent for Info)
                _fileLogger.LogDebug($"=== Enhanced Metrics Update ===");
                _fileLogger.LogDebug($"Process: PID {candidate.ProcessId} ({candidate.ProcessName}) - {candidate.WindowTitle}");
                _fileLogger.LogDebug($"Performance: FPS {candidate.Fps:F1}, Frame Time {candidate.FrameTimeMs:F2}ms, GPU Frame Time {candidate.GpuFrameTimeMs:F2}ms");
                _fileLogger.LogDebug($"RTSS Native Stats: Min {candidate.MinFps:F1} | Avg {candidate.AvgFps:F1} | Max {candidate.MaxFps:F1} FPS");
                _fileLogger.LogDebug($"RTSS Frame Times: Min {candidate.MinFrameTimeMs:F2} | Avg {candidate.AvgFrameTimeMs:F2} | Max {candidate.MaxFrameTimeMs:F2} ms");
                _fileLogger.LogDebug($"RTSS Percentiles: 1% Low {candidate.OnePercentLowFpsNative:F1} | 0.1% Low {candidate.ZeroPointOnePercentLowFps:F1} FPS");
                _fileLogger.LogDebug($"Graphics: {candidate.GraphicsAPI} ({candidate.Architecture}) - {candidate.GameCategory}");
                _fileLogger.LogDebug($"Display: {candidate.ResolutionX}x{candidate.ResolutionY} @ {candidate.RefreshRate:F0}Hz, {candidate.DisplayMode}, VSync: {candidate.VSync}");
                _fileLogger.LogDebug($"State: Fullscreen {candidate.IsFullscreen}, Foreground {candidate.IsForeground}");
                
                // Update the enhanced sensors with the full RTSSCandidate data
                _sensorService.UpdateEnhancedSensors(candidate);
            }
            catch (Exception ex)
            {
                _fileLogger.LogError("Error in OnEnhancedMetricsUpdated", ex);
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
                        // Can't use _fileLogger here as it may be disposed, fallback to console for disposal errors only
                        Console.WriteLine($"Error during disposal: {ex}");
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
