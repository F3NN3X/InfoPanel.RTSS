# RTSS Frame Time Monitor - Quick Start Guide

## What is this?
A tool that **automatically monitors frame time statistics** from any 3D game/application using RivaTuner Statistics Server (RTSS) shared memory.

## Key Feature
**Automatic Benchmark Mode**: No need to manually enable benchmark mode in RTSS! The tool does it automatically for every game session.

---

## Prerequisites
1. **RTSS installed** (RivaTuner Statistics Server 7.3.x or later)
   - Download: https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/
2. **RTSS running** (check system tray for RTSS icon)

---

## Quick Start (3 Steps)

### Step 1: Run the Tool
```powershell
cd "c:\Program Files (x86)\RivaTuner Statistics Server\SDK\Samples\SharedMemory\RTSSSharedMemorySample\dump\MAHMMonitor"
.\rtss-auto.exe
```

### Step 2: Launch Your Game
Just start any 3D game or application that RTSS is monitoring.

### Step 3: See the Stats!
The console will display:
- **Average FPS** (mean framerate)
- **1% Low FPS** (99th percentile - how smooth it feels)
- **0.1% Low FPS** (99.9th percentile - worst case stutters)

**Example Output**:
```
=== Frame Time Statistics ===
Sample count: 1024 / 1024

Summary:
  Average FPS:     136.1
  1% Low FPS:      123.1  ← Most important metric!
  0.1% Low FPS:     32.6
```

---

## What Do These Numbers Mean?

### Average FPS
The mean framerate across all captured frames. **Least important metric** (doesn't show stutters).

### 1% Low FPS ⭐ **MOST IMPORTANT**
The framerate at the 99th percentile. This means:
- **99% of frames are FASTER than this**
- **1% of frames are SLOWER than this**

This is the **best indicator of smoothness**. High 1% Low = smooth gameplay.

**Example**:
- Average: 100 FPS, 1% Low: 95 FPS → **Very smooth!** (only 5% drop)
- Average: 100 FPS, 1% Low: 30 FPS → **Stuttery!** (70% drop)

### 0.1% Low FPS
The framerate at the 99.9th percentile (worst 0.1% of frames). Shows **extreme stutters**.

---

## Stopping the Monitor
Press **Ctrl+C** in the console window.

---

## Log File
Statistics are automatically saved to: `rtss-auto.log` (same directory as the .exe)

Use this for:
- Comparing gaming sessions
- Performance tuning (before/after driver updates)
- Sharing performance data

---

## Troubleshooting

### "No 3D application detected"
**Solution**: Make sure:
1. RTSS is running (check system tray)
2. Your game is actually running
3. RTSS OSD appears in the game (confirms RTSS is injecting)

---

### "Benchmark mode NOT enabled" keeps repeating
**First time only? → This is NORMAL!**
- The tool auto-enables benchmark mode on every game launch
- You should see `[SUCCESS] dwStatFlags updated: 0x00000000 -> 0x00000001` immediately after

**Every cycle? → Problem!**
- Run as Administrator: Right-click `rtss-auto.exe` → "Run as administrator"
- Verify RTSS version is 7.3.x or later

---

### Statistics are all zeros
**Solution**: Wait 2-3 seconds for the frame buffer to fill with data.

---

## Need More Details?
See the full documentation: **DOCUMENTATION.md** (same folder)

---

## Quick Build Instructions
If you modified the source code:

```powershell
cd "c:\Program Files (x86)\RivaTuner Statistics Server\SDK\Samples\SharedMemory\RTSSSharedMemorySample\dump\MAHMMonitor"
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\Launch-VsDevShell.ps1" -Arch amd64 -SkipAutomaticLocation
cl.exe /nologo /O2 /W3 /EHsc /I../.. /Fe:rtss-auto.exe rtss-auto.cpp /link kernel32.lib user32.lib
```

---

## Real-World Usage Example

### Scenario: Testing Graphics Settings
1. **Baseline**: Run rtss-auto.exe, play for 2 minutes, note 1% Low FPS
2. **Change settings**: Increase graphics quality in-game
3. **Test**: Play for 2 minutes again
4. **Compare**: If 1% Low FPS drops significantly (>20%), settings may be too high

**Rule of Thumb**:
- **1% Low > 60 FPS**: Smooth for 60Hz monitors
- **1% Low > 120 FPS**: Smooth for 144Hz monitors
- **1% Low < target refresh rate**: Noticeable stuttering

---

**Version**: 1.0  
**Last Updated**: October 26, 2025  
**Full Documentation**: DOCUMENTATION.md
