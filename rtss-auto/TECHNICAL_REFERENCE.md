# RTSS Technical Reference Card

## Quick Offset Reference

### Per-Application Entry (RTSS_SHARED_MEMORY_APP_ENTRY)
Base offset from `pMem->dwAppArrOffset + (dwAppArrIndex * dwAppEntrySize)`

| Field Name | Offset | Type | Size | Description |
|------------|--------|------|------|-------------|
| `dwStatFlags` | 284 | DWORD | 4 bytes | Benchmark mode control flags |
| `dwStatFrameTimeBuf` | 5080 | DWORD[1024] | 4096 bytes | Frame time buffer (microseconds) |
| `dwStatFrameTimeBufPos` | 9176 | DWORD | 4 bytes | Current buffer write position |
| `dwStatFrameTimeBufFramerate` | 9180 | DWORD | 4 bytes | Framerate at buffer position |
| `dwStatFramerate1Dot0PercentLow` | 9548 | DWORD | 4 bytes | 1% Low FPS × 10 |
| `dwStatFramerate0Dot1PercentLow` | 9552 | DWORD | 4 bytes | 0.1% Low FPS × 10 |

---

## Flag Constants

```cpp
#define STATFLAG_RECORD 0x00000001  // Enable frame time recording
```

### Flag Operations
```cpp
// Read current flags
DWORD* pStatFlags = (DWORD*)(pAppBytes + 284);
DWORD currentFlags = *pStatFlags;

// Check if benchmark mode enabled
bool isEnabled = (currentFlags & STATFLAG_RECORD) != 0;

// Enable benchmark mode
*pStatFlags = currentFlags | STATFLAG_RECORD;

// Disable benchmark mode
*pStatFlags = currentFlags & ~STATFLAG_RECORD;
```

---

## Shared Memory Access

### Opening with Write Permission
```cpp
HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");
LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;
```

**CRITICAL**: Must use `FILE_MAP_ALL_ACCESS` for both `OpenFileMapping` and `MapViewOfFile` to enable writes!

### Read-Only Access
```cpp
HANDLE hMapFile = OpenFileMapping(FILE_MAP_READ, FALSE, "RTSSSharedMemoryV2");
LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_READ, 0, 0, 0);
```

---

## 3D Application Detection

### RTSS v2.10+ (Current)
```cpp
bool is3DApp = (pAppEntry->dwTime0 != 0 || 
                pAppEntry->dwTime1 != 0 || 
                pAppEntry->dwFrames != 0);
```

### RTSS v2.09 and Earlier (Legacy - DO NOT USE)
```cpp
// API flags are all 0x0 in v2.10+, unreliable!
bool is3DApp = (pAppEntry->dwFlags & APPFLAG_API_MASK) != 0;
```

**Note**: In RTSS v2.10+, API detection flags (`APPFLAG_D3D8`, `APPFLAG_D3D9`, etc.) all show `0x0`. Use frame timing values instead.

---

## Frame Time Buffer

### Buffer Structure
- **Size**: 1024 DWORDs (4096 bytes)
- **Type**: Circular buffer
- **Units**: Microseconds (μs)
- **Position**: `dwStatFrameTimeBufPos` (0-1023)

### Reading Buffer
```cpp
DWORD* pFrameTimeBuf = (DWORD*)(pAppBytes + 5080);
DWORD bufferPos = *(DWORD*)(pAppBytes + 9176);

for (size_t i = 0; i < 1024; i++) {
    DWORD frameTimeMicros = pFrameTimeBuf[i];
    if (frameTimeMicros > 0) {
        float frameTimeMs = frameTimeMicros / 1000.0f;  // Convert to ms
        float fps = 1000.0f / frameTimeMs;               // Convert to FPS
    }
}
```

### Buffer Validation
```cpp
// Skip zero values (unused slots)
if (frameTimeMicros > 0 && frameTimeMicros < 1000000) {  // < 1 second (sanity check)
    // Valid frame time
}
```

---

## Pre-Calculated Percentiles

### Reading Values
```cpp
DWORD fps1PercentLow_x10 = *(DWORD*)(pAppBytes + 9548);
DWORD fps01PercentLow_x10 = *(DWORD*)(pAppBytes + 9552);

float fps1PercentLow = fps1PercentLow_x10 / 10.0f;
float fps01PercentLow = fps01PercentLow_x10 / 10.0f;
```

**IMPORTANT**: These values are **multiplied by 10** in shared memory. Divide by 10.0 to get actual FPS.

---

## Percentile Calculation (Manual)

### Algorithm
```cpp
std::vector<DWORD> frameTimes; // in microseconds
// ... fill with buffer data ...

// Sort ascending
std::sort(frameTimes.begin(), frameTimes.end());

// Calculate percentiles
size_t count = frameTimes.size();
size_t idx99 = static_cast<size_t>(count * 0.99);    // 99th percentile
size_t idx999 = static_cast<size_t>(count * 0.999);  // 99.9th percentile

DWORD frameTime99 = frameTimes[idx99];
DWORD frameTime999 = frameTimes[idx999];

// Convert to FPS
float fps1PercentLow = (frameTime99 > 0) ? (1000000.0f / frameTime99) : 0.0f;
float fps01PercentLow = (frameTime999 > 0) ? (1000000.0f / frameTime999) : 0.0f;
```

---

## Common Pitfalls

### 1. Benchmark Mode Required ⚠️
**ALL frame time statistics require benchmark mode enabled!**

Without `STATFLAG_RECORD`:
- `dwStatFrameTimeBuf` = all zeros
- `dwStatFramerate1Dot0PercentLow` = 0
- `dwStatFramerate0Dot1PercentLow` = 0

### 2. Flag Persistence ⚠️
**`dwStatFlags` RESETS to 0x00000000 when application closes!**

Must re-enable on every application launch (not persistent).

### 3. Read-Only Access ⚠️
**FILE_MAP_READ cannot write to shared memory!**

Attempting to write with read-only access causes silent failure (no exception). Always use `FILE_MAP_ALL_ACCESS` for writes.

### 4. Offset Calculation ⚠️
**Per-app offset calculation**:
```cpp
BYTE* pAppBytes = (BYTE*)pMem + pMem->dwAppArrOffset + (dwAppArrIndex * pMem->dwAppEntrySize);
```

**NOT**:
```cpp
// WRONG! This gives wrong offsets
RTSS_SHARED_MEMORY_APP_ENTRY* pEntry = &pMem->appEntry[dwAppArrIndex];
```

### 5. API Flags in v2.10+ ⚠️
**Do NOT use `APPFLAG_D3D8/9/10/11/12` for detection in RTSS v2.10+!**

All API flags show `0x0` in modern RTSS versions. Use frame timing values (`dwTime0/1/Frames`) instead.

---

## Version-Specific Behavior

### RTSS v2.10+ (Current)
- API flags: All `0x0` (unreliable)
- Detection: Use `dwTime0/1/Frames`
- Shared memory version: `0x00020015`

### RTSS v2.09 and Earlier (Legacy)
- API flags: Reliable
- Detection: Use `dwFlags & APPFLAG_API_MASK`
- Shared memory version: `< 0x00020015`

---

## Compilation Reference

### MSVC Command Line
```cmd
cl.exe /nologo /O2 /W3 /EHsc /I<SDK_PATH> /Fe:output.exe source.cpp /link kernel32.lib user32.lib
```

### Flags
- `/O2`: Optimize for speed
- `/W3`: Warning level 3
- `/EHsc`: C++ exception handling (standard)
- `/I<path>`: Include directory (for RTSSSharedMemory.h)
- `/Fe:<name>`: Output executable name
- `/link <libs>`: Linker libraries

### Required Libraries
- `kernel32.lib`: Windows kernel functions (OpenFileMapping, etc.)
- `user32.lib`: Windows user functions (optional, for GUI apps)

---

## Shared Memory Structure Reference

### RTSS_SHARED_MEMORY (Base Structure)
```cpp
typedef struct _RTSS_SHARED_MEMORY {
    DWORD dwSignature;       // 'RTSS' (0x52545353)
    DWORD dwVersion;         // e.g., 0x00020015
    DWORD dwAppArrOffset;    // Offset to application array
    DWORD dwAppArrSize;      // Number of app entries (max 256)
    DWORD dwAppEntrySize;    // Size of each entry in bytes
    // ... more fields ...
};
```

### RTSS_SHARED_MEMORY_APP_ENTRY (Per-Application)
```cpp
// Access via manual offset calculation
BYTE* pAppBytes = (BYTE*)pMem + pMem->dwAppArrOffset + (index * pMem->dwAppEntrySize);

// Then access fields:
char* szName = (char*)(pAppBytes + 0);                  // Name offset 0
DWORD dwTime0 = *(DWORD*)(pAppBytes + 256);             // Timing fields
DWORD* pStatFlags = (DWORD*)(pAppBytes + 284);          // Benchmark flag
DWORD* pFrameBuf = (DWORD*)(pAppBytes + 5080);          // Frame buffer
// ... etc
```

---

## Performance Metrics Interpretation

### Average FPS
- **What**: Mean framerate
- **Use**: General performance baseline
- **Limitation**: Hides stutters (single slow frame averages out)

### 1% Low FPS (99th Percentile) ⭐
- **What**: 99% of frames are faster than this
- **Use**: **Best indicator of smoothness**
- **Target**: Should be within 10-20% of average for smooth gameplay

### 0.1% Low FPS (99.9th Percentile)
- **What**: 99.9% of frames are faster than this (worst 0.1%)
- **Use**: Extreme stutter detection
- **Note**: Single frames, less representative than 1% Low

### Example Analysis
```
Average: 144 FPS
1% Low:  130 FPS   ← 90% of average = smooth
0.1% Low: 45 FPS   ← Occasional stutter (acceptable)
```

```
Average: 100 FPS
1% Low:   30 FPS   ← 30% of average = very stuttery!
0.1% Low: 15 FPS   ← Severe stutters
```

---

## Code Snippets

### Complete Example: Enable Benchmark Mode
```cpp
// Open with write access
HANDLE hMap = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");
LPVOID pAddr = MapViewOfFile(hMap, FILE_MAP_ALL_ACCESS, 0, 0, 0);
LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pAddr;

// Find first 3D app
for (DWORD i = 0; i < pMem->dwAppArrSize; i++) {
    BYTE* pAppBytes = (BYTE*)pMem + pMem->dwAppArrOffset + (i * pMem->dwAppEntrySize);
    
    DWORD dwTime0 = *(DWORD*)(pAppBytes + 256);
    DWORD dwTime1 = *(DWORD*)(pAppBytes + 260);
    DWORD dwFrames = *(DWORD*)(pAppBytes + 268);
    
    if (dwTime0 != 0 || dwTime1 != 0 || dwFrames != 0) {
        // Found 3D app, enable benchmark mode
        DWORD* pStatFlags = (DWORD*)(pAppBytes + 284);
        *pStatFlags |= STATFLAG_RECORD;
        printf("Benchmark mode enabled!\n");
        break;
    }
}
```

---

## Testing Checklist

Before deployment:
- [ ] Test with at least 2 different games
- [ ] Verify auto-enable works on fresh launch
- [ ] Check flag resets after game close
- [ ] Validate statistics match RTSS OSD (±5%)
- [ ] Test on both Windows 10 and 11
- [ ] Verify log file format is parseable
- [ ] Run as admin (test permission requirements)

---

**Version**: 1.0  
**Last Updated**: October 26, 2025  
**Full Documentation**: DOCUMENTATION.md
