using System;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Time-based frame data for accurate 1% low calculation following CapFrameX methodology.
    /// Stores frame time with precise timestamp and focus state for smart filtering.
    /// </summary>
    public struct TimedFrameData
    {
        public double FrameTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
        public bool WasFocused { get; set; }
        
        public TimedFrameData(double frameTimeMs, DateTime timestamp, bool wasFocused = true)
        {
            FrameTimeMs = frameTimeMs;
            Timestamp = timestamp;
            WasFocused = wasFocused;
        }
    }
}