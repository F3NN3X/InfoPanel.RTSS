using System;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Represents a candidate process detected by RTSS with comprehensive gaming metrics
    /// Includes advanced RTSS shared memory data for detailed performance analysis
    /// </summary>
    public class RTSSCandidate
    {
        // Basic process identification
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string ProcessPath { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        
        // State flags
        public bool IsFullscreen { get; set; }
        public bool IsForeground { get; set; }
        
        // Core FPS metrics
        public double Fps { get; set; }
        public double FrameTimeMs { get; set; }
        public double OnePercentLowFps { get; set; }
        
        // RTSS native percentile calculations
        public double OnePercentLowFpsNative { get; set; }
        public double ZeroPointOnePercentLowFps { get; set; }
        
        // Advanced frame timing
        public double GpuFrameTimeMs { get; set; }
        public double CpuFrameTimeMs { get; set; }
        
        // Graphics system information
        public string GraphicsAPI { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string GameCategory { get; set; } = string.Empty;
        
        // Display properties (resolution removed - was confusing in borderless mode)
        public double RefreshRate { get; set; }
        public bool VSync { get; set; }
        public string DisplayMode { get; set; } = string.Empty;
        
        // RTSS internal data
        public uint RTSSFlags { get; set; }
        public uint RTSSEngineVersion { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.Now;
    }
}