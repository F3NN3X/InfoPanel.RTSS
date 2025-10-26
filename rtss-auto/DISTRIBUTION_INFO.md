# RTSS Frame Time Monitor - Distribution Package

## 📦 Package Information

**Version**: 1.0  
**Release Date**: October 26, 2025  
**Package Size**: 331 KB (338,969 bytes)  
**Platform**: Windows 10/11 x64  

---

## 📁 Package Contents

### Core Files (289 KB)
- **rtss-auto.exe** (181 KB) - Ready-to-use executable
- **rtss-auto.cpp** (14 KB) - Source code
- **rtss-auto.obj** (94 KB) - Compiled object file

### Documentation (50 KB)
- **README.md** (7 KB) - Start here! Overview & quick links
- **QUICKSTART.md** (4 KB) - 5-minute user guide
- **DOCUMENTATION.md** (19 KB) - Complete technical documentation
- **TECHNICAL_REFERENCE.md** (10 KB) - Developer reference
- **PROJECT_SUMMARY.md** (10 KB) - Development history & testing

---

## 🚀 Quick Start (3 Steps)

### Step 1: Extract Package
Extract all files to a folder of your choice (e.g., `C:\Tools\rtss-auto\`)

### Step 2: Run the Monitor
```powershell
.\rtss-auto.exe
```

### Step 3: Launch Your Game
Statistics will appear automatically in the console!

---

## ⚙️ System Requirements

### Required
- ✅ **Windows 10 or 11** (x64)
- ✅ **RTSS 7.3.x or later** (installed and running)
  - Download: https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/

### Optional (for compilation only)
- ✅ **Visual Studio 2022** with C++ Desktop Development workload

---

## 📖 Documentation Guide

### New to this tool?
1. **Start with**: README.md (project overview)
2. **Then read**: QUICKSTART.md (setup in 5 minutes)
3. **If needed**: Check troubleshooting section

### Want to modify the code?
1. **Reference**: TECHNICAL_REFERENCE.md (offsets, snippets)
2. **Deep dive**: DOCUMENTATION.md (architecture details)

### Project manager/technical lead?
1. **Overview**: PROJECT_SUMMARY.md (testing, validation)
2. **Details**: DOCUMENTATION.md (full technical guide)

---

## 🎯 What This Tool Does

### Automatic Frame Time Monitoring
Monitors frame time statistics from any 3D game/application using RTSS shared memory:
- ✅ **Average FPS** (mean framerate)
- ✅ **1% Low FPS** (99th percentile - smoothness indicator)
- ✅ **0.1% Low FPS** (99.9th percentile - worst stutters)

### Key Feature: Auto-Enable Benchmark Mode
**No manual RTSS configuration needed!** The tool automatically enables RTSS benchmark mode for every game session via shared memory writes.

---

## 📊 Sample Output

```
=== Frame Time Statistics ===
Sample count: 1024 / 1024

Frame Time (ms):
  Min:       7.34 (Max FPS: 136.2)
  Avg:       7.35 (Avg FPS: 136.1)
  Max:      30.68 (Min FPS:  32.6)
  99th%:     8.12 (1% Low: 123.1 FPS)
  99.9th%:  30.62 (0.1% Low:  32.6 FPS)

Summary:
  Average FPS:     136.1
  1% Low FPS:      123.1  ← Most important metric!
  0.1% Low FPS:     32.6
```

---

## 🔧 Building from Source

### Prerequisites
- Visual Studio 2022 with C++ Desktop Development
- RTSS SDK headers (included in RTSS installation)

### Compilation Command
```powershell
# Open VS Developer PowerShell, then:
cd <package_folder>
cl.exe /nologo /O2 /W3 /EHsc /I<RTSS_SDK_PATH> /Fe:rtss-auto.exe rtss-auto.cpp /link kernel32.lib user32.lib
```

**Note**: Replace `<RTSS_SDK_PATH>` with path to RTSS SDK Include folder (e.g., `C:\Program Files (x86)\RivaTuner Statistics Server\SDK\Samples\SharedMemory\RTSSSharedMemorySample\Include`)

---

## 🐛 Troubleshooting

### "No 3D application detected"
**Solution**: 
1. Verify RTSS is running (check system tray)
2. Launch a 3D game
3. Confirm RTSS OSD appears in-game

### "Benchmark mode NOT enabled" repeats
**First time? → Normal!** Tool auto-enables on every game launch.

**Every cycle? → Problem!**
- Run as Administrator: `Right-click rtss-auto.exe → Run as administrator`

### All statistics show zero
**Solution**: Wait 2-3 seconds for RTSS buffer to populate with data.

**See QUICKSTART.md for more troubleshooting help.**

---

## 📝 License & Credits

### License
This tool is provided as-is for educational and analysis purposes. RTSS SDK usage follows RivaTuner licensing terms.

### Technology
- **RTSS SDK**: Unwinder (Alexey Nicolaychuk)
- **RivaTuner Statistics Server**: https://www.guru3d.com/

### Development
- **Language**: C++ (MSVC)
- **Compiler**: Visual Studio 2022 Community
- **Platform**: Windows 10/11 x64

---

## 🔗 Resources

### RTSS Download
https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/

### RTSS Forums
https://forums.guru3d.com/forums/rivatuner-statistics-server-rtss-forum.26/

---

## 📈 Version History

### v1.0 (October 26, 2025) - Initial Release
- ✅ Automatic benchmark mode enablement via `dwStatFlags` write
- ✅ Real-time frame time statistics (min/max/avg/percentiles)
- ✅ Comprehensive logging with timestamps
- ✅ Per-session auto-enable on application launch
- ✅ Multi-application detection (monitors first active app)
- ✅ Tested with RTSS 7.3.x, shared memory v0x00020015

---

## 📧 Support

For issues, questions, or contributions:
1. Check **QUICKSTART.md** troubleshooting section
2. Review **DOCUMENTATION.md** for detailed technical info
3. Consult RTSS community forums for RTSS-specific questions

---

## 🎮 Real-World Usage Example

### Scenario: Testing GPU Upgrade Performance
**Before Upgrade**:
```
Average FPS:  85.2
1% Low FPS:   72.1  (84% of average)
```

**After Upgrade**:
```
Average FPS:  144.8
1% Low FPS:   128.4  (89% of average)
```

**Analysis**: Not only did average FPS improve 70%, but smoothness improved (1% Low went from 84% to 89% of average). This indicates better overall performance.

---

## 📦 Distribution Checklist

When distributing this package:
- ✅ All 8 files included
- ✅ README.md points to correct documentation
- ✅ rtss-auto.exe is compiled and tested
- ✅ Documentation is up-to-date
- ✅ Version number matches across all files
- ✅ License information included

---

## 🚨 Important Notes

### Benchmark Mode Behavior
**CRITICAL**: RTSS resets the benchmark mode flag (`dwStatFlags`) to `0x00000000` when an application closes. This is **BY DESIGN**, not a bug. The tool automatically re-enables the flag on every application launch.

### Permissions
Some systems may require running `rtss-auto.exe` as Administrator for shared memory write access.

### RTSS Version
Tested with RTSS 7.3.x (shared memory version 0x00020015). Compatibility with older versions not guaranteed.

---

**Package Prepared**: October 26, 2025  
**Status**: ✅ Production Ready  
**Total Files**: 8 (3 core + 5 documentation)  
**Total Size**: 331 KB  

**Enjoy monitoring your frame times!** 🎮📊
