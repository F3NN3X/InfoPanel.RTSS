namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// Structure for RTSS native statistics data
    /// Contains the raw native statistics read from RTSS shared memory
    /// </summary>
    public class RTSSNativeStatistics
    {
        public bool HasNativeStats { get; set; }
        public double Native1PercentLowFps { get; set; }
        public double Native0Point1PercentLowFps { get; set; }
        public int NativeFrameTimeUs { get; set; }
        public int NativeFrameTimeMinUs { get; set; }
        public int NativeFrameTimeMaxUs { get; set; }
        public int NativeFrameTimeAvgUs { get; set; }
        public uint NativeFrameCount { get; set; }
    }
}