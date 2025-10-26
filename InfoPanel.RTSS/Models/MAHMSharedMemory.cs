using System;
using System.Runtime.InteropServices;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// MSI Afterburner Hardware Monitoring (MAHM) shared memory structures for v2.0.
    /// Ported from MAHMSharedMemory.h - provides access to hardware monitoring sensors
    /// and framerate statistics without requiring RTSS benchmark mode.
    /// </summary>
    
    /// <summary>
    /// MAHM shared memory header containing metadata about the monitoring data.
    /// Signature 'MAHM' (0x4D48414D) indicates valid data is available.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MAHM_SHARED_MEMORY_HEADER
    {
        /// <summary>
        /// Signature to verify shared memory status:
        /// - 'MAHM' (0x4D48414D): Hardware monitoring memory is initialized and contains valid data
        /// - 0xDEAD: Memory is marked for deallocation and no longer contains valid data
        /// - Other values: Memory is not initialized
        /// </summary>
        public uint dwSignature;

        /// <summary>
        /// Header version in format (major &lt;&lt; 16) + minor.
        /// Must be 0x00020000 for v2.0.
        /// </summary>
        public uint dwVersion;

        /// <summary>
        /// Size of this header structure in bytes.
        /// </summary>
        public uint dwHeaderSize;

        /// <summary>
        /// Number of subsequent MAHM_SHARED_MEMORY_ENTRY structures in the entries array.
        /// </summary>
        public uint dwNumEntries;

        /// <summary>
        /// Size of each entry in the MAHM_SHARED_MEMORY_ENTRY array in bytes.
        /// </summary>
        public uint dwEntrySize;

        /// <summary>
        /// Last polling time as 32-bit Unix timestamp (seconds since epoch).
        /// </summary>
        public int time;

        /// <summary>
        /// Number of subsequent MAHM_SHARED_MEMORY_GPU_ENTRY structures (v2.0+).
        /// </summary>
        public uint dwNumGpuEntries;

        /// <summary>
        /// Size of each entry in the MAHM_SHARED_MEMORY_GPU_ENTRY array in bytes (v2.0+).
        /// </summary>
        public uint dwGpuEntrySize;
    }

    /// <summary>
    /// Flags for MAHM_SHARED_MEMORY_ENTRY.dwFlags field.
    /// </summary>
    [Flags]
    public enum MAHMEntryFlags : uint
    {
        /// <summary>
        /// Data source is configured to be displayed in On-Screen Display.
        /// </summary>
        ShowInOSD = 0x00000001,

        /// <summary>
        /// Data source is configured to be displayed in Logitech keyboard LCD.
        /// </summary>
        ShowInLCD = 0x00000002,

        /// <summary>
        /// Data source is configured to be displayed in tray icon.
        /// </summary>
        ShowInTray = 0x00000004
    }

    /// <summary>
    /// MAHM monitoring source IDs for identifying specific sensor types.
    /// Critical IDs for framerate statistics (no benchmark mode required):
    /// - FRAMERATE_MIN: Minimum FPS
    /// - FRAMERATE_AVG: Average FPS  
    /// - FRAMERATE_MAX: Maximum FPS
    /// - FRAMERATE_1DOT0_PERCENT_LOW: 1% Low FPS (99th percentile worst frame times)
    /// </summary>
    public static class MAHMMonitoringSourceId
    {
        // Unknown/Invalid
        public const uint Unknown = 0xFFFFFFFF;

        // GPU Temperature Sensors
        public const uint GpuTemperature = 0x00000000;
        public const uint PcbTemperature = 0x00000001;
        public const uint MemTemperature = 0x00000002;
        public const uint VrmTemperature = 0x00000003;

        // Fan Sensors
        public const uint FanSpeed = 0x00000010;
        public const uint FanTachometer = 0x00000011;
        public const uint FanSpeed2 = 0x00000012;
        public const uint FanTachometer2 = 0x00000013;
        public const uint FanSpeed3 = 0x00000014;
        public const uint FanTachometer3 = 0x00000015;

        // GPU Clock Sensors
        public const uint CoreClock = 0x00000020;
        public const uint ShaderClock = 0x00000021;
        public const uint MemoryClock = 0x00000022;

        // GPU Usage Sensors
        public const uint GpuUsage = 0x00000030;
        public const uint MemoryUsage = 0x00000031;
        public const uint FrameBufferUsage = 0x00000032;
        public const uint VideoEngineUsage = 0x00000033;
        public const uint BusUsage = 0x00000034;
        public const uint MemoryUsageProcess = 0x00000035;

        // GPU Voltage Sensors
        public const uint GpuVoltage = 0x00000040;
        public const uint AuxVoltage = 0x00000041;
        public const uint MemoryVoltage = 0x00000042;
        public const uint Aux2Voltage = 0x00000043;

        // **CRITICAL: Framerate Statistics (Primary Use Case)**
        /// <summary>
        /// Current instantaneous framerate (FPS).
        /// </summary>
        public const uint Framerate = 0x00000050;

        /// <summary>
        /// Current frame time in milliseconds.
        /// </summary>
        public const uint Frametime = 0x00000051;

        /// <summary>
        /// Minimum FPS recorded during monitoring session.
        /// Replaces RTSS dwStatFramerateMin (no benchmark mode needed).
        /// </summary>
        public const uint FramerateMin = 0x00000052;

        /// <summary>
        /// Average FPS calculated over monitoring session.
        /// Replaces RTSS dwStatFramerateAvg (no benchmark mode needed).
        /// </summary>
        public const uint FramerateAvg = 0x00000053;

        /// <summary>
        /// Maximum FPS recorded during monitoring session.
        /// Replaces RTSS dwStatFramerateMax (no benchmark mode needed).
        /// </summary>
        public const uint FramerateMax = 0x00000054;

        /// <summary>
        /// 1% Low FPS - 99th percentile of worst frame times.
        /// Replaces RTSS dwStatFramerateLow (no benchmark mode needed).
        /// Key metric for smoothness and stuttering analysis.
        /// </summary>
        public const uint Framerate1Dot0PercentLow = 0x00000055;

        /// <summary>
        /// 0.1% Low FPS - 99.9th percentile of worst frame times.
        /// Even more stringent smoothness metric.
        /// </summary>
        public const uint Framerate0Dot1PercentLow = 0x00000056;

        // GPU Power Sensors
        public const uint GpuRelPower = 0x00000060;
        public const uint GpuAbsPower = 0x00000061;

        // GPU Limits
        public const uint GpuTempLimit = 0x00000070;
        public const uint GpuPowerLimit = 0x00000071;
        public const uint GpuVoltageLimit = 0x00000072;
        public const uint GpuUtilLimit = 0x00000074;
        public const uint GpuSliSyncLimit = 0x00000075;

        // CPU Sensors
        public const uint CpuTemperature = 0x00000080;
        public const uint CpuUsage = 0x00000090;
        public const uint CpuClock = 0x000000A0;
        public const uint CpuPower = 0x00000100;

        // System Memory Sensors
        public const uint RamUsage = 0x00000091;
        public const uint PagefileUsage = 0x00000092;
        public const uint RamUsageProcess = 0x00000093;

        // Multi-GPU Temperature Sensors (GPU 2-5)
        public const uint GpuTemperature2 = 0x000000B0;
        public const uint PcbTemperature2 = 0x000000B1;
        public const uint MemTemperature2 = 0x000000B2;
        public const uint VrmTemperature2 = 0x000000B3;

        public const uint GpuTemperature3 = 0x000000C0;
        public const uint PcbTemperature3 = 0x000000C1;
        public const uint MemTemperature3 = 0x000000C2;
        public const uint VrmTemperature3 = 0x000000C3;

        public const uint GpuTemperature4 = 0x000000D0;
        public const uint PcbTemperature4 = 0x000000D1;
        public const uint MemTemperature4 = 0x000000D2;
        public const uint VrmTemperature4 = 0x000000D3;

        public const uint GpuTemperature5 = 0x000000E0;
        public const uint PcbTemperature5 = 0x000000E1;
        public const uint MemTemperature5 = 0x000000E2;
        public const uint VrmTemperature5 = 0x000000E3;

        // Plugin Sensors
        public const uint PluginGpu = 0x000000F0;
        public const uint PluginCpu = 0x000000F1;
        public const uint PluginMobo = 0x000000F2;
        public const uint PluginRam = 0x000000F3;
        public const uint PluginHdd = 0x000000F4;
        public const uint PluginNet = 0x000000F5;
        public const uint PluginPsu = 0x000000F6;
        public const uint PluginUps = 0x000000F7;
        public const uint PluginMisc = 0x000000FF;
    }

    /// <summary>
    /// MAHM shared memory entry containing sensor data (temperature, usage, framerate, etc.).
    /// Array of these entries follows immediately after MAHM_SHARED_MEMORY_HEADER in shared memory.
    /// Use dwSrcId (MAHMMonitoringSourceId constants) to identify specific sensors.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct MAHM_SHARED_MEMORY_ENTRY
    {
        /// <summary>
        /// Data source name in English (e.g. "Core clock", "GPU temperature").
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szSrcName;

        /// <summary>
        /// Data source units (e.g. "MHz", "°C", "FPS").
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szSrcUnits;

        /// <summary>
        /// Localized data source name (e.g. "Частота ядра" for Russian GUI).
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szLocalizedSrcName;

        /// <summary>
        /// Localized data source units (e.g. "МГц" for Russian GUI).
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szLocalizedSrcUnits;

        /// <summary>
        /// Recommended output format string (e.g. "%.3f" for voltage sensors).
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szRecommendedFormat;

        /// <summary>
        /// Last polled sensor data (e.g. 500.0 for 500MHz, 149.6 for 149.6 FPS).
        /// Set to float.MaxValue (FLT_MAX) if data is not available.
        /// </summary>
        public float data;

        /// <summary>
        /// Minimum limit for graph rendering (e.g. 0 for clocks/FPS).
        /// </summary>
        public float minLimit;

        /// <summary>
        /// Maximum limit for graph rendering (e.g. 2000 for 2GHz clock limit).
        /// </summary>
        public float maxLimit;

        /// <summary>
        /// Bitmask containing combination of MAHMEntryFlags (ShowInOSD, ShowInLCD, ShowInTray).
        /// </summary>
        public uint dwFlags;

        /// <summary>
        /// Data source GPU index (zero-based) or 0xFFFFFFFF for global data sources.
        /// Framerate statistics (Min/Avg/Max/1% Low) use 0xFFFFFFFF (global).
        /// GPU-specific sensors (temperature, usage) use 0, 1, 2, etc. for multi-GPU systems.
        /// </summary>
        public uint dwGpu;

        /// <summary>
        /// Data source ID (see MAHMMonitoringSourceId constants).
        /// Use this to identify specific sensors (e.g. 0x00000052 for FramerateMin).
        /// </summary>
        public uint dwSrcId;
    }

    /// <summary>
    /// MAHM shared memory GPU entry containing GPU hardware information.
    /// Array of these entries follows immediately after MAHM_SHARED_MEMORY_ENTRY array in shared memory.
    /// Provides GPU identification, driver version, BIOS version, and memory size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct MAHM_SHARED_MEMORY_GPU_ENTRY
    {
        /// <summary>
        /// GPU identifier in VEN_%04X&amp;DEV_%04X&amp;SUBSYS_%08X&amp;REV_%02X&amp;BUS_%d&amp;DEV_%d&amp;FN_%d format.
        /// Example: "VEN_10DE&amp;DEV_0A20&amp;SUBSYS_071510DE&amp;REV_00&amp;BUS_1&amp;DEV_0&amp;FN_0"
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szGpuId;

        /// <summary>
        /// GPU family/architecture (e.g. "GT216", "Ampere", "RDNA3").
        /// Can be empty if data is not available.
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szFamily;

        /// <summary>
        /// Display device description (e.g. "GeForce GT 220", "Radeon RX 7900 XTX").
        /// Can be empty if data is not available.
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDevice;

        /// <summary>
        /// Display driver description (e.g. "6.14.11.9621, ForceWare 196.21", "31.0.24001.5003").
        /// Can be empty if data is not available.
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDriver;

        /// <summary>
        /// BIOS version (e.g. "70.16.24.00.00", "115-D4120300-100").
        /// Can be empty if data is not available.
        /// MAX_PATH = 260 characters.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szBIOS;

        /// <summary>
        /// On-board GPU memory amount in kilobytes (e.g. 1048576 = 1GB).
        /// Can be 0 if data is not available.
        /// </summary>
        public uint dwMemAmount;
    }
}
