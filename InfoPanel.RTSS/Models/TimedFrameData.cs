using System;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Time-based frame data for accurate 1% low calculation following CapFrameX methodology.
    /// Stores frame time with precise timestamp for time-weighted statistical analysis.
    /// </summary>
    public struct TimedFrameData
    {
        public double FrameTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
        
        public TimedFrameData(double frameTimeMs, DateTime timestamp)
        {
            FrameTimeMs = frameTimeMs;
            Timestamp = timestamp;
        }
    }
}