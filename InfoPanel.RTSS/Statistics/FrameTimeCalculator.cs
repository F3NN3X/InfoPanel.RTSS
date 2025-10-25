using System;
using System.Collections.Generic;
using System.Linq;
using InfoPanel.RTSS.Models;
using InfoPanel.RTSS.Services;

namespace InfoPanel.RTSS.Statistics
{
    /// <summary>
    /// Handles frame time calculations including industry-standard 1% low FPS calculations.
    /// Implements both real-time (60-second) and session-wide statistical approaches.
    /// </summary>
    public class FrameTimeCalculator
    {
        private readonly Queue<double> _frameTimeBuffer = new();
        private readonly Queue<TimedFrameData> _timedFrameBuffer = new();
        private readonly object _frameBufferLock = new object();
        private readonly FileLoggingService? _fileLogger;
        
        private static readonly TimeSpan MinBufferDuration = TimeSpan.FromSeconds(60);
        private const int FrameBufferSize = 100;
        private int _onePercentLowCalculationCount = 0;

        public FrameTimeCalculator(FileLoggingService? fileLogger = null)
        {
            _fileLogger = fileLogger;
        }

        /// <summary>
        /// Updates frame time buffers with new frame data
        /// </summary>
        public void UpdateFrameTimeBuffer(double frameTime)
        {
            if (frameTime <= 0) return;
            
            // Update legacy frame buffer (for compatibility)
            _frameTimeBuffer.Enqueue(frameTime);
            while (_frameTimeBuffer.Count > FrameBufferSize)
            {
                _frameTimeBuffer.Dequeue();
            }
            
            // Update time-based buffer for enhanced 1% low calculation
            AddFrameDataToBuffer(frameTime);
        }

        /// <summary>
        /// Add frame data to time-based buffer with automatic cleanup of old data
        /// Follows CapFrameX methodology for time-weighted 1% low calculation
        /// </summary>
        private void AddFrameDataToBuffer(double frameTimeMs)
        {
            // Default to focused state for backward compatibility
            AddFrameDataToBufferWithFocus(frameTimeMs, true);
        }

        /// <summary>
        /// Add frame data to time-based buffer with focus state metadata.
        /// Enables smart filtering for focus-aware 1% low calculations.
        /// </summary>
        private void AddFrameDataToBufferWithFocus(double frameTimeMs, bool wasFocused)
        {
            var now = DateTime.UtcNow;
            var frameData = new TimedFrameData(frameTimeMs, now, wasFocused);
            
            lock (_frameBufferLock)
            {
                _timedFrameBuffer.Enqueue(frameData);
                
                // Remove frames older than 60 seconds
                var cutoffTime = now.Subtract(MinBufferDuration);
                while (_timedFrameBuffer.Count > 0 && _timedFrameBuffer.Peek().Timestamp < cutoffTime)
                {
                    _timedFrameBuffer.Dequeue();
                }
                
                // Also remove excess frames to prevent memory growth (keep max 10,000 frames)
                while (_timedFrameBuffer.Count > 10000)
                {
                    _timedFrameBuffer.Dequeue();
                }
            }
        }

        /// <summary>
        /// Calculate 1% low FPS using time-weighted method following CapFrameX approach
        /// Returns the average frame rate during the worst 1% of gameplay time
        /// </summary>
        public double Calculate1PercentLow()
        {
            lock (_frameBufferLock)
            {
                // Need sufficient data for meaningful calculation
                if (_timedFrameBuffer.Count < 60) // At least 1 second at 60 FPS
                {
                    return 0.0;
                }

                try
                {
                    var frameData = _timedFrameBuffer.ToArray();
                    var totalDuration = frameData.Max(f => f.Timestamp) - frameData.Min(f => f.Timestamp);
                    
                    // Need at least 10 seconds of data for stable 1% low
                    if (totalDuration.TotalSeconds < 10)
                    {
                        return 0.0;
                    }

                    // Calculate 1% of total time duration
                    var onePercentDuration = totalDuration.TotalMilliseconds * 0.01;
                    
                    // Sort frames by frame time (worst frames first)
                    var sortedFrames = frameData.OrderByDescending(f => f.FrameTimeMs).ToArray();
                    
                    // Accumulate worst frame times until we reach 1% of total time
                    double accumulatedTime = 0;
                    var worstFrames = new List<TimedFrameData>();
                    
                    foreach (var frame in sortedFrames)
                    {
                        worstFrames.Add(frame);
                        accumulatedTime += frame.FrameTimeMs;
                        
                        if (accumulatedTime >= onePercentDuration)
                            break;
                    }
                    
                    // Calculate average frame time of worst 1% of time
                    var averageWorstFrameTime = worstFrames.Average(f => f.FrameTimeMs);
                    var onePercentLowFps = averageWorstFrameTime > 0 ? 1000.0 / averageWorstFrameTime : 0.0;
                    
                    // Throttled logging (every 100 calculations)
                    _onePercentLowCalculationCount++;
                    if (_onePercentLowCalculationCount % 100 == 0)
                    {
                        _fileLogger?.LogInfo($"1% Low Calculation: {frameData.Length} frames over {totalDuration.TotalSeconds:F1}s, worst {worstFrames.Count} frames, avg worst: {averageWorstFrameTime:F2}ms = {onePercentLowFps:F1} FPS");
                    }
                    
                    return onePercentLowFps;
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"Error calculating time-based 1% low: {ex.Message}");
                    return 0.0;
                }
            }
        }

        /// <summary>
        /// Clears all frame time buffers
        /// </summary>
        public void ClearBuffers()
        {
            _frameTimeBuffer.Clear();
            
            lock (_frameBufferLock)
            {
                _timedFrameBuffer.Clear();
            }
        }

        /// <summary>
        /// Gets the current number of frames in the time-based buffer
        /// </summary>
        public int BufferFrameCount
        {
            get
            {
                lock (_frameBufferLock)
                {
                    return _timedFrameBuffer.Count;
                }
            }
        }

        /// <summary>
        /// Gets the time span covered by current buffer data
        /// </summary>
        public TimeSpan BufferDuration
        {
            get
            {
                lock (_frameBufferLock)
                {
                    if (_timedFrameBuffer.Count == 0) return TimeSpan.Zero;
                    
                    var frameData = _timedFrameBuffer.ToArray();
                    return frameData.Max(f => f.Timestamp) - frameData.Min(f => f.Timestamp);
                }
            }
        }
    }
}