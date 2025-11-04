using System;
using System.Collections.Generic;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Enhanced RTSS candidate with native statistics from RTSS shared memory
    /// Contains all performance data extracted directly from RTSS (ported from C++ implementation)
    /// </summary>
    public class RTSSCandidate
    {
        // Process Information
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public bool IsForeground { get; set; }
        
        // Current Performance (calculated from timing data like C++ version)
        public double Fps { get; set; }
        public double FrameTimeMs { get; set; }
        
        // RTSS Native Statistics (direct from RTSS calculations - ported from C++)
        public double MinFps { get; set; }
        public double AvgFps { get; set; }
        public double MaxFps { get; set; }
        public double OnePercentLowFps { get; set; }  // ⭐ Native RTSS 1% low from dwStatFramerate1Dot0PercentLow!
        
        // Technical Details
        public string GraphicsAPI { get; set; } = string.Empty;
        public string WindowMode { get; set; } = string.Empty;
        public uint ResolutionX { get; set; }
        public uint ResolutionY { get; set; }
        public bool IsX64 { get; set; }
        public bool IsUWP { get; set; }
        
        // RTSS Internal Data (from C++ implementation)
        public uint RTSSFlags { get; set; }
        public uint FrameCount { get; set; }
        public uint Time0 { get; set; }
        public uint Time1 { get; set; }
        public uint FrameTimeUs { get; set; }
        public bool Is3DApplication { get; set; }
        
        // Timestamps
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Determines if this application has valid 3D rendering data (ported from C++ bIs3DApp logic)
        /// </summary>
        public bool HasValid3DData => Is3DApplication && Fps > 0;
        
        /// <summary>
        /// Determines if this application has RTSS native statistics available
        /// </summary>
        public bool HasNativeStatistics => OnePercentLowFps > 0 || MinFps > 0 || MaxFps > 0;
        
        /// <summary>
        /// Get resolution string formatted like C++ version
        /// </summary>
        public string ResolutionString => ResolutionX > 0 && ResolutionY > 0 ? $"{ResolutionX}x{ResolutionY}" : "Unknown";
        
        /// <summary>
        /// Get architecture flags formatted like C++ version
        /// </summary>
        public string ArchitectureString
        {
            get
            {
                var parts = new List<string>();
                if (IsX64) parts.Add("x64");
                if (IsUWP) parts.Add("UWP");
                return parts.Count > 0 ? string.Join(" ", parts) : "";
            }
        }
        
        public override string ToString()
        {
            return $"{ProcessName} (PID: {ProcessId}) - {Fps:F1} FPS, 1% Low: {OnePercentLowFps:F1} FPS [{GraphicsAPI}]";
        }
    }
}