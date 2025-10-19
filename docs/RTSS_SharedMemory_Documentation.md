# RTSS Shared Memory Complete Documentation

## Overview

This document provides a comprehensive guide to accessing all performance data from RTSS (RivaTuner Statistics Server) shared memory, including FPS, frame times, percentile lows, GPU metrics, and advanced latency data. This documentation also covers related memory structures from PresentMon Data Provider, HWiNFO, and MSI Afterburner for complete system monitoring.

## Related Memory Structures

### 1. RTSS Shared Memory (RTSSSharedMemoryV2)
- **Primary focus**: Per-application frame rate and rendering metrics
- **Memory Name**: `RTSSSharedMemoryV2`
- **Access Mutex**: `Global\Access_ESSMSharedMemory`

### 2. PresentMon Data Provider (PMDP)
- **Focus**: ETW-based frame analysis with detailed latency breakdown
- **Memory Name**: `PMDPSharedMemory`
- **Signature**: `'PMDP'` (0x50444D50)

### 3. HWiNFO Sensors
- **Focus**: Hardware monitoring (temperatures, voltages, fan speeds)
- **Memory Name**: `Global\HWiNFO_SENS_SM2`
- **Access Mutex**: `Global\HWiNFO_SM2_MUTEX`

### 4. MSI Afterburner Hardware Monitoring (MAHM)
- **Focus**: GPU-focused hardware monitoring with frame rate metrics
- **Memory Name**: Varies by implementation
- **Signature**: `'MAHM'` (0x4D41484D)

## RTSS Memory Structure Layout

```
RTSSSharedMemoryV2 (1MB total)
├── Header (Variable size, contains offsets)
├── OSD Slots Array (8 × ~300KB each for v2.20+)
├── App Array (128 × 1024 bytes each = 128KB)  
├── Performance Counter Arrays (per-app, 256 entries each)
└── Video Capture Config
```

## RTSS Header Structure (Dynamic size based on version)

```cpp
struct RTSSSharedMemoryHeader {
    DWORD dwSignature;           // 0x53535452 ('RTSS')
    DWORD dwVersion;             // 0x00020015 (v2.21) or newer
    DWORD dwAppEntrySize;        // Size of each app entry (1024)
    DWORD dwAppArrOffset;        // Offset to app array
    DWORD dwAppArrSize;          // Size of app array (128 entries)
    DWORD dwOSDEntrySize;        // Size of each OSD entry (~300KB for v2.20+)
    DWORD dwOSDArrOffset;        // Offset to OSD array
    DWORD dwOSDArrSize;          // Size of OSD array (8 entries)
    DWORD dwOSDFrame;            // Global OSD frame counter
    LONG dwBusy;                 // Bit 0: Writer lock (v2.14+)
    
    // v2.15+ fields
    DWORD dwDesktopVideoCaptureFlags;
    DWORD dwDesktopVideoCaptureStat[5];
    
    // v2.16+ fields  
    DWORD dwLastForegroundApp;   // Last foreground app index
    DWORD dwLastForegroundAppProcessID; // Last foreground PID
    
    // v2.18+ fields
    DWORD dwProcessPerfCountersEntrySize; // Size of perf counter entries
    DWORD dwProcessPerfCountersArrOffset; // Offset to perf counters (per-app)
    
    // v2.19+ fields
    LARGE_INTEGER qwLatencyMarkerSetTimestamp;   // Input latency marker set
    LARGE_INTEGER qwLatencyMarkerResetTimestamp; // Input latency marker reset
};
```

## OSD Entry Structure (300KB+ each for v2.20+)

```cpp
struct RTSSOSDEntry {
    char szOSD[256];             // Basic OSD text
    char szOSDOwner[256];        // Owner ID
    char szOSDEx[4096];          // Extended OSD text (v2.7+)
    BYTE buffer[262144];         // 256KB data buffer (v2.12+)
    char szOSDEx2[32768];        // Additional 32KB text (v2.20+)
};
```

## Per-Application Entry Structure (1024 bytes each)

Each application has a dedicated 1024-byte slot in the app array:

### Basic Application Info (Offset +0)
```cpp
DWORD dwProcessID;               // +0: Process ID
char szName[MAX_PATH];           // +4: Process executable name (260 bytes)
DWORD dwFlags;                   // +264: Application flags
```

### Real-time FPS Data (Offset +268)
```cpp
DWORD dwTime0;                   // +268: Start time (milliseconds)
DWORD dwTime1;                   // +272: End time (milliseconds)  
DWORD dwFrames;                  // +276: Frame count in period
DWORD dwFrameTime;               // +280: Frame time (microseconds)
```

**FPS Calculation:**
- **Period-based FPS:** `1000.0f * dwFrames / (dwTime1 - dwTime0)`
- **Instantaneous FPS:** `1000000.0f / dwFrameTime`

### Statistics Data (Offset +284)
```cpp
DWORD dwStatFlags;               // +284: Statistics flags
DWORD dwStatTime0;               // +288: Stats period start
DWORD dwStatTime1;               // +292: Stats period end
DWORD dwStatFrames;              // +296: Total frames in stats period
DWORD dwStatCount;               // +300: Number of measurements
DWORD dwStatFramerateMin;        // +304: Minimum FPS (1% low equivalent)
DWORD dwStatFramerateAvg;        // +308: Average FPS
DWORD dwStatFramerateMax;        // +312: Maximum FPS
```

### Advanced Latency Data (v2.21+ - Offset +571)
```cpp
ULONGLONG qwInputSampleTime;     // +571: Input sampling timestamp
ULONGLONG qwSimStartTime;        // +579: Simulation start
ULONGLONG qwSimEndTime;          // +587: Simulation end
ULONGLONG qwRenderSubmitStartTime; // +595: Render submit start
ULONGLONG qwRenderSubmitEndTime; // +603: Render submit end
ULONGLONG qwPresentStartTime;    // +611: Present() call start
ULONGLONG qwPresentEndTime;      // +619: Present() call end
ULONGLONG qwDriverStartTime;     // +627: Driver processing start
ULONGLONG qwDriverEndTime;       // +635: Driver processing end
ULONGLONG qwOsRenderQueueStartTime; // +643: OS render queue start
ULONGLONG qwOsRenderQueueEndTime;   // +651: OS render queue end
ULONGLONG qwGpuRenderStartTime;  // +659: GPU render start
ULONGLONG qwGpuRenderEndTime;    // +667: GPU render end
DWORD dwGpuActiveRenderTime;     // +675: GPU active time (microseconds)
DWORD dwGpuFrameTime;            // +679: GPU frame time (microseconds)
```

### OSD Configuration (Offset +316)
```cpp
DWORD dwOSDX;                    // +316: OSD X position
DWORD dwOSDY;                    // +320: OSD Y position
DWORD dwOSDPixel;                // +324: OSD zoom level
DWORD dwOSDColor;                // +328: OSD text color (RGB)
DWORD dwOSDFrame;                // +332: OSD frame ID
```

### Performance Counters (v2.18+ - Per App)
```cpp
// Performance counter types
#define PROCESS_PERF_COUNTER_ID_RAM_USAGE                 0x00000001
#define PROCESS_PERF_COUNTER_ID_D3DKMT_VRAM_USAGE_LOCAL   0x00000100  
#define PROCESS_PERF_COUNTER_ID_D3DKMT_VRAM_USAGE_SHARED  0x00000101

struct RTSSPerformanceCounter {
    DWORD dwID;                  // Counter type ID
    DWORD dwParam;               // Counter parameters (e.g., GPU location)
    DWORD dwData;                // Counter value
};

// Each app has array of 256 performance counters
RTSSPerformanceCounter arrPerfCounters[256];
```

### Video Capture Configuration
```cpp
struct RTSSVideoCaptureConfig {
    DWORD dwVideoCaptureFlags;   // Capture settings
    char szVideoCapturePath[MAX_PATH]; // Output path
    DWORD dwVideoFramerate;      // Target FPS
    DWORD dwVideoFramesize;      // Frame size setting
    DWORD dwVideoFormat;         // Video format
    DWORD dwVideoQuality;        // Quality setting
    DWORD dwVideoCaptureThreads; // Thread count
};

// Video capture flags
#define VIDEOCAPTUREFLAG_CAPTURE_ENABLED              0x00000001
#define VIDEOCAPTUREFLAG_CAPTURE_OSD                  0x00000010
#define VIDEOCAPTUREFLAG_INTERNAL_RESIZE              0x00010000
```

## PresentMon Data Provider Structure

PresentMon Data Provider creates its own shared memory with comprehensive frame analysis:

```cpp
// Memory name: "PMDPSharedMemory"
struct PMDPSharedMemory {
    DWORD dwSignature;           // 'PMDP' (0x504D4450)
    DWORD dwVersion;             // 0x00020000 (v2.0)
    DWORD dwFrameArrEntrySize;   // sizeof(PMDP_FRAME_DATA)
    DWORD dwFrameArrOffset;      // Offset to frame array
    DWORD dwFrameArrSize;        // Array size (8192 entries)
    DWORD dwFrameCount;          // Total frames captured
    DWORD dwFramePos;            // Current position in ring buffer
    DWORD dwStatus;              // Current status (see PMDP_STATUS_*)
    
    PMDP_FRAME_DATA arrFrame[8192]; // Ring buffer of frame data
};

// Status codes
#define PMDP_STATUS_OK                    0
#define PMDP_STATUS_INIT_FAILED          1  
#define PMDP_STATUS_START_STREAM_FAILED  2
#define PMDP_STATUS_GET_FRAME_DATA_FAILED 3
```

### PresentMon Frame Data Structure
```cpp
struct PMDPFrameData {
    // V1 Data (Basic metrics)
    char Application[MAX_PATH];  // Process name
    uint32_t ProcessID;          // Process ID
    uint64_t SwapChainAddress;   // DirectX swap chain
    uint32_t Runtime;            // Graphics runtime (D3D11/12, etc.)
    int32_t SyncInterval;        // VSync setting
    uint32_t PresentFlags;       // Present() flags
    uint32_t Dropped;            // Frame dropped flag
    double TimeInSeconds;        // Frame timestamp
    double msInPresentAPI;       // Time in Present() call
    double msBetweenPresents;    // Frame time
    uint32_t AllowsTearing;      // Variable refresh rate
    uint32_t PresentMode;        // Presentation mode
    double msUntilRenderComplete; // Render completion time
    double msUntilDisplayed;     // Display latency
    double msBetweenDisplayChange; // Display change interval
    double msUntilRenderStart;   // Render start latency
    uint64_t qpcTime;            // High-precision timestamp
    double msSinceInput;         // Input latency
    double msGpuActive;          // GPU active time
    double msGpuVideoActive;     // GPU video processing time

    // V2 Data (Extended metrics)
    uint64_t CPUStart;           // CPU start timestamp
    double Frametime;            // Total frame time
    double CPUBusy;              // CPU busy time
    double CPUWait;              // CPU wait time  
    double GPULatency;           // GPU latency
    double GPUTime;              // GPU processing time
    double GPUBusy;              // GPU busy time
    double VideoBusy;            // Video processing busy time
    double GPUWait;              // GPU wait time
    double DisplayLatency;       // Display pipeline latency
    double DisplayedTime;        // Time when displayed
    double AnimationError;       // Animation smoothness error
    double ClickToPhotonLatency; // Total input-to-display latency
};
```

### PresentMon API Metrics (v3.0)

PresentMon API provides access to 100+ metrics through structured queries:

```cpp
// Key metrics available through PresentMon API
enum PMMetric {
    PM_METRIC_APPLICATION,                    // Application name
    PM_METRIC_DISPLAYED_FPS,                 // Displayed FPS
    PM_METRIC_PRESENTED_FPS,                 // Presented FPS  
    PM_METRIC_APPLICATION_FPS,               // Application FPS
    PM_METRIC_CPU_FRAME_TIME,                // CPU frame time
    PM_METRIC_GPU_TIME,                      // GPU time
    PM_METRIC_DISPLAY_LATENCY,               // Display latency
    PM_METRIC_CLICK_TO_PHOTON_LATENCY,       // Input latency
    PM_METRIC_GPU_POWER,                     // GPU power usage
    PM_METRIC_GPU_TEMPERATURE,               // GPU temperature
    PM_METRIC_GPU_UTILIZATION,               // GPU utilization
    PM_METRIC_GPU_MEM_USED,                  // GPU memory usage
    PM_METRIC_CPU_UTILIZATION,               // CPU utilization
    PM_METRIC_ANIMATION_ERROR,               // Frame pacing error
    PM_METRIC_FRAME_TYPE,                    // Frame type classification
    // ... 80+ more metrics
};

// Statistical processing options
enum PMStat {
    PM_STAT_AVG,                 // Average
    PM_STAT_PERCENTILE_99,       // 99th percentile
    PM_STAT_PERCENTILE_95,       // 95th percentile  
    PM_STAT_PERCENTILE_01,       // 1st percentile (1% low)
    PM_STAT_PERCENTILE_05,       // 5th percentile
    PM_STAT_MAX,                 // Maximum
    PM_STAT_MIN,                 // Minimum
    // ... more statistics
};
```

```csharp
public class RTSSFrameData
{
    public uint ProcessID { get; set; }
    public string ProcessName { get; set; } = "";
    public float CurrentFPS { get; set; }
    public float AverageFPS { get; set; }
    public float MinFPS { get; set; }          // 1% low equivalent
    public float MaxFPS { get; set; }
    public float FrameTimeMs { get; set; }
    public float GpuTimeMs { get; set; }
    public float InputLatencyMs { get; set; }
    public float RenderLatencyMs { get; set; }
    public float PresentLatencyMs { get; set; }
    public float TotalLatencyMs { get; set; }
}

public RTSSFrameData? ReadAppData(int appIndex)
{
    const int APP_ENTRY_SIZE = 1024;
    const int APP_ARRAY_OFFSET = 0x48; // After header
    
    int baseOffset = APP_ARRAY_OFFSET + (appIndex * APP_ENTRY_SIZE);
    
    // Read basic info
    uint pid = _accessor.ReadUInt32(baseOffset + 0);
    if (pid == 0) return null; // Empty slot
    
    byte[] nameBytes = new byte[260];
    _accessor.ReadArray(baseOffset + 4, nameBytes, 0, 260);
    string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
    
    // Read FPS data
    uint dwTime0 = _accessor.ReadUInt32(baseOffset + 268);
    uint dwTime1 = _accessor.ReadUInt32(baseOffset + 272);
    uint dwFrames = _accessor.ReadUInt32(baseOffset + 276);
    uint dwFrameTime = _accessor.ReadUInt32(baseOffset + 280);
    
    // Read statistics
    uint dwStatFramerateMin = _accessor.ReadUInt32(baseOffset + 304);
    uint dwStatFramerateAvg = _accessor.ReadUInt32(baseOffset + 308);
    uint dwStatFramerateMax = _accessor.ReadUInt32(baseOffset + 312);
    
    // Read GPU data (v2.21+)
    uint dwGpuActiveTime = _accessor.ReadUInt32(baseOffset + 675);
    uint dwGpuFrameTime = _accessor.ReadUInt32(baseOffset + 679);
    
    // Read latency timestamps (v2.21+)
    ulong qwInputTime = _accessor.ReadUInt64(baseOffset + 571);
    ulong qwSimStart = _accessor.ReadUInt64(baseOffset + 579);
    ulong qwSimEnd = _accessor.ReadUInt64(baseOffset + 587);
    ulong qwPresentStart = _accessor.ReadUInt64(baseOffset + 611);
    ulong qwPresentEnd = _accessor.ReadUInt64(baseOffset + 619);
    ulong qwGpuStart = _accessor.ReadUInt64(baseOffset + 659);
    ulong qwGpuEnd = _accessor.ReadUInt64(baseOffset + 667);
    
    return new RTSSFrameData
    {
        ProcessID = pid,
        ProcessName = name,
        CurrentFPS = dwFrameTime > 0 ? 1000000.0f / dwFrameTime : 0,
        AverageFPS = dwStatFramerateAvg / 1000.0f, // RTSS stores in millihertz
        MinFPS = dwStatFramerateMin / 1000.0f,     // 1% low
        MaxFPS = dwStatFramerateMax / 1000.0f,
        FrameTimeMs = dwFrameTime / 1000.0f,
        GpuTimeMs = dwGpuFrameTime / 1000.0f,
        InputLatencyMs = CalculateLatency(qwInputTime, qwPresentEnd),
        RenderLatencyMs = CalculateLatency(qwSimStart, qwGpuEnd),
        PresentLatencyMs = CalculateLatency(qwPresentStart, qwPresentEnd),
        TotalLatencyMs = CalculateLatency(qwInputTime, qwGpuEnd)
    };
}

private float CalculateLatency(ulong start, ulong end)
{
    if (start == 0 || end == 0 || end <= start) return 0;
    
    // Convert QPC timestamps to milliseconds
    // Note: You need QueryPerformanceFrequency() for accurate conversion
    return (float)(end - start) / 10000.0f; // Approximate for now
}
```

## Advanced Metrics Extraction

### 1% and 0.1% Low FPS
```csharp
// These require maintaining a rolling window of frame times
private Queue<float> _frameTimeWindow = new Queue<float>(1000);

public void UpdateFrameTimeWindow(float frameTimeMs)
{
    _frameTimeWindow.Enqueue(frameTimeMs);
    if (_frameTimeWindow.Count > 1000)
        _frameTimeWindow.Dequeue();
}

public float Calculate1PercentLow()
{
    if (_frameTimeWindow.Count < 100) return 0;
    
    var sorted = _frameTimeWindow.OrderByDescending(x => x).ToArray();
    int onePercentIndex = (int)(sorted.Length * 0.01);
    return 1000.0f / sorted[onePercentIndex]; // Convert worst 1% frame time to FPS
}

public float CalculatePoint1PercentLow()
{
    if (_frameTimeWindow.Count < 1000) return 0;
    
    var sorted = _frameTimeWindow.OrderByDescending(x => x).ToArray();
    int pointOnePercentIndex = (int)(sorted.Length * 0.001);
    return 1000.0f / sorted[pointOnePercentIndex]; // Convert worst 0.1% frame time to FPS
}
```

### Frame Time Variance and Consistency
```csharp
public RTSSAdvancedMetrics CalculateAdvancedMetrics()
{
    var frameTimes = _frameTimeWindow.ToArray();
    if (frameTimes.Length < 10) return new RTSSAdvancedMetrics();
    
    float avg = frameTimes.Average();
    float variance = frameTimes.Select(ft => (ft - avg) * (ft - avg)).Average();
    float stdDev = (float)Math.Sqrt(variance);
    
    return new RTSSAdvancedMetrics
    {
        AverageFrameTime = avg,
        FrameTimeVariance = variance,
        FrameTimeStdDev = stdDev,
        FrameTimeCV = stdDev / avg, // Coefficient of variation (consistency)
        P95FrameTime = frameTimes.OrderBy(x => x).Skip((int)(frameTimes.Length * 0.95)).First(),
        P99FrameTime = frameTimes.OrderBy(x => x).Skip((int)(frameTimes.Length * 0.99)).First()
    };
}
```

### Input-to-Display Latency Pipeline
```csharp
public RTSSLatencyBreakdown CalculateLatencyBreakdown(RTSSFrameData data)
{
    // Using the advanced timestamp data from v2.21+
    return new RTSSLatencyBreakdown
    {
        InputToSimulation = data.InputLatencyMs,
        SimulationTime = CalculateLatency(qwSimStart, qwSimEnd),
        RenderSubmissionTime = CalculateLatency(qwRenderSubmitStart, qwRenderSubmitEnd),
        DriverProcessingTime = CalculateLatency(qwDriverStart, qwDriverEnd),
        GpuRenderTime = data.GpuTimeMs,
        PresentTime = data.PresentLatencyMs,
        TotalPipelineLatency = data.TotalLatencyMs
    };
}
```

## Memory Access Patterns

### Efficient Polling Strategy
```csharp
public void PollRTSSData()
{
    // Read header first to check if data changed
    uint frameCount = _accessor.ReadUInt32(headerOffset + lastFrameCountOffset);
    if (frameCount == _lastFrameCount) return; // No new data
    
    _lastFrameCount = frameCount;
    
    // Only scan active app slots (check PID != 0)
    for (int i = 0; i < 128; i++)
    {
        int baseOffset = APP_ARRAY_OFFSET + (i * 1024);
        uint pid = _accessor.ReadUInt32(baseOffset);
        if (pid == 0) continue; // Skip empty slots
        
        var data = ReadAppData(i);
        if (data != null)
            ProcessFrameData(data);
    }
}
```

### Process Targeting
```csharp
public RTSSFrameData? FindProcessData(uint targetPID)
{
    for (int i = 0; i < 128; i++)
    {
        int baseOffset = APP_ARRAY_OFFSET + (i * 1024);
        uint pid = _accessor.ReadUInt32(baseOffset);
        if (pid == targetPID)
            return ReadAppData(i);
    }
    return null;
}

public RTSSFrameData? FindProcessData(string processName)
{
    for (int i = 0; i < 128; i++)
    {
        var data = ReadAppData(i);
        if (data?.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase) == true)
            return data;
    }
    return null;
}
```

## Data Structures Summary

### Available Metrics
1. **Real-time FPS:** `1000000 / dwFrameTime`
2. **Period FPS:** `1000 * dwFrames / (dwTime1 - dwTime0)`
3. **Statistical FPS:** Min/Avg/Max from `dwStatFramerateXxx`
4. **Frame Times:** `dwFrameTime / 1000.0` (milliseconds)
5. **GPU Times:** `dwGpuFrameTime / 1000.0` (milliseconds)
6. **Latency Pipeline:** Full input-to-display breakdown
7. **1% Low FPS:** Derived from frame time percentiles
8. **0.1% Low FPS:** Derived from frame time percentiles

### Memory Layout Constants
```csharp
public static class RTSSMemoryOffsets
{
    public const int HEADER_SIZE = 72;
    public const int APP_ARRAY_OFFSET = 0x48;
    public const int APP_ENTRY_SIZE = 1024;
    public const int MAX_APPS = 128;
    
    // Per-app offsets
    public const int PROCESS_ID = 0;
    public const int PROCESS_NAME = 4;
    public const int FRAME_TIME = 280;
    public const int STAT_MIN_FPS = 304;
    public const int STAT_AVG_FPS = 308;
    public const int STAT_MAX_FPS = 312;
    public const int GPU_FRAME_TIME = 679;
    public const int INPUT_TIMESTAMP = 571;
    public const int PRESENT_START = 611;
    public const int PRESENT_END = 619;
    public const int GPU_START = 659;
    public const int GPU_END = 667;
}
```

## HWiNFO Sensors Shared Memory

HWiNFO provides comprehensive hardware monitoring data:

```cpp
// Memory name: "Global\HWiNFO_SENS_SM2"
// Access mutex: "Global\HWiNFO_SM2_MUTEX"
struct HWiNFOSharedMemory {
    DWORD dwSignature;           // "HWiS" if active, 'DEAD' when inactive
    DWORD dwVersion;             // Version number
    DWORD dwRevision;            // Revision number
    __time64_t poll_time;        // Last polling time
    
    DWORD dwOffsetOfSensorSection;  // Offset to sensor array
    DWORD dwSizeOfSensorElement;    // sizeof(HWiNFOSensorElement)
    DWORD dwNumSensorElements;      // Number of sensors
    
    DWORD dwOffsetOfReadingSection; // Offset to readings array
    DWORD dwSizeOfReadingElement;   // sizeof(HWiNFOReadingElement)  
    DWORD dwNumReadingElements;     // Number of readings
};

struct HWiNFOSensorElement {
    DWORD dwSensorID;                    // Unique sensor ID
    DWORD dwSensorInst;                  // Sensor instance
    char szSensorNameOrig[128];          // Original sensor name
    char szSensorNameUser[128];          // User-renamed sensor name
};

struct HWiNFOReadingElement {
    SENSOR_READING_TYPE tReading;        // Reading type (temp, voltage, etc.)
    DWORD dwSensorIndex;                 // Parent sensor index
    DWORD dwReadingID;                   // Unique reading ID
    char szLabelOrig[128];               // Original label
    char szLabelUser[128];               // User-renamed label
    char szUnit[16];                     // Units (°C, V, RPM, etc.)
    double Value;                        // Current value
    double ValueMin;                     // Minimum recorded
    double ValueMax;                     // Maximum recorded
    double ValueAvg;                     // Average value
};

// Reading types
enum SENSOR_READING_TYPE {
    SENSOR_TYPE_TEMP,        // Temperature
    SENSOR_TYPE_VOLT,        // Voltage
    SENSOR_TYPE_FAN,         // Fan speed
    SENSOR_TYPE_CURRENT,     // Current
    SENSOR_TYPE_POWER,       // Power
    SENSOR_TYPE_CLOCK,       // Clock frequency
    SENSOR_TYPE_USAGE,       // Usage percentage
    SENSOR_TYPE_OTHER        // Other types
};
```

## MSI Afterburner Hardware Monitoring (MAHM)

MSI Afterburner provides GPU-focused monitoring with frame rate metrics:

```cpp
struct MAHMSharedMemory {
    DWORD dwSignature;           // 'MAHM' (0x4D41484D)
    DWORD dwVersion;             // 0x00020000 (v2.0)
    DWORD dwHeaderSize;          // Header size
    DWORD dwNumEntries;          // Number of monitoring entries
    DWORD dwEntrySize;           // Size of each entry
    time_t time;                 // Last polling time
    DWORD dwNumGpuEntries;       // Number of GPU entries (v2.0+)
    DWORD dwGpuEntrySize;        // GPU entry size (v2.0+)
};

struct MAHMEntry {
    char szSrcName[MAX_PATH];           // Data source name
    char szSrcUnits[MAX_PATH];          // Units
    char szLocalizedSrcName[MAX_PATH];  // Localized name
    char szLocalizedSrcUnits[MAX_PATH]; // Localized units
    char szRecommendedFormat[MAX_PATH]; // Display format
    float data;                         // Current value
    float minLimit;                     // Graph minimum
    float maxLimit;                     // Graph maximum
    DWORD dwFlags;                      // Display flags
    DWORD dwGpu;                        // GPU index
    DWORD dwSrcId;                      // Source ID (see MONITORING_SOURCE_ID_*)
};

// Frame rate specific source IDs
#define MONITORING_SOURCE_ID_FRAMERATE                    0x00000050
#define MONITORING_SOURCE_ID_FRAMETIME                    0x00000051  
#define MONITORING_SOURCE_ID_FRAMERATE_MIN                0x00000052
#define MONITORING_SOURCE_ID_FRAMERATE_AVG                0x00000053
#define MONITORING_SOURCE_ID_FRAMERATE_MAX                0x00000054
#define MONITORING_SOURCE_ID_FRAMERATE_1DOT0_PERCENT_LOW  0x00000055  // 1% low
#define MONITORING_SOURCE_ID_FRAMERATE_0DOT1_PERCENT_LOW  0x00000056  // 0.1% low

// GPU monitoring source IDs
#define MONITORING_SOURCE_ID_GPU_TEMPERATURE              0x00000000
#define MONITORING_SOURCE_ID_CORE_CLOCK                   0x00000020
#define MONITORING_SOURCE_ID_MEMORY_CLOCK                 0x00000022
#define MONITORING_SOURCE_ID_GPU_USAGE                    0x00000030
#define MONITORING_SOURCE_ID_MEMORY_USAGE                 0x00000031
#define MONITORING_SOURCE_ID_GPU_VOLTAGE                  0x00000040
#define MONITORING_SOURCE_ID_FAN_SPEED                    0x00000010
#define MONITORING_SOURCE_ID_GPU_REL_POWER                0x00000060  // Relative power
#define MONITORING_SOURCE_ID_GPU_ABS_POWER                0x00000061  // Absolute power
```

## PresentMon Data Provider Structure

PresentMon Data Provider creates its own shared memory with comprehensive frame analysis:

```cpp
// Memory name: "PMDPSharedMemory"
struct PMDPSharedMemory {
    DWORD dwSignature;           // 'PMDP' (0x504D4450)
    DWORD dwVersion;             // 0x00020000 (v2.0)
    DWORD dwFrameArrEntrySize;   // sizeof(PMDP_FRAME_DATA)
    DWORD dwFrameArrOffset;      // Offset to frame array
    DWORD dwFrameArrSize;        // Array size (8192 entries)
    DWORD dwFrameCount;          // Total frames captured
    DWORD dwFramePos;            // Current position in ring buffer
    DWORD dwStatus;              // Current status (see PMDP_STATUS_*)
    
    PMDP_FRAME_DATA arrFrame[8192]; // Ring buffer of frame data
};

// Status codes
#define PMDP_STATUS_OK                    0
#define PMDP_STATUS_INIT_FAILED          1  
#define PMDP_STATUS_START_STREAM_FAILED  2
#define PMDP_STATUS_GET_FRAME_DATA_FAILED 3
```

### PresentMon Frame Data Structure
```cpp
struct PMDPFrameData {
    // V1 Data (Basic metrics)
    char Application[MAX_PATH];  // Process name
    uint32_t ProcessID;          // Process ID
    uint64_t SwapChainAddress;   // DirectX swap chain
    uint32_t Runtime;            // Graphics runtime (D3D11/12, etc.)
    int32_t SyncInterval;        // VSync setting
    uint32_t PresentFlags;       // Present() flags
    uint32_t Dropped;            // Frame dropped flag
    double TimeInSeconds;        // Frame timestamp
    double msInPresentAPI;       // Time in Present() call
    double msBetweenPresents;    // Frame time
    uint32_t AllowsTearing;      // Variable refresh rate
    uint32_t PresentMode;        // Presentation mode
    double msUntilRenderComplete; // Render completion time
    double msUntilDisplayed;     // Display latency
    double msBetweenDisplayChange; // Display change interval
    double msUntilRenderStart;   // Render start latency
    uint64_t qpcTime;            // High-precision timestamp
    double msSinceInput;         // Input latency
    double msGpuActive;          // GPU active time
    double msGpuVideoActive;     // GPU video processing time

    // V2 Data (Extended metrics)
    uint64_t CPUStart;           // CPU start timestamp
    double Frametime;            // Total frame time
    double CPUBusy;              // CPU busy time
    double CPUWait;              // CPU wait time  
    double GPULatency;           // GPU latency
    double GPUTime;              // GPU processing time
    double GPUBusy;              // GPU busy time
    double VideoBusy;            // Video processing busy time
    double GPUWait;              // GPU wait time
    double DisplayLatency;       // Display pipeline latency
    double DisplayedTime;        // Time when displayed
    double AnimationError;       // Animation smoothness error
    double ClickToPhotonLatency; // Total input-to-display latency
};
```

### PresentMon API Metrics (v3.0)

PresentMon API provides access to 100+ metrics through structured queries:

```cpp
// Key metrics available through PresentMon API
enum PMMetric {
    PM_METRIC_APPLICATION,                    // Application name
    PM_METRIC_DISPLAYED_FPS,                 // Displayed FPS
    PM_METRIC_PRESENTED_FPS,                 // Presented FPS  
    PM_METRIC_APPLICATION_FPS,               // Application FPS
    PM_METRIC_CPU_FRAME_TIME,                // CPU frame time
    PM_METRIC_GPU_TIME,                      // GPU time
    PM_METRIC_DISPLAY_LATENCY,               // Display latency
    PM_METRIC_CLICK_TO_PHOTON_LATENCY,       // Input latency
    PM_METRIC_GPU_POWER,                     // GPU power usage
    PM_METRIC_GPU_TEMPERATURE,               // GPU temperature
    PM_METRIC_GPU_UTILIZATION,               // GPU utilization
    PM_METRIC_GPU_MEM_USED,                  // GPU memory usage
    PM_METRIC_CPU_UTILIZATION,               // CPU utilization
    PM_METRIC_ANIMATION_ERROR,               // Frame pacing error
    PM_METRIC_FRAME_TYPE,                    // Frame type classification
    // ... 80+ more metrics
};

// Statistical processing options
enum PMStat {
    PM_STAT_AVG,                 // Average
    PM_STAT_PERCENTILE_99,       // 99th percentile
    PM_STAT_PERCENTILE_95,       // 95th percentile  
    PM_STAT_PERCENTILE_01,       // 1st percentile (1% low)
    PM_STAT_PERCENTILE_05,       // 5th percentile
    PM_STAT_MAX,                 // Maximum
    PM_STAT_MIN,                 // Minimum
    // ... more statistics
};
```

## Complete System Monitoring Architecture

This documentation reveals the complete ecosystem of performance monitoring tools available on Windows:

### Data Source Hierarchy
1. **RTSS** - Application-level frame metrics with graphics API hooks
2. **PresentMon** - ETW-based frame analysis with Windows integration  
3. **HWiNFO** - Hardware sensors (temperatures, voltages, power)
4. **MSI Afterburner** - GPU-focused monitoring with frame rate statistics

### Integration Strategy
- **Primary FPS Source**: RTSS hooks (anti-cheat compatible) → PresentMon ETW (fallback)
- **Hardware Monitoring**: HWiNFO (comprehensive) → MSI Afterburner (GPU-focused)
- **Latency Analysis**: PresentMon API (complete pipeline) → RTSS timestamps
- **Statistical Processing**: All sources provide rolling windows for percentile calculations

This multi-source approach ensures comprehensive system monitoring with redundancy for anti-cheat compatibility.

## Additional Memory Access Patterns

### Unified Data Reader
```csharp
public class UnifiedPerformanceMonitor
{
    private RTSSReader _rtssReader;
    private PMDPReader _pmdpReader; 
    private HWiNFOReader _hwinfoReader;
    private MAHMReader _mahmReader;
    
    public PerformanceSnapshot GetCompleteSnapshot(uint processId)
    {
        return new PerformanceSnapshot
        {
            // Frame metrics (RTSS primary, PMDP fallback)
            FPS = _rtssReader.GetFPS(processId) ?? _pmdpReader.GetFPS(processId),
            FrameTime = _rtssReader.GetFrameTime(processId) ?? _pmdpReader.GetFrameTime(processId),
            OnePercentLow = _rtssReader.Get1PercentLow(processId) ?? CalculateFromPMDP(processId),
            
            // Hardware metrics (HWiNFO primary, MAHM fallback)
            GPUTemp = _hwinfoReader.GetGPUTemperature() ?? _mahmReader.GetGPUTemperature(),
            GPUUsage = _hwinfoReader.GetGPUUsage() ?? _mahmReader.GetGPUUsage(),
            GPUPower = _hwinfoReader.GetGPUPower() ?? _mahmReader.GetGPUPower(),
            
            // Advanced metrics (PresentMon exclusive)
            InputLatency = _pmdpReader.GetInputLatency(processId),
            DisplayLatency = _pmdpReader.GetDisplayLatency(processId),
            AnimationError = _pmdpReader.GetAnimationError(processId)
        };
    }
}
```

This documentation covers the complete Windows performance monitoring ecosystem and provides practical implementation guidance for accessing all available metrics.