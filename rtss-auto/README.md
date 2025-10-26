# MAHMMonitor

Hardware sensor monitoring tool using MSI Afterburner shared memory.

## Purpose

Real-time monitoring of CPU/GPU temperatures, usage, clocks, voltages, and power via the MAHM (MSI Afterburner Hardware Monitor) shared memory interface.

## Usage

```powershell
.\MAHMMonitor.exe
```

## Features

- ✅ CPU temperature monitoring
- ✅ GPU temperature, usage, power
- ✅ Memory clock speeds
- ✅ Voltage monitoring
- ✅ Fan speeds (RPM & %)
- ✅ Power consumption (Watts)
- ✅ Framerate and frame time
- ✅ 1% and 0.1% FPS lows

## Build

```powershell
cl.exe /nologo /O2 /W3 /EHsc /I.. /Fe:MAHMMonitor.exe MAHMMonitor.cpp /link kernel32.lib user32.lib
```

## Requirements

- MSI Afterburner running (shares hardware data via MAHMSharedMemory)
- Works alongside # RTSS Frame Time Statistics Monitor

## 📊 Real-Time Frame Time Analysis Tool

Automatically monitor frame time statistics from any 3D game/application using RivaTuner Statistics Server (RTSS) shared memory.

### ⚡ Key Feature
**Automatic Benchmark Mode**: No manual RTSS configuration needed! The tool automatically enables benchmark mode for every game session.

---

## 📚 Documentation

Choose the guide that fits your needs:

### 🚀 **New User?** → [QUICKSTART.md](QUICKSTART.md)
Get running in 3 steps. Includes troubleshooting and real-world examples.

### 📖 **Want Details?** → [DOCUMENTATION.md](DOCUMENTATION.md)
Complete guide: building, architecture, testing results, and future enhancements.

### 🔧 **Developer?** → [TECHNICAL_REFERENCE.md](TECHNICAL_REFERENCE.md)
Quick offset reference, code snippets, and common pitfalls.

### 📋 **Project Overview?** → [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)
Development history, key discoveries, and testing validation.

---

## 🎯 Quick Start

```powershell
# 1. Navigate to folder
cd "c:\Program Files (x86)\RivaTuner Statistics Server\SDK\Samples\SharedMemory\RTSSSharedMemorySample\dump\MAHMMonitor"

# 2. Run the monitor
.\rtss-auto.exe

# 3. Launch your game
# Statistics will appear automatically!
```

---

## 📈 What You Get

```
=== Frame Time Statistics ===
Sample count: 1024 / 1024

Summary:
  Average FPS:     136.1
  1% Low FPS:      123.1  ← Most important for smoothness!
  0.1% Low FPS:     32.6
```

### Why 1% Low FPS Matters ⭐
- Shows **real-world smoothness** (99th percentile)
- High 1% Low = smooth gameplay
- Low 1% Low = stuttery experience
- **Target**: Within 80-90% of average FPS

---

## ✅ Requirements

- ✅ Windows 10/11
- ✅ RTSS 7.3.x or later (installed and running)
- ✅ Visual Studio 2022 (for compilation only)

---

## 🛠️ Building from Source

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\Launch-VsDevShell.ps1" -Arch amd64 -SkipAutomaticLocation
cl.exe /nologo /O2 /W3 /EHsc /I../.. /Fe:rtss-auto.exe rtss-auto.cpp /link kernel32.lib user32.lib
```

---

## 🔍 Key Discovery

**Problem**: RTSS frame time statistics only work when benchmark mode is manually enabled.

**Solution**: This tool **automatically enables benchmark mode** via shared memory writes:
```cpp
DWORD* pStatFlags = (DWORD*)(pAppBytes + 284);
*pStatFlags |= STATFLAG_RECORD;  // 0x00000001
```

**Tested**: Flag resets on app close, so the tool re-enables per session automatically.

---

## 📁 Files

| File | Description |
|------|-------------|
| `rtss-auto.exe` | Ready-to-use executable (180 KB) |
| `rtss-auto.cpp` | Source code (14 KB) |
| `rtss-auto.log` | Runtime log (created automatically) |
| `QUICKSTART.md` | User guide (5 minutes) |
| `DOCUMENTATION.md` | Complete documentation (19 KB) |
| `TECHNICAL_REFERENCE.md` | Developer reference (10 KB) |
| `PROJECT_SUMMARY.md` | Project overview (11 KB) |

---

## 🎮 Usage Example

### Testing Graphics Settings
1. **Baseline**: Run rtss-auto.exe, play 2 minutes, note 1% Low FPS
2. **Change**: Increase graphics quality
3. **Test**: Play 2 minutes again
4. **Compare**: If 1% Low drops >20%, settings may be too high

---

## 📊 Interpreting Results

| Metric | What It Shows | Importance |
|--------|---------------|------------|
| Average FPS | Mean framerate | ⭐ Baseline only |
| 1% Low FPS | 99% of frames faster | ⭐⭐⭐ **Most important** |
| 0.1% Low FPS | Worst 0.1% of frames | ⭐⭐ Extreme stutters |

**Rule of Thumb**:
- 1% Low within 10-20% of average → Smooth
- 1% Low < 70% of average → Stuttery

---

## 🐛 Troubleshooting

**"No 3D application detected"**  
→ Ensure RTSS is running and injecting (check for OSD in-game)

**"Benchmark mode NOT enabled" repeats**  
→ Run as Administrator

**All statistics are zero**  
→ Wait 2-3 seconds for buffer to populate

See [QUICKSTART.md](QUICKSTART.md#troubleshooting) for more help.

---

## 🧪 Testing Validation

✅ **Multi-Session Test**: NMS → Forever Winter → NMS (confirmed flag reset behavior)  
✅ **Accuracy Test**: Statistics match RTSS OSD within 5%  
✅ **Performance Test**: <1ms enable delay (zero user-visible impact)  

See [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md#testing-validation) for detailed results.

---

## 🚀 Version History

**v1.0** (October 26, 2025)
- ✅ Automatic benchmark mode enablement
- ✅ Real-time statistics (min/max/avg/percentiles)
- ✅ Comprehensive logging
- ✅ Per-session auto-enable
- ✅ Tested with RTSS 7.3.x

---

## 📝 License

Educational/analysis use. RTSS SDK usage follows RivaTuner licensing terms.

---

## 🔗 Resources

- **RTSS Download**: https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/
- **RTSS SDK**: Included in RTSS installation (`SDK\Samples\SharedMemory\`)

---

**Last Updated**: October 26, 2025  
**Version**: 1.0  
**Status**: ✅ Production Ready

## Sensor IDs

Predefined sensor constants in `MAHMSharedMemory.h`:
- `MONITORING_SOURCE_ID_GPU_TEMPERATURE`
- `MONITORING_SOURCE_ID_GPU_USAGE`
- `MONITORING_SOURCE_ID_GPU_POWER`
- `MONITORING_SOURCE_ID_FRAMERATE`
- And many more...

## Output Example

```
GPU Temp: 65°C
GPU Usage: 98%
GPU Power: 285W
FPS: 150.0
Frame Time: 6.67ms
1% Low: 135.0 FPS
```

## Use Cases

1. **System Monitoring**: Track hardware metrics during gaming
2. **Temperature Logging**: Monitor thermal performance
3. **Power Analysis**: Track GPU power consumption
4. **Performance Correlation**: Compare FPS with hardware load

## Documentation

See `../docs/SDK_HEADER_ANALYSIS.md` for complete sensor ID reference and structure details.

## Related Tools

- **BackgroundMonitor**: Game performance monitoring
- **MAHM_TEST_RESULTS.md**: Test results and sensor validation
