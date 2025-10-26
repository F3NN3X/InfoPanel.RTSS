# InfoPanel.RTSS Plugin

A plugin for the InfoPanel app that reads FPS data directly from RivaTuner Statistics Server (RTSS) to monitor and display real-time performance metrics for fullscreen applications with anti-cheat compatibility.

## Overview

InfoPanel.RTSS provides detailed performance statistics for running fullscreen applications, enabling users to monitor gaming performance in real-time through InfoPanel's interface. By leveraging RTSS's shared memory, the plugin achieves pixel-perfect FPS accuracy while remaining compatible with kernel-level anti-cheat systems. The plugin tracks FPS, frame times, and low percentile data, updating every second with efficient event-driven detection.

![InfoPanel.RTSS Screenshot](https://i.imgur.com/shmb3rI.png)

**Version:** 1.2.0  
**Author:** F3NN3X

> **🎯 Latest Update:** Version 1.2.0 introduces **Automatic Benchmark Mode** - eliminating manual RTSS configuration! The plugin now automatically enables RTSS benchmark mode when detecting games, ensuring frame time statistics (Min/Avg/Max/1% Low) are always available without user intervention.

## Features

* **🎯 Automatic Benchmark Mode (New in v1.2.0)**: Automatically enables RTSS benchmark mode for frame time statistics - no manual configuration needed!
  * Zero user intervention required
  * Statistics auto-populate when games launch
  * Continuous re-enable across gaming sessions
  * Graceful fallback with clear status indication
* **Anti-Cheat Compatible**: Uses RTSS shared memory for non-invasive FPS monitoring, compatible with kernel-level anti-cheat systems (BattlEye, EAC, Vanguard, etc.)
* **Real-time Performance Monitoring**: Tracks and displays performance metrics for fullscreen applications with second-by-second updates.
* **Comprehensive Performance Metrics**:
  * Current Frames Per Second (FPS) - read directly from RTSS overlay
  * Frame time in milliseconds - calculated for pixel-perfect accuracy
  * 1% Low FPS (CapFrameX methodology) - 60-second rolling window with 99th percentile calculation
  * Min/Avg/Max FPS - session-wide statistics automatically enabled by benchmark mode
* **Display Information**:
  * Main display resolution (e.g., "3840x2160")
  * Main display refresh rate (e.g., "240Hz")
  * GPU name detection
  * Display mode detection (Fullscreen, Borderless Fullscreen, Windowed)
* **Advanced Graphics API Detection**: Automatically identifies graphics technologies and process architectures
* **Benchmark Mode Status**: Real-time status sensor showing auto-enable state and permission warnings
* **Window Title Reporting**: Shows the title of the currently monitored fullscreen application.
* **Efficient Resource Usage**:
  * Event-driven detection ensures immediate startup when fullscreen apps launch
  * Proper cleanup and metric clearing when fullscreen apps close
  * Optimized calculations with minimal resource overhead (<1ms enable delay)
  * Thread-safe sensor updates prevent crashes during rapid state changes
* **Multi-monitor Support**: Accurate fullscreen detection on multiple monitor setups.
* **Universal Game Support**: Works with any game without hardcoded process names or special handling.

## Graphics API & Architecture Detection

InfoPanel.RTSS provides detailed graphics API and architecture detection using modern RTSS shared memory analysis. This information helps understand the rendering technology and process type of monitored applications.

### Supported Graphics APIs

The plugin automatically detects and displays the following graphics APIs:

| **Graphics API** | **Description** | **Example Games** |
|------------------|-----------------|-------------------|
| **Vulkan** | Modern low-level graphics API | No Man's Sky, DOOM Eternal, Red Dead Redemption 2 |
| **DirectX 12** | Microsoft's modern low-level API | Battlefield 2042, Forza Horizon 5, Cyberpunk 2077 |
| **DirectX 12 AFR** | Multi-GPU Alternate Frame Rendering | SLI/CrossFire enabled games |
| **DirectX 11** | Modern high-level DirectX | Most contemporary games (2010-2020) |
| **DirectX 10** | Legacy DirectX version | Older games (2006-2010) |
| **DirectX 9Ex** | Enhanced DirectX 9 | Windows Vista+ enhanced games |
| **DirectX 9** | Legacy DirectX version | Older games (2002-2008) |
| **DirectX 8** | Legacy DirectX version | Very old games (2000-2004) |
| **OpenGL** | Cross-platform graphics API | Minecraft, older indie games, some AAA titles |
| **DirectDraw** | Legacy 2D graphics API | Retro games, 2D applications |

### Architecture Classifications

Games are categorized into architectural families based on their graphics technology:

| **Architecture Type** | **Graphics APIs** | **Characteristics** |
|-----------------------|-------------------|---------------------|
| **Modern Low-Level** | Vulkan, DirectX 12, DirectX 12 AFR | Close-to-metal APIs with explicit control |
| **Modern** | DirectX 11 | High-level modern APIs with driver optimization |
| **Traditional** | DirectX 9/9Ex/10, OpenGL | Established APIs with mature toolchains |
| **Legacy** | DirectX 8, DirectDraw | Older technologies for retro/compatibility |

### Process Architecture Detection

The plugin also detects the process architecture from RTSS flags:

| **Process Type** | **Description** |
|------------------|-----------------|
| **x64** | 64-bit native process |
| **x86** | 32-bit native process |
| **UWP** | Universal Windows Platform app |
| **x64 UWP** | 64-bit UWP application |

### Combined Architecture Display

The plugin combines graphics API classification with process architecture for comprehensive information:

**Examples:**
- `"Modern Low-Level (x64)"` - Vulkan/DirectX 12 running as 64-bit process
- `"Modern (x86)"` - DirectX 11 running as 32-bit process  
- `"Traditional (x64 UWP)"` - OpenGL running as 64-bit UWP app
- `"Legacy (x86)"` - DirectX 8 running as 32-bit process

### Technical Implementation

- **RTSS v2.10+ Compatibility**: Uses modern APPFLAG enumerated values instead of deprecated bit flags
- **Accurate Detection**: Fixes previous issues where Vulkan games were misidentified as DirectX 11
- **Real-time Analysis**: Graphics API detection updates in real-time as games launch
- **Debug Logging**: Console output shows raw RTSS flags and detected API values for troubleshooting

## Automatic Benchmark Mode (v1.2.0)

### What is Automatic Benchmark Mode?

**Version 1.2.0 introduces a revolutionary feature that eliminates manual RTSS configuration.** The plugin now automatically enables RTSS's benchmark mode when detecting games, ensuring comprehensive frame time statistics are always available.

### The Problem This Solves

**RTSS Benchmark Mode Behavior:**
- RTSS has a "benchmark mode" that enables detailed frame time statistics (Min/Avg/Max FPS, 1% Low)
- This mode **auto-disables after each game session** - requiring manual re-enabling via RTSS settings
- Without benchmark mode, only current FPS is available (no Min/Avg/Max/1% Low statistics)
- Users had to repeatedly enable it through RTSS UI for every gaming session

### The Solution

InfoPanel.RTSS v1.2.0 **automatically enables benchmark mode** via direct RTSS shared memory writes:

✅ **Zero User Configuration**: No manual RTSS settings changes needed  
✅ **Automatic Re-Enable**: Detects when benchmark mode resets and re-enables it instantly  
✅ **Full Statistics**: Min/Avg/Max FPS and 1% Low automatically populate  
✅ **Transparent Operation**: Works silently in the background  
✅ **Anti-Cheat Safe**: Uses passive shared memory writes (no injection)

### How It Works

1. **Detection**: Plugin monitors RTSS shared memory for running 3D applications
2. **Auto-Enable**: When game detected, plugin writes `STATFLAG_RECORD` flag to RTSS shared memory
3. **Continuous Monitoring**: Plugin checks flag status and re-enables if it resets (per-session behavior)
4. **Statistics Flow**: RTSS collects frame time data → Plugin reads comprehensive statistics
5. **Status Display**: "Benchmark Mode" sensor shows real-time enable status

### Status Indicators

The plugin provides a **"Benchmark Mode"** sensor with the following states:

| **Status** | **Meaning** | **Action Required** |
|------------|-------------|---------------------|
| **✓ Enabled** | Auto-enable working, statistics available | None - working perfectly |
| **Failed (RTSS Not Running)** | RTSS shared memory unavailable | Launch RTSS before starting games |

### Technical Details

**Implementation:**
- **BenchmarkModeManager Service**: Specialized service managing RTSS shared memory writes
- **Critical Offset**: `dwStatFlags` at byte 284 (per-app benchmark mode control)
- **Flag Constant**: `STATFLAG_RECORD (0x00000001)` enables frame time recording
- **Write Verification**: Reads back after write to confirm flag change succeeded
- **Performance**: <1ms enable delay (zero user-visible impact)

**Compatibility:**
- **RTSS Version**: Tested with RTSS v7.3.x (shared memory version 0x00020015)
- **Anti-Cheat**: Passive memory writes maintain existing anti-cheat compatibility
- **Accuracy**: Statistics match RTSS OSD within ±5% (validated via multi-session testing)

**Credit:**
- Based on proven C++ implementation (`rtss-auto.cpp`) from exhaustive RTSS shared memory research
- Direct port to C# for seamless InfoPanel integration

## Requirements

* InfoPanel app (latest version recommended)
* Windows operating system
* **RivaTuner Statistics Server (RTSS)** - Required for FPS monitoring
  * Download from: [Guru3D - RivaTuner Statistics Server](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html)
  * Or install MSI Afterburner (includes RTSS): [MSI Afterburner](https://www.msi.com/Landing/afterburner)
  * RTSS must be running for the plugin to capture FPS data

## RTSS Setup & Configuration

### What is RTSS?

RivaTuner Statistics Server (RTSS) is a powerful overlay application that provides on-screen display (OSD) functionality for monitoring hardware statistics and FPS in games. This plugin reads FPS data directly from RTSS's shared memory, making it compatible with anti-cheat protected games.

### Installation

1. **Download and Install RTSS**:
   - Standalone: Download from [Guru3D](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html)
   - Or install MSI Afterburner which includes RTSS

2. **Configure RTSS**:
   - Launch RTSS (RivaTunerStatisticsServer.exe)
   - Make sure RTSS is running and enabled (system tray icon should be visible)
   - RTSS will automatically hook into games when they launch
   - **No manual benchmark mode configuration needed** - v1.2.0 enables it automatically!

3. **Verify Operation**:
   - Launch a fullscreen game
   - You should see the RTSS overlay in-game (if enabled)
   - InfoPanel.RTSS will read the same FPS values RTSS displays
   - **Benchmark Mode sensor** will show "✓ Enabled" if auto-enable is working

### Important Notes

- **RTSS Must Be Running**: The plugin requires RTSS to be running before launching games. If RTSS is not detected, no FPS data will be available.

- **Automatic Benchmark Mode (v1.2.0)**: The plugin automatically enables RTSS benchmark mode for comprehensive statistics:
  - **Automatic Statistics**: Min/Avg/Max/1% Low FPS populate automatically without manual RTSS configuration
  - **Status Monitoring**: Check the "Benchmark Mode" sensor to verify auto-enable is working
  
- **Hook Timing**: RTSS may take a few seconds to hook into newly launched games (up to 60 seconds for first launch). During this time:
  - Window title may show "Nothing to capture"
  - FPS data will appear once RTSS hooks the game
  - This is normal behavior and not a plugin error

- **Overlay Not Required**: You don't need to have RTSS's on-screen overlay enabled. The plugin reads from shared memory regardless of overlay visibility.

- **Anti-Cheat Compatibility**: RTSS is widely accepted by anti-cheat systems because it uses non-invasive DirectX hooking. This plugin reads data passively from RTSS without any injection.

- **Pixel-Perfect Accuracy**: FPS values match RTSS overlay exactly, calculated from the same data source (RTSS Frames field at offset 276).

### Troubleshooting

**No FPS Data Displayed:**
- Verify RTSS is running (check system tray)
- Launch the game and wait 10-60 seconds for RTSS to hook
- Check RTSS settings to ensure game hooking is enabled
- Some games may need to be added to RTSS's application profile

**Window Title Shows "Nothing to capture":**
- This typically means RTSS hasn't hooked the game yet
- Wait a few more seconds - title will appear once RTSS connects
- If it persists, restart RTSS and relaunch the game

**For detailed RTSS troubleshooting, see [RTSS-TROUBLESHOOTING.md](RTSS-TROUBLESHOOTING.md)**

## To compile

* .NET runtime compatible with InfoPanel
* .NET 8.0 Windows SDK

## Installation

1. Download the latest release from GitHub.
2. Import into InfoPanel via the "Import Plugin" feature.
3. The plugin will automatically start monitoring fullscreen applications.

## Installation from Source

1. Clone or download this repository.
2. Build the project in a .NET environment.
3. Copy the compiled DLLs and dependencies to your InfoPanel plugins folder.

## Configuration

The plugin can be customized through the `InfoPanel.RTSS.ini` configuration file, which is created automatically in the plugin directory.

### Available Settings

#### Display Settings
- **`defaultCaptureMessage`**: Customize the message displayed when no game is being monitored
  - **Default**: `"Nothing to capture"`
  - **Examples**: 
    - `"Waiting for game..."`
    - `"Ready to monitor"`
    - `"No active monitoring"`
    - `"Aucun jeu détecté"` (French)
    - `"Kein Spiel erkannt"` (German)

#### Debug Settings
- **`debug`**: Enable/disable debug logging for troubleshooting
  - **Default**: `false`
  - **Set to `true`**: Enables detailed logging to debug.log file
  - **Set to `false`**: Disables logging for production use

#### Custom Game Categories
- **User-Defined Categories**: Create custom game categories by adding INI sections
  - **Format**: `[Game_Category_YourCategoryName]`
  - **Pattern Support**: Exact matches, wildcards (`*`), and comma-separated lists
  - **Example Categories**: Competitive FPS, Racing Games, VR Games, Retro Games
  - **Priority**: Custom categories override default categorization
  - **Pattern Examples**:
    - `pattern1=cyberpunk2077.exe` (exact match)
    - `pattern2=*witcher*` (wildcard match)
    - `processes=eldenring.exe,sekiro.exe,*souls*` (comma-separated list)

#### Example Configuration File
```ini
[Display]
# Default message to display when no game is being captured
defaultCaptureMessage=Nothing to capture

# Update interval in milliseconds for UI updates
updateInterval=1000

# Number of frames to use for smoothing calculations
smoothingFrames=60

[Debug]
# Enable/disable debug logging to debug.log file and console debug output
# Set to true to enable detailed logging for troubleshooting (RTSS, sensors, window capture)
# Set to false to disable debug logging for production use
debug=false

# Custom Game Categories
# Define your own game categories by creating sections named [Game_Category_YourCategoryName]
# You can use exact process names, wildcard patterns (*), or comma-separated lists

[Game_Category_Competitive FPS]
processes=*valorant*,*csgo*,*cs2*,*overwatch*,*apex*,*rainbow*

[Game_Category_Racing Games]
processes=*forza*,*gran*,*dirt*,*f1*,*crew*,*nfs*

[Game_Category_My Favorite Games]
pattern1=cyberpunk2077.exe
pattern2=*witcher*
pattern3=*battlefield*
processes=eldenring.exe,sekiro.exe,*souls*
```

## Usage

1. **Ensure RTSS is Running**: Launch RivaTuner Statistics Server before starting games.
2. **Launch InfoPanel**: Start InfoPanel with the plugin loaded.
3. **Start Your Game**: Launch any fullscreen game or application.
4. **Automatic Detection**: The plugin automatically detects fullscreen applications and begins monitoring.
5. **Automatic Benchmark Mode**: Plugin enables RTSS benchmark mode automatically (v1.2.0 feature).
6. **View Metrics**: Real-time performance metrics appear in InfoPanel's UI:
   - Current FPS (matching RTSS overlay exactly)
   - Frame time in milliseconds
   - 1% Low FPS for stutter analysis (CapFrameX methodology)
   - Min/Avg/Max FPS (automatically enabled via benchmark mode)
   - Benchmark Mode status (✓ Enabled / ✗ Disabled)
   - Window title, resolution, refresh rate, GPU name, display mode, graphics API
7. **Automatic Cleanup**: Metrics reset when fullscreen applications are closed.

### Supported Games

This plugin works with **any game** that RTSS can hook, including those with anti-cheat protection:
- Battlefield 2042/6 (Javelin anti-cheat)
- Gray Zone Warfare
- Valorant (Vanguard anti-cheat)
- Apex Legends (EAC)
- PUBG (BattlEye)
- Rainbow Six Siege
- No Man's Sky
- Deadside
- And many more...

## Notes

* **RTSS Required**: This plugin requires RTSS to be running. Without RTSS, no FPS data will be available.
* **Hook Delay**: On first game launch, RTSS may take 10-60 seconds to hook the game. This is normal - be patient.
* **Accuracy**: FPS values are read directly from RTSS's Frames field, ensuring pixel-perfect accuracy matching the overlay.
* **Automatic Benchmark Mode (v1.2.0)**: Plugin automatically enables RTSS benchmark mode for comprehensive statistics - no manual configuration needed!
  * Min/Avg/Max/1% Low statistics auto-populate when games launch
  * Check "Benchmark Mode" sensor for status verification
* **Stuck Values Fix (v1.2.0)**: Plugin now validates process existence to prevent sensor values from freezing after game exits.
* For detailed version history, please refer to the `CHANGELOG.md` file.
