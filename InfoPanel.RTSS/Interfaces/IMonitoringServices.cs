using InfoPanel.RTSS.Models;
using InfoPanel.RTSS.Services;

namespace InfoPanel.RTSS.Interfaces
{
    /// <summary>
    /// Service responsible for monitoring application performance using RTSS.
    /// </summary>
    public interface IPerformanceMonitoringService : IDisposable
    {
        /// <summary>
        /// Event fired when new performance metrics are available.
        /// </summary>
        event Action<PerformanceMetrics>? MetricsUpdated;

        /// <summary>
        /// Indicates whether performance monitoring is currently active.
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Starts monitoring performance for the specified process.
        /// </summary>
        /// <param name="processId">The process ID to monitor.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the monitoring operation.</returns>
        Task StartMonitoringAsync(uint processId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops performance monitoring and resets metrics.
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Gets the current performance metrics.
        /// </summary>
        /// <returns>Current performance metrics or null if not monitoring.</returns>
        PerformanceMetrics? GetCurrentMetrics();

        /// <summary>
        /// Gets the process ID that RTSS is currently monitoring.
        /// </summary>
        /// <returns>The monitored process ID, or 0 if no process is being monitored.</returns>
        uint GetRTSSMonitoredProcessId();

        /// <summary>
        /// Gets the underlying DXGI service for event subscriptions.
        /// </summary>
        DXGIFrameMonitoringService DXGIService { get; }
    }

    /// <summary>
    /// Service responsible for gathering system information.
    /// </summary>
    public interface ISystemInformationService
    {
        /// <summary>
        /// Gets the current system information including display settings and GPU name.
        /// </summary>
        /// <returns>Current system information.</returns>
        SystemInformation GetSystemInformation();

        /// <summary>
        /// Gets the primary monitor's resolution and refresh rate.
        /// </summary>
        /// <returns>A tuple containing resolution string and refresh rate.</returns>
        (string resolution, uint refreshRate) GetPrimaryMonitorSettings();

        /// <summary>
        /// Gets the name of the system's graphics card.
        /// </summary>
        /// <returns>GPU name or default value if not found.</returns>
        string GetGpuName();
    }

    /// <summary>
    /// Service responsible for managing InfoPanel sensors and their updates.
    /// </summary>
    public interface ISensorManagementService
    {
        /// <summary>
        /// Creates and registers all sensors with the provided container.
        /// </summary>
        /// <param name="containers">List of plugin containers to add sensors to.</param>
        void CreateAndRegisterSensors(List<InfoPanel.Plugins.IPluginContainer> containers);

        /// <summary>
        /// Updates all sensors with the current monitoring state.
        /// </summary>
        /// <param name="state">Current monitoring state containing all metrics.</param>
        void UpdateSensors(MonitoringState state);

        /// <summary>
        /// Resets all sensors to their default values.
        /// </summary>
        void ResetSensors();

        /// <summary>
        /// Updates only the performance sensors with new metrics.
        /// </summary>
        /// <param name="metrics">Performance metrics to apply.</param>
        void UpdatePerformanceSensors(PerformanceMetrics metrics);

        /// <summary>
        /// Updates only the window information sensor.
        /// </summary>
        /// <param name="windowInfo">Window information to apply.</param>
        void UpdateWindowSensor(WindowInformation windowInfo);

        /// <summary>
        /// Updates only the system information sensors.
        /// </summary>
        /// <param name="systemInfo">System information to apply.</param>
        void UpdateSystemSensors(SystemInformation systemInfo);
    }
}