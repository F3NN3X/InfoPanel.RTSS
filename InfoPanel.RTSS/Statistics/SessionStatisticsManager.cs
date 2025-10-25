using System;
using System.Linq;
using InfoPanel.RTSS.Models;
using InfoPanel.RTSS.Services;

namespace InfoPanel.RTSS.Statistics
{
    /// <summary>
    /// Manages session-wide statistics for memory-efficient long-term gaming session tracking.
    /// Implements hybrid calculation system combining real-time and session-wide approaches.
    /// </summary>
    public class SessionStatisticsManager
    {
        private readonly SessionStatistics _sessionStats = new();
        private readonly object _sessionStatsLock = new object();
        private readonly FrameTimeCalculator _frameTimeCalculator;
        private readonly FileLoggingService? _fileLogger;
        
        private int _onePercentLowCalculationCount = 0;

        public SessionStatisticsManager(FrameTimeCalculator frameTimeCalculator, FileLoggingService? fileLogger = null)
        {
            _frameTimeCalculator = frameTimeCalculator;
            _fileLogger = fileLogger;
        }

        /// <summary>
        /// Updates session statistics with new frame data
        /// </summary>
        public void UpdateSessionStatistics(double frameTimeMs)
        {
            lock (_sessionStatsLock)
            {
                _sessionStats.TotalFrameCount++;
                
                // Only track frames worse than current tracked minimums to maintain efficiency
                bool shouldTrack = _sessionStats.WorstFrameTimes.Count < _sessionStats.MaxWorstFramesTracked;
                
                if (!shouldTrack && _sessionStats.WorstFrameTimes.Count > 0)
                {
                    // Check if this frame is worse than our best tracked worst frame
                    var bestWorstFrame = _sessionStats.WorstFrameTimes.Keys.Last(); // Last = smallest (best) due to descending sort
                    shouldTrack = frameTimeMs > bestWorstFrame;
                }
                
                if (shouldTrack)
                {
                    // Add or increment this frame time
                    if (_sessionStats.WorstFrameTimes.ContainsKey(frameTimeMs))
                    {
                        _sessionStats.WorstFrameTimes[frameTimeMs]++;
                    }
                    else
                    {
                        _sessionStats.WorstFrameTimes[frameTimeMs] = 1;
                        
                        // If we exceed our limit, remove the best (smallest) worst frame
                        if (_sessionStats.WorstFrameTimes.Count > _sessionStats.MaxWorstFramesTracked)
                        {
                            var bestWorstFrame = _sessionStats.WorstFrameTimes.Keys.Last();
                            _sessionStats.WorstFrameTimes.Remove(bestWorstFrame);
                        }
                    }
                    
                    _sessionStats.TotalWorstFramesProcessed++;
                }
            }
        }

        /// <summary>
        /// Enhanced 1% low calculation combining real-time (60s window) and session-wide statistics.
        /// Provides both immediate responsiveness and long-term session accuracy.
        /// </summary>
        public double CalculateEnhanced1PercentLow()
        {
            // Calculate real-time 1% low from 60-second buffer
            var realTime1PercentLow = _frameTimeCalculator.Calculate1PercentLow();
            
            // Calculate session-wide 1% low from statistical aggregation
            var session1PercentLow = CalculateSession1PercentLow();
            
            // Hybrid approach: Use real-time for short sessions, session-wide for longer sessions
            lock (_sessionStatsLock)
            {
                var sessionMinutes = _sessionStats.TotalSessionDuration.TotalMinutes;
                
                // For sessions under 10 minutes, primarily use real-time calculation
                if (sessionMinutes < 10)
                {
                    return realTime1PercentLow;
                }
                
                // For longer sessions, blend real-time and session-wide calculations
                // Weight shifts from real-time to session-wide as session gets longer
                var sessionWeight = Math.Min(0.8, sessionMinutes / 60.0); // Max 80% session weight at 1 hour
                var realTimeWeight = 1.0 - sessionWeight;
                
                // If either calculation is invalid, use the valid one
                if (realTime1PercentLow <= 0) return session1PercentLow;
                if (session1PercentLow <= 0) return realTime1PercentLow;
                
                // Weighted blend of both calculations
                var enhancedResult = (realTime1PercentLow * realTimeWeight) + (session1PercentLow * sessionWeight);
                
                // Enhanced logging every 100 calculations
                if (_onePercentLowCalculationCount % 100 == 0)
                {
                    _fileLogger?.LogInfo($"Enhanced 1% Low: Session {sessionMinutes:F1}min, Real-time: {realTime1PercentLow:F1} FPS, Session: {session1PercentLow:F1} FPS, Blended: {enhancedResult:F1} FPS (weights: {realTimeWeight:F2}/{sessionWeight:F2})");
                }
                
                _onePercentLowCalculationCount++;
                return enhancedResult;
            }
        }

        /// <summary>
        /// Calculate session-wide 1% low from statistical aggregation without storing all frames.
        /// Uses worst frame time tracking for memory-efficient long session analysis.
        /// </summary>
        private double CalculateSession1PercentLow()
        {
            lock (_sessionStatsLock)
            {
                // Need sufficient session data for meaningful calculation
                if (_sessionStats.TotalFrameCount < 1000 || _sessionStats.WorstFrameTimes.Count == 0)
                {
                    return 0.0;
                }
                
                try
                {
                    // Calculate 1% of total frames processed
                    var onePercentFrameCount = Math.Max(1, (long)(_sessionStats.TotalFrameCount * 0.01));
                    
                    // Accumulate worst frames until we reach 1% of total frame count
                    long accumulatedFrames = 0;
                    double totalWorstFrameTime = 0;
                    
                    foreach (var kvp in _sessionStats.WorstFrameTimes)
                    {
                        var frameTime = kvp.Key;
                        var count = kvp.Value;
                        
                        var framesToAdd = Math.Min(count, onePercentFrameCount - accumulatedFrames);
                        totalWorstFrameTime += frameTime * framesToAdd;
                        accumulatedFrames += framesToAdd;
                        
                        if (accumulatedFrames >= onePercentFrameCount)
                            break;
                    }
                    
                    // Calculate average worst frame time and convert to FPS
                    if (accumulatedFrames > 0)
                    {
                        var averageWorstFrameTime = totalWorstFrameTime / accumulatedFrames;
                        return averageWorstFrameTime > 0 ? 1000.0 / averageWorstFrameTime : 0.0;
                    }
                    
                    return 0.0;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"Error calculating session 1% low: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Resets session statistics for new gaming session
        /// </summary>
        public void ResetSession()
        {
            lock (_sessionStatsLock)
            {
                _sessionStats.Reset();
                _fileLogger?.LogInfo("Session statistics reset for new gaming session");
            }
        }

        /// <summary>
        /// Gets current session statistics for debugging/monitoring
        /// </summary>
        public (TimeSpan duration, long frameCount, int worstFramesTracked) GetSessionInfo()
        {
            lock (_sessionStatsLock)
            {
                return (_sessionStats.TotalSessionDuration, _sessionStats.TotalFrameCount, _sessionStats.WorstFrameTimes.Count);
            }
        }

        /// <summary>
        /// Gets the session start time
        /// </summary>
        public DateTime SessionStart
        {
            get
            {
                lock (_sessionStatsLock)
                {
                    return _sessionStats.SessionStart;
                }
            }
        }
    }
}