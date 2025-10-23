# Advanced Metrics Hybrid Fix (v1.1.4)

## Issue Analysis

The debug logs revealed that RTSS v2.21 is returning all zeros for the advanced statistical fields:
```
PID 49940 RTSS metrics - 1%Low: 0, 0.1%Low: 0, FrameTimeMin: 0, FrameTimeMax: 0, GPU: 0
```

This indicates either:
1. RTSS statistical data hasn't accumulated enough samples yet
2. The specific RTSS configuration doesn't enable these statistics
3. The fields require a minimum time period before becoming valid

## Hybrid Solution Implemented

### Enhanced Debug Logging
Added comprehensive field inspection to compare old vs new offsets:
```csharp
// Check both old and new offset ranges
var offset288 = Marshal.ReadInt32(mapView, entryOffset + 288); // Original frameTimeLow  
var offset292 = Marshal.ReadInt32(mapView, entryOffset + 292); // Original frameTimeAvg
var offset296 = Marshal.ReadInt32(mapView, entryOffset + 296); // Original frameTimeMax

_fileLogger?.LogDebugThrottled($"PID {processId} RTSS v2.21 - StatFlags: {statFlags}, FrameTimeCount: {frameTimeCount}", $"rtss_stats_{processId}");
_fileLogger?.LogDebugThrottled($"PID {processId} New offsets - 1%Low@536: {framerate1PercentLow}, 0.1%Low@540: {framerate0Point1PercentLow}, MinFT@516: {frameTimeMin}, MaxFT@524: {frameTimeMax}", $"rtss_new_{processId}");
_fileLogger?.LogDebugThrottled($"PID {processId} Old offsets - @288: {offset288}, @292: {offset292}, @296: {offset296}", $"rtss_old_{processId}");
```

### Intelligent Fallback System
Implemented priority-based field selection:

#### 1% Low FPS Priority:
1. **Primary**: Native RTSS v2.13+ field (offset 536) - Direct FPS value
2. **Fallback**: Old offset 288 with conversion (frame time to FPS)
3. **Last Resort**: Plugin's own calculation from frame buffer

#### Frame Time Statistics Priority:
1. **Primary**: RTSS v2.5+ statistical fields (offsets 516-524) - Microsecond precision
2. **Fallback**: Original fields (offsets 292-296) - 0.1ms precision  
3. **Calculated**: Mathematical estimates when no data available

```csharp
// 1% Low with intelligent fallback
if (framerate1PercentLow > 0)
{
    onePercentLow = framerate1PercentLow; // Native RTSS calculation
}
else if (offset288 > 0) 
{
    onePercentLow = 10000.0 / offset288; // Convert old frame time format
}

// Frame time statistics with unit-aware conversion
if (frameTimeAvg > 0)
{
    avgFrameTimeMs = frameTimeAvg / 1000.0; // New: microseconds to ms
}
else if (offset292 > 0)
{
    avgFrameTimeMs = offset292 / 10.0; // Old: 0.1ms to ms
}
else
{
    avgFrameTimeMs = 1000.0 / fps; // Calculated from FPS
}
```

### Min Frame Time Estimation
When direct min frame time isn't available:
```csharp
if (frameTimeMin > 0)
{
    minFrameTimeMs = frameTimeMin / 1000.0; // Direct from RTSS
}
else if (maxFrameTimeMs > 0)
{
    minFrameTimeMs = avgFrameTimeMs * 0.8; // Estimate as 80% of average
}
```

## Expected Behavior

### Immediate Benefits:
- **1% Low FPS**: Should now show values immediately using fallback calculation
- **Frame Time Stats**: Proper values from whichever RTSS field is populated
- **Min FPS**: Calculated from available frame time data

### Long-term Benefits:
- **Native RTSS Stats**: Will automatically switch to more accurate native calculations when RTSS accumulates sufficient data
- **0.1% Low**: Will appear once RTSS builds statistical buffer (typically after 30+ seconds of gameplay)

### Debug Information:
The enhanced logging will show:
- Statistical flags and frame count from RTSS
- Values from both old and new field offsets
- Which fallback path is being used

## Testing Strategy

1. **Immediate Test**: Launch game, should see 1% low values right away (fallback mode)
2. **Extended Test**: Play for 2+ minutes, should see transition to native RTSS values
3. **Log Analysis**: Check debug logs to see which fields populate first
4. **Cross-Validation**: Compare values against RTSS OSD to verify accuracy

## Compatibility Matrix

| RTSS Version | 1% Low Source | Frame Stats | 0.1% Low | Resolution | GPU Timing |
|--------------|---------------|-------------|----------|------------|-------------|
| v2.5+        | Fallback (288)| Native      | None     | None       | None        |
| v2.13+       | Native (536)  | Native      | Native   | None       | None        |
| v2.20+       | Native (536)  | Native      | Native   | Native     | None        |
| v2.21+       | Native (536)  | Native      | Native   | Native     | Native      |

Current RTSS: **v2.21** (should support all features once statistical data accumulates)