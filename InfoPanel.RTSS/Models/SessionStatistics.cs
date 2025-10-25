using System;
using System.Collections.Generic;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Session-wide statistical aggregation for memory-efficient long-session tracking.
    /// Uses SortedDictionary to maintain worst frame times without storing all frames.
    /// </summary>
    public class SessionStatistics
    {
        public DateTime SessionStart { get; set; } = DateTime.UtcNow;
        public TimeSpan TotalSessionDuration => DateTime.UtcNow - SessionStart;
        public long TotalFrameCount { get; set; } = 0;
        
        // SortedDictionary for efficient worst frame tracking (key=frameTime, value=count)
        public SortedDictionary<double, int> WorstFrameTimes { get; set; } = new(new DescendingDoubleComparer());
        
        // Track aggregation efficiency
        public int MaxWorstFramesTracked { get; set; } = 10000; // Configurable limit
        public long TotalWorstFramesProcessed { get; set; } = 0;
        
        public void Reset()
        {
            SessionStart = DateTime.UtcNow;
            TotalFrameCount = 0;
            WorstFrameTimes.Clear();
            TotalWorstFramesProcessed = 0;
        }
    }

    /// <summary>
    /// Custom comparer for SortedDictionary to maintain descending order (worst times first)
    /// </summary>
    public class DescendingDoubleComparer : IComparer<double>
    {
        public int Compare(double x, double y)
        {
            // Reverse comparison for descending order
            return y.CompareTo(x);
        }
    }
}