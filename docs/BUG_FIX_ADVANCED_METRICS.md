# Bug Fix: Advanced Metrics Issues (v1.1.4)

## Issues Identified

Based on real-world testing with No Man's Sky (Vulkan), three critical bugs were identified in the advanced metrics system:

### 1. 1% Low FPS Inconsistency
- **Issue**: 1% low values switching between 0 and actual values inconsistently
- **Root Cause**: Using wrong RTSS field offset (288 instead of 536) for native 1% low FPS
- **Impact**: Unreliable 1% low measurements, confusing users

### 2. 0.1% Low FPS Always Zero
- **Issue**: 0.1% low FPS sensor permanently showing 0 regardless of game performance
- **Root Cause**: ZeroPointOnePercentLowFps field never populated from RTSS data
- **Impact**: Missing critical performance metric for competitive gaming analysis

### 3. Min FPS Always Zero
- **Issue**: Min FPS sensor showing 0 instead of actual minimum frame rate
- **Root Cause**: Min FPS not calculated from RTSS frame time statistics in enhanced sensor updates
- **Impact**: Incomplete frame rate analysis, missing important performance bottleneck indicator

## Fixes Applied

### 1. Corrected RTSS Field Offsets (RTSSOnlyMonitoringService.cs)

**Before**: Reading incorrect/intermediate frame time fields
```csharp
var frameTimeLow = Marshal.ReadInt32(mapView, entryOffset + 288); // Wrong field
var frameTimeAvg = Marshal.ReadInt32(mapView, entryOffset + 292); // Wrong units (0.1ms)
var frameTimeMax = Marshal.ReadInt32(mapView, entryOffset + 296); // Wrong units (0.1ms)
```

**After**: Reading correct RTSS v2.13+ statistical fields
```csharp
// Frame time statistics (v2.5+) - microseconds
var frameTimeMin = Marshal.ReadInt32(mapView, entryOffset + 516); // dwStatFrameTimeMin
var frameTimeAvg = Marshal.ReadInt32(mapView, entryOffset + 520); // dwStatFrameTimeAvg  
var frameTimeMax = Marshal.ReadInt32(mapView, entryOffset + 524); // dwStatFrameTimeMax

// Native 1% and 0.1% low FPS (v2.13+)
var framerate1PercentLow = Marshal.ReadInt32(mapView, entryOffset + 536); // dwStatFramerate1Dot0PercentLow
var framerate0Point1PercentLow = Marshal.ReadInt32(mapView, entryOffset + 540); // dwStatFramerate0Dot1PercentLow

// Resolution (v2.20+)
var width = Marshal.ReadInt32(mapView, entryOffset + 564); // dwResolutionX
var height = Marshal.ReadInt32(mapView, entryOffset + 568); // dwResolutionY

// GPU timing (v2.21+)
var gpuFrameTime = Marshal.ReadInt32(mapView, entryOffset + 612); // dwGpuFrameTime
var gpuActiveRenderTime = Marshal.ReadInt32(mapView, entryOffset + 608); // dwGpuActiveRenderTime
```

### 2. Fixed Unit Conversions

**Before**: Incorrect unit assumptions
```csharp
FrameTimeMs = frameTime > 0 ? frameTime / 10.0 : 1000.0 / fps, // Wrong: assumed 0.1ms units
OnePercentLowFps = frameTimeLow > 0 ? 10000.0 / frameTimeLow : 0, // Wrong field entirely
```

**After**: Correct microsecond to millisecond conversion
```csharp
FrameTimeMs = frameTime > 0 ? frameTime / 1000.0 : 1000.0 / fps, // Correct: microseconds to ms
OnePercentLowFps = framerate1PercentLow > 0 ? framerate1PercentLow : 0, // Direct FPS value
ZeroPointOnePercentLowFps = framerate0Point1PercentLow > 0 ? framerate0Point1PercentLow : 0, // Direct FPS value
MinFrameTimeMs = frameTimeMin > 0 ? frameTimeMin / 1000.0 : 0, // Microseconds to ms
MaxFrameTimeMs = frameTimeMax > 0 ? frameTimeMax / 1000.0 : 0, // Microseconds to ms
```

### 3. Added Min FPS Calculation (SensorManagementService.cs)

**Added**: Min FPS calculation in enhanced sensor updates
```csharp
// Calculate min FPS from max frame time (inverse relationship)
_minFpsSensor.Value = candidate.MaxFrameTimeMs > 0 ? (float)(1000.0 / candidate.MaxFrameTimeMs) : 0;
```

### 4. Enhanced Debug Logging

**Added**: Detailed RTSS metrics logging for troubleshooting
```csharp
_fileLogger?.LogDebugThrottled($"PID {processId} RTSS metrics - 1%Low: {framerate1PercentLow}, 0.1%Low: {framerate0Point1PercentLow}, FrameTimeMin: {frameTimeMin}, FrameTimeMax: {frameTimeMax}, GPU: {gpuFrameTime}", $"rtss_metrics_{processId}");
```

## RTSS Shared Memory Structure Reference

Based on RTSSSharedMemory.h analysis:

### Key Field Offsets (from app entry base):
- **264**: dwFlags (graphics API detection)
- **280**: dwStatFlags (statistics status)
- **284**: dwFrameTime (microseconds, per-frame)
- **516**: dwStatFrameTimeMin (microseconds, statistical minimum)
- **520**: dwStatFrameTimeAvg (microseconds, statistical average)
- **524**: dwStatFrameTimeMax (microseconds, statistical maximum)
- **536**: dwStatFramerate1Dot0PercentLow (FPS, native 1% low)
- **540**: dwStatFramerate0Dot1PercentLow (FPS, native 0.1% low)
- **564**: dwResolutionX (v2.20+)
- **568**: dwResolutionY (v2.20+)
- **608**: dwGpuActiveRenderTime (microseconds, v2.21+)
- **612**: dwGpuFrameTime (microseconds, v2.21+)

### Version Requirements:
- **v2.5+**: Frame time statistics (min/avg/max)
- **v2.13+**: Native 1% and 0.1% low FPS calculations
- **v2.20+**: Native resolution detection
- **v2.21+**: GPU timing metrics

## Testing Results Expected

With these fixes, the following behavior should be observed:

1. **1% Low FPS**: Consistent, non-zero values when RTSS has sufficient frame data
2. **0.1% Low FPS**: Proper values for games with extended play sessions (requires statistical build-up)
3. **Min FPS**: Calculated as inverse of maximum frame time, providing accurate minimum performance indicator
4. **Debug Logs**: Detailed RTSS field values for advanced troubleshooting

## Validation Steps

1. Launch game with RTSS hooking enabled
2. Monitor debug logs for "RTSS metrics" entries showing non-zero advanced values
3. Verify InfoPanel displays:
   - Consistent 1% low FPS values
   - Non-zero 0.1% low after extended gameplay
   - Proper min FPS calculations
   - Accurate GPU timing (if supported by RTSS version)

## Notes

- 0.1% low requires longer statistical periods to become meaningful
- Min FPS reflects worst-case frame delivery (1/max_frame_time)
- GPU timing requires RTSS v2.21+ and compatible graphics drivers
- All advanced metrics depend on RTSS statistical data accumulation