using InfoPanel.RTSS.Models;
using System;
using System.Collections.Generic;

namespace InfoPanel.RTSS.Interfaces
{
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
    }

    /// <summary>
    /// Service responsible for managing configuration settings.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets whether debug logging is enabled.
        /// </summary>
        bool IsDebugEnabled { get; }
        
        /// <summary>
        /// Gets the configuration file path.
        /// </summary>
        string ConfigFilePath { get; }
    }
}
