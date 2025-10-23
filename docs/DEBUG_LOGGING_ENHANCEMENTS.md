# Debug Logging Enhancements & RTSS Analysis (v1.1.4)

## Changes Made

### 1. Moved All Debug Output to Plugin Debug Log
**Objective**: Keep InfoPanel console clean, centralize all plugin debugging to `plugin-debug.log`

**Changes**:
- Replaced all `Console.WriteLine()` statements with `_fileLogger?.LogXxx()` calls
- Enhanced error handling to use file logging consistently
- Removed duplicate logging (both console and file) to single file logging

### 2. Comprehensive RTSS Field Analysis
**Objective**: Dump all available RTSS fields to understand what data is actually available

**Added extensive field reading**:
```csharp
// Basic fields
var offset264 = Marshal.ReadInt32(mapView, entryOffset + 264); // dwFlags
var offset268 = Marshal.ReadInt32(mapView, entryOffset + 268); // dwTime0
var offset272 = Marshal.ReadInt32(mapView, entryOffset + 272); // dwTime1  
var offset276 = Marshal.ReadInt32(mapView, entryOffset + 276); // dwFrames
var offset280 = Marshal.ReadInt32(mapView, entryOffset + 280); // dwStatFlags
var offset284 = Marshal.ReadInt32(mapView, entryOffset + 284); // dwFrameTime

// Legacy frame time fields (v2.0+)
var offset288 = Marshal.ReadInt32(mapView, entryOffset + 288); // Original frameTimeLow
var offset292 = Marshal.ReadInt32(mapView, entryOffset + 292); // Original frameTimeAvg
var offset296 = Marshal.ReadInt32(mapView, entryOffset + 296); // Original frameTimeMax
var offset300 = Marshal.ReadInt32(mapView, entryOffset + 300); // dwFrameTimeCount

// Statistical FPS fields
var offset304 = Marshal.ReadInt32(mapView, entryOffset + 304); // dwStatFramerateMin
var offset308 = Marshal.ReadInt32(mapView, entryOffset + 308); // dwStatFramerateAvg
var offset312 = Marshal.ReadInt32(mapView, entryOffset + 312); // dwStatFramerateMax

// Advanced fields (v2.5+, v2.13+, v2.20+, v2.21+)
// frameTimeMin, frameTimeAvg, frameTimeMax, frameTimeCount (v2.5+)
// framerate1PercentLow, framerate0Point1PercentLow (v2.13+)
// width, height (v2.20+)
// gpuFrameTime, gpuActiveRenderTime (v2.21+)
```

**Debug output structure**:
```
=== RTSS Field Dump for PID {processId} ===
Basic: Flags@264={flags:X}, Time0@268={time0}, Time1@272={time1}, Frames@276={frames}
Stats: StatFlags@280={statFlags}, FrameTime@284={frameTime}, Count@300={count}
Legacy: FTLow@288={ftLow}, FTAvg@292={ftAvg}, FTMax@296={ftMax}
StatFPS: Min@304={minFps}, Avg@308={avgFps}, Max@312={maxFps}
New v2.5+: MinFT@516={minFT}, AvgFT@520={avgFT}, MaxFT@524={maxFT}, Count@528={count}
New v2.13+: 1%Low@536={1pLow}, 0.1%Low@540={01pLow}
New v2.20+: ResX@564={resX}, ResY@568={resY}
New v2.21+: GPUFrame@612={gpuFrame}, GPUActive@608={gpuActive}
```

### 3. Enhanced 1% Low Calculation Logic
**Objective**: Always provide meaningful 1% low values using best available data source

**Strategy**:
- Always update plugin's internal frame buffer for backup calculation
- Use RTSS native 1% low when available (> 0)
- Fall back to plugin's calculated 1% low when RTSS returns 0
- Log which method is being used for transparency

```csharp
// Always update our frame buffer, use RTSS if available
UpdateFrameTimeBuffer(_currentFrameTime);
var calculated1PercentLow = Calculate1PercentLow();

if (hookedProcess.OnePercentLowFps > 0)
{
    _current1PercentLow = hookedProcess.OnePercentLowFps;
    _fileLogger?.LogDebug($"Using RTSS native 1% low: {hookedProcess.OnePercentLowFps:F1} FPS (calculated: {calculated1PercentLow:F1})");
}
else
{
    _current1PercentLow = calculated1PercentLow;
    _fileLogger?.LogDebug($"Using calculated 1% low: {calculated1PercentLow:F1} FPS (RTSS: 0)");
}
```

## Analysis of Current Issues

### RTSS Field Status from Debug Logs:
```
PID 6696 RTSS v2.21 - StatFlags: 2231, FrameTimeCount: 0
PID 6696 New offsets - 1%Low@536: 0, 0.1%Low@540: 0, MinFT@516: 0, MaxFT@524: 0
PID 6696 Old offsets - @288: 0, @292: 0, @296: 0
```

### Key Findings:
1. **RTSS Version**: 2.21 (0x00020015) - Latest version with all features
2. **Statistical Status**: StatFlags = 2231 (0x8B7) - Statistics enabled
3. **Frame Count**: FrameTimeCount = 0 - No statistical frames captured yet
4. **All Advanced Fields**: Returning 0 - Waiting for statistical accumulation

### Root Cause:
RTSS statistical fields require a **minimum accumulation period** before returning meaningful data. The `FrameTimeCount: 0` indicates RTSS hasn't captured enough frames for statistical analysis yet.

## Expected Behavior After Changes

### Immediate Results:
1. **Clean InfoPanel Console**: No more plugin debug spam
2. **Rich Plugin Debug Log**: Comprehensive RTSS field analysis
3. **Working 1% Low**: Plugin's calculated 1% low until RTSS provides data
4. **Diagnostic Information**: Clear indication of data sources used

### Long-term Results (after RTSS accumulates data):
1. **Native RTSS 1% Low**: Transition to RTSS native calculation when available
2. **0.1% Low Values**: Appear once RTSS builds sufficient statistical buffer
3. **All Advanced Metrics**: Populate as RTSS statistical engine provides data

## Debug Log Analysis Points

### What to Look For:
1. **RTSS Field Dump**: Which fields are non-zero over time
2. **1% Low Source**: Whether using "RTSS native" or "calculated"
3. **FrameTimeCount**: When this becomes > 0, RTSS stats should activate
4. **StatFlags Changes**: Any changes in statistical status flags

### Troubleshooting Guide:
- **All zeros**: RTSS needs more time to accumulate statistical data
- **FrameTimeCount > 0 but advanced fields still 0**: Possible RTSS configuration issue
- **StatFlags = 0**: RTSS statistics disabled in configuration
- **Legacy fields populated**: Older RTSS compatibility mode active

## Testing Recommendations

1. **Launch game and immediately check**: Should see calculated 1% low values
2. **Play for 30+ seconds**: Monitor FrameTimeCount increasing
3. **Look for transition**: Watch for switch from "calculated" to "RTSS native" 1% low
4. **Extended play (2+ minutes)**: Check for 0.1% low appearance
5. **Compare with RTSS OSD**: Validate accuracy of displayed values

The comprehensive field dumping will reveal exactly what RTSS is providing and when, enabling precise diagnosis of any remaining issues.