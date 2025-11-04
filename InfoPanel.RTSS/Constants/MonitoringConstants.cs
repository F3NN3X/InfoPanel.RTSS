namespace InfoPanel.RTSS.Constants
{
    /// <summary>
    /// Contains configuration constants used throughout the RTSS monitoring plugin.
    /// </summary>
    public static class MonitoringConstants
    {
        /// <summary>
        /// Update interval for UI sensors in seconds.
        /// </summary>
        public const int UiUpdateIntervalSeconds = 1;

        /// <summary>
        /// Require this percentage of monitor area coverage for fullscreen detection.
        /// </summary>
        public const float FullscreenAreaThreshold = 0.95f;
    }

    /// <summary>
    /// Contains sensor configuration constants.
    /// </summary>
    public static class SensorConstants
    {
        public const string FpsSensorId = "fps";
        public const string OnePercentLowFpsSensorId = "1% low fps";
        public const string CurrentFrameTimeSensorId = "current frame time";
        public const string WindowTitleSensorId = "windowtitle";
        public const string ResolutionSensorId = "resolution";
        public const string RefreshRateSensorId = "refreshrate";
        public const string GpuNameSensorId = "gpu-name";

        public const string FpsSensorDisplayName = "Frames Per Second";
        public const string OnePercentLowFpsSensorDisplayName = "1% Low FPS";
        public const string CurrentFrameTimeSensorDisplayName = "Current Frame Time";
        public const string WindowTitleSensorDisplayName = "Currently Capturing";
        public const string ResolutionSensorDisplayName = "Display Resolution";
        public const string RefreshRateSensorDisplayName = "Display Refresh Rate";
        public const string GpuNameSensorDisplayName = "GPU Name";

        public const string FpsUnit = "FPS";
        public const string FrameTimeUnit = "ms";
        public const string RefreshRateUnit = "Hz";

        // DefaultWindowTitle removed in v1.2.0 - now uses ConfigurationService.DefaultCaptureMessage
        public const string DefaultResolution = "0 x 0";
        public const string DefaultGpuName = "Unknown GPU";
        public const string NoCapture = "-";
    }
}