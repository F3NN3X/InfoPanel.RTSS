using System;
using System.Runtime.InteropServices;

namespace InfoPanel.RTSS.Models
{
    /// <summary>
    /// RTSS Shared Memory Structures - Direct port from working C++ implementation
    /// Provides access to all RTSS performance data including background monitoring
    /// </summary>
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RTSS_SHARED_MEMORY
    {
        // CRITICAL: Field order must match RTSSSharedMemory.h exactly!
        public uint dwSignature;                    // 'RTSS' (0x52545353)
        public uint dwVersion;                      // Version (e.g., 0x00020015 = v2.21)
        
        // App array descriptor (correct order from RTSS header)
        public uint dwAppEntrySize;                 // Size of each app entry in bytes (typically varies by RTSS version)
        public uint dwAppArrOffset;                 // Offset to app array in bytes (0x00003080 typically)
        public uint dwAppArrSize;                   // Number of app array entries (256 typically)
        
        // OSD array descriptor  
        public uint dwOSDEntrySize;                 // Size of each OSD entry
        public uint dwOSDArrOffset;                 // Offset to OSD array
        public uint dwOSDArrSize;                   // Number of OSD entries
        
        public uint dwOSDFrame;                     // Global OSD frame ID
        
        // v2.14+ fields
        public int dwBusy;                          // Busy flag for thread safety
        
        // v2.15+ fields  
        public uint dwDesktopVideoCaptureFlags;
        public uint dwDesktopVideoCaptureStat0;     // Desktop video stats (5 uints inline)
        public uint dwDesktopVideoCaptureStat1;
        public uint dwDesktopVideoCaptureStat2;
        public uint dwDesktopVideoCaptureStat3;
        public uint dwDesktopVideoCaptureStat4;
        
        // v2.16+ fields
        public uint dwLastForegroundApp;            // Last foreground app index
        public uint dwLastForegroundAppProcessID;   // Last foreground app PID
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct RTSS_SHARED_MEMORY_APP_ENTRY
    {
        // CRITICAL: Must match exact order from RTSSSharedMemory.h!
        
        // Application identification (first 3 fields)
        public uint dwProcessID;                    // Process ID
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szName;                       // Process executable name (MAX_PATH)
        
        public uint dwFlags;                        // Application flags (API type, architecture)
        
        // Instantaneous framerate fields
        public uint dwTime0;                        // Start time (milliseconds)
        public uint dwTime1;                        // End time (milliseconds)
        public uint dwFrames;                       // Frame count in time period
        public uint dwFrameTime;                    // Current frame time (microseconds)
        
        // Framerate statistics fields
        public uint dwStatFlags;                    // Statistics flags
        public uint dwStatTime0;                    // Stats period start
        public uint dwStatTime1;                    // Stats period end
        public uint dwStatFrames;                   // Total frames in stats period
        public uint dwStatCount;                    // Number of min/avg/max measurements
        public uint dwStatFramerateMin;             // Minimum FPS * 10
        public uint dwStatFramerateAvg;             // Average FPS * 10
        public uint dwStatFramerateMax;             // Maximum FPS * 10
        
        // OSD fields (lots of them!)
        public uint dwOSDX;
        public uint dwOSDY;
        public uint dwOSDPixel;
        public uint dwOSDColor;
        public uint dwOSDFrame;
        
        public uint dwScreenCaptureFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szScreenCapturePath;
        
        // v2.1+ fields
        public uint dwOSDBgndColor;
        
        // v2.2+ fields
        public uint dwVideoCaptureFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szVideoCapturePath;
        public uint dwVideoFramerate;
        public uint dwVideoFramesize;
        public uint dwVideoFormat;
        public uint dwVideoQuality;
        public uint dwVideoCaptureThreads;
        public uint dwScreenCaptureQuality;
        public uint dwScreenCaptureThreads;
        
        // v2.3+ fields
        public uint dwAudioCaptureFlags;
        
        // v2.4+ fields
        public uint dwVideoCaptureFlagsEx;
        
        // v2.5+ fields (frame time statistics)
        public uint dwAudioCaptureFlags2;
        public uint dwStatFrameTimeMin;
        public uint dwStatFrameTimeAvg;
        public uint dwStatFrameTimeMax;
        public uint dwStatFrameTimeCount;
        
        // Frame time buffer (1024 DWORDs)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public uint[] dwStatFrameTimeBuf;
        
        public uint dwStatFrameTimeBufPos;
        public uint dwStatFrameTimeBufFramerate;
        
        // v2.6+ fields
        public long qwAudioCapturePTTEventPush;
        public long qwAudioCapturePTTEventRelease;
        public long qwAudioCapturePTTEventPush2;
        public long qwAudioCapturePTTEventRelease2;
        
        // v2.8+ fields
        public uint dwPrerecordSizeLimit;
        public uint dwPrerecordTimeLimit;
        
        // v2.13+ fields (1% Low statistics!)
        public long qwStatTotalTime;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public uint[] dwStatFrameTimeLowBuf;
        
        public uint dwStatFramerate1Dot0PercentLow; // ⭐ 1% Low FPS * 10
        public uint dwStatFramerate0Dot1PercentLow; // 0.1% Low FPS * 10
        
        // v2.17+ fields
        public uint dw1Dot0PercentLowBufPos;
        public uint dw0Dot1PercentLowBufPos;
        
        // v2.18+ fields (process performance counters)
        public uint dwProcessPerfCountersFlags;
        public uint dwProcessPerfCountersCount;
        public uint dwProcessPerfCountersSamplingPeriod;
        public uint dwProcessPerfCountersSamplingTime;
        public uint dwProcessPerfCountersTimestamp;
        
        // v2.19+ fields (latency marker)
        public long qwLatencyMarkerPresentTimestamp;
        
        // v2.20+ fields (resolution)
        public uint dwResolutionX;
        public uint dwResolutionY;
        
        // v2.21+ fields (detailed frame timing - REFLEX support)
        public ulong qwInputSampleTime;
        public ulong qwSimStartTime;
        public ulong qwSimEndTime;
        public ulong qwRenderSubmitStartTime;
        public ulong qwRenderSubmitEndTime;
        public ulong qwPresentStartTime;
        public ulong qwPresentEndTime;
        public ulong qwDriverStartTime;
        public ulong qwDriverEndTime;
        public ulong qwOsRenderQueueStartTime;
        public ulong qwOsRenderQueueEndTime;
        public ulong qwGpuRenderStartTime;
        public ulong qwGpuRenderEndTime;
        public uint dwGpuActiveRenderTime;
        public uint dwGpuFrameTime;
        
        // Note: Process perf counters array (256 entries) is accessed via dwProcessPerfCountersArrOffset
        // We don't include it here since we access it dynamically based on the offset
    }

    /// <summary>
    /// RTSS Application Flags - API Types and Architecture (from your C++ code)
    /// </summary>
    public static class RTSSFlags
    {
        // API Types (lower 16 bits)
        public const uint API_OPENGL = 0x00000001;
        public const uint API_DIRECTDRAW = 0x00000002;
        public const uint API_D3D8 = 0x00000003;
        public const uint API_D3D9 = 0x00000004;
        public const uint API_D3D9EX = 0x00000005;
        public const uint API_D3D10 = 0x00000006;
        public const uint API_D3D11 = 0x00000007;
        public const uint API_D3D12 = 0x00000008;
        public const uint API_D3D12AFR = 0x00000009;
        public const uint API_VULKAN = 0x0000000A;
        
        // Architecture flags
        public const uint APPFLAG_ARCHITECTURE_X64 = 0x00010000;
        public const uint APPFLAG_ARCHITECTURE_UWP = 0x00020000;
        
        /// <summary>
        /// Get API name from flags (exact match to your C++ GetAPIName function)
        /// </summary>
        public static string GetAPIName(uint flags)
        {
            uint apiType = flags & 0x0000FFFF;
            return apiType switch
            {
                API_OPENGL => "OpenGL",
                API_DIRECTDRAW => "DirectDraw", 
                API_D3D8 => "D3D8",
                API_D3D9 => "D3D9",
                API_D3D9EX => "D3D9Ex",
                API_D3D10 => "D3D10",
                API_D3D11 => "D3D11",
                API_D3D12 => "D3D12",
                API_D3D12AFR => "D3D12AFR",
                API_VULKAN => "Vulkan",
                _ => "Unknown"
            };
        }
        
        /// <summary>
        /// Check if application is x64 architecture
        /// </summary>
        public static bool IsX64(uint flags) => (flags & APPFLAG_ARCHITECTURE_X64) != 0;
        
        /// <summary>
        /// Check if application is UWP
        /// </summary>
        public static bool IsUWP(uint flags) => (flags & APPFLAG_ARCHITECTURE_UWP) != 0;
    }
    
    /// <summary>
    /// Helper class for RTSS calculations (ported from your C++ code)
    /// </summary>
    public static class RTSSCalculations
    {
        /// <summary>
        /// Calculate framerate from RTSS timing data (exact port of your C++ CalculateFramerate function)
        /// </summary>
        public static float CalculateFramerate(uint dwTime0, uint dwTime1, uint dwFrames)
        {
            if (dwTime1 <= dwTime0 || dwFrames == 0) return 0.0f;
            uint dwDelta = dwTime1 - dwTime0;
            if (dwDelta == 0) return 0.0f;
            return (float)dwFrames * 1000.0f / (float)dwDelta;
        }
        
        /// <summary>
        /// Convert RTSS native statistics from DWORD * 10 to float
        /// </summary>
        public static float ConvertRTSSStatistic(uint dwStatValue)
        {
            return dwStatValue / 10.0f;
        }
        
        /// <summary>
        /// Check if application has 3D rendering data (ported from your C++ bIs3DApp logic)
        /// </summary>
        public static bool Is3DApplication(uint dwTime0, uint dwTime1, uint dwFrames)
        {
            return dwTime0 != 0 || dwTime1 != 0 || dwFrames != 0;
        }
    }
}