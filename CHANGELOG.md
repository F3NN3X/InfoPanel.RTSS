# CHANGELOG

## v1.1.4 (October 22, 2025)

**🧹 Legacy Code Cleanup & Enhanced User Experience**

### 🗑️ **Major Code Cleanup**
- **Removed Legacy Services**: Eliminated unused `DXGIFrameMonitoringService` (1300+ lines of complex GPU performance counter code)
  - **GPU Performance Counter Removal**: Deleted all Windows GPU performance counter integration 
  - **Legacy Fallback Elimination**: Removed complex GPU adapter detection and frame rate counter logic
  - **Simplified Architecture**: Clean 5-service architecture with only essential components

### 📦 **Dependency Optimization**
- **Package Cleanup**: Removed unused NuGet package dependencies from RTSS-only architecture
  - **Removed** `System.Diagnostics.PerformanceCounter` (v9.0.5) - was only used by removed DXGIFrameMonitoringService
  - **Removed** `Vanara.PInvoke.DwmApi` (v4.0.6) - desktop window manager calls no longer needed
  - **Retained Essential Packages**: Kept only `System.Management`, `Vanara.PInvoke.Kernel32`, and `Vanara.PInvoke.User32`

### 🏗️ **Interface Cleanup**
- **Cleaned Interface Definitions**: Recreated `IMonitoringServices.cs` with only essential service interfaces
  - **Removed** `IDXGIFrameMonitoringService` references and related legacy interfaces
  - **Streamlined** service contracts to match RTSS-only architecture
  - **Maintained** essential interfaces: `ISensorManagementService`, `ISystemInformationService`, `IConfigurationService`

### ✨ **New Features**
- **Customizable Default Message**: User-configurable capture message via INI settings
  - **Added** `[Display] defaultCaptureMessage` configuration option in InfoPanel.RTSS.ini
  - **User Control**: Customize the "Nothing to capture" message to any preferred text
  - **Localization Support**: Users can set messages in their preferred language
  - **Default Value**: Maintains "Nothing to capture" if not configured for backwards compatibility

### 🐛 **Critical Bug Fixes**
- **Fixed Window Title Capture Issue**: Resolved blank title display despite RTSS detecting games
  - **Root Cause**: `WindowInformation.IsValid` validation was failing due to missing required fields
  - **Solution**: Properly populate ProcessId, WindowHandle, and IsFullscreen fields for validation
  - **Impact**: Game window titles now display correctly when FPS monitoring is active

- **Fixed Stale FPS Data After Game Exit**: Resolved persistent FPS display when games close
  - **Root Cause**: Cleanup logic only triggered when no RTSS entries existed, but stale entries with zero FPS persisted
  - **Solution**: Enhanced cleanup to trigger when no **valid FPS data** is found, regardless of RTSS entry existence
  - **Impact**: FPS data and window titles now clear immediately (within 16ms) when games exit

- **Unified Debug Logging Control**: Consolidated all debug output under single INI toggle
  - **Enhancement**: Both RTSS monitoring and sensor window capture debug output now controlled by `[Debug] debug` setting
  - **User Experience**: Single configuration point for all plugin debug logging
  - **Performance**: No unwanted debug output when debugging is disabled

### 🔧 **Build System Updates**
- **Updated Package Comments**: Refreshed project file comments to reflect current RTSS-only usage patterns
- **Dependency Documentation**: Added inline comments explaining each remaining package's specific usage
- **Build Verification**: Confirmed successful compilation after legacy code and dependency removal

---

## v1.1.3 (October 22, 2025)

**🔧 RTSS-Only Architecture & Simplified Monitoring**

### 🏗️ **Major Architectural Changes** 
- **RTSS-Only Monitoring**: Complete elimination of complex fullscreen detection in favor of pure RTSS shared memory scanning
  - **Removed** `StableFullscreenDetectionService` and complex multi-service architecture  
  - **Added** `RTSSOnlyMonitoringService` that continuously scans RTSS shared memory every 16ms
  - **Direct RTSS Integration**: Only monitors processes that RTSS has successfully hooked - no competing detection systems
  - **Simplified Event System**: Single `MetricsUpdated` event with direct FPS, frame time, and window title updates

### ✨ **New Features**
- **Debug Logging Toggle**: User-controllable debug output via InfoPanel.RTSS.ini configuration
  - **Added** `[Debug] debug=false` setting to control logging behavior
  - **Enhanced** FileLoggingService to respect debug configuration setting
  - **User Control**: No more unwanted debug.log files during normal operation - enable only when needed

- **Enhanced Application Blacklist**: Comprehensive process filtering to prevent false positives
  - **Added** Discord, iCUE, and SignalRGB to blacklist preventing interference and 60-second timeouts
  - **Eliminated** false positive detections from background applications
  - **Improved** process filtering for reliable game-only monitoring

### 🚀 **Performance & Stability Improvements**
- **Simplified Plugin Architecture**: Streamlined main plugin implementation
  - **Reduced** complex state management and competing monitoring systems
  - **Eliminated** monitoring restart loops that prevented RTSS from completing 60-second hook attempts
  - **Direct RTSS Reading**: FPS data comes directly from RTSS Frames field (offset 276) for pixel-perfect accuracy
  - **PID-Based Title Mapping**: Window titles are mapped by process ID ensuring accurate correlation with FPS data

- **Anti-Cheat Compatibility**: Passive monitoring approach safe for protected games
  - **No ETW Tracing**: Eliminated kernel-level monitoring that triggers anti-cheat detection
  - **No DLL Injection**: RTSS handles all hooking, plugin only reads shared memory passively
  - **Works with**: Battlefield (Javelin), Valorant (Vanguard), Apex Legends (EAC), and other protected games

### 🐛 **Bug Fixes**
- **Fixed** FPS flashing after game closure - clean sensor transitions when processes end
- **Fixed** Complex detection system interference causing RTSS hook failures
- **Fixed** Process existence validation preventing infinite monitoring loops for dead processes
- **Fixed** Thread-safe sensor updates preventing collection modification exceptions

### 🏃‍♂️ **User Experience**
- **Stable FPS Display**: No more rapid switching between detection methods
- **Accurate Window Titles**: PID-based filtering ensures titles match the process providing FPS data
- **Clean UI Transitions**: Smooth sensor updates when applications start/stop
- **Reliable Operation**: Single monitoring source eliminates timing conflicts and state corruption

---

## v1.1.2 (October 22, 2025)

**🎯 RTSS-First Title Detection & Improved Stability**

### ✨ **New Features**
- **RTSS-First Window Title Detection**: Revolutionary approach that only displays window titles after RTSS successfully hooks a process
  - Eliminates timing issues where titles showed as "-" or "[No Window]"
  - Perfect PID matching between RTSS monitoring and window title display
  - Event-driven architecture with `RTSSHooked` callback system
  - Enhanced window title detection with retry logic and process refresh for games during startup

- **Stable Fullscreen Detection**: Replaced complex window monitoring with proven stable detection service
  - Uses comprehensive system process blacklisting for better reliability
  - Improved tolerance-based fullscreen detection from stable version
  - Reduced false positives from system windows and desktop applications

### 🚀 **Performance Improvements**  
- **Thread-Safe Sensor Updates**: Added lock synchronization to prevent collection modification exceptions
  - All sensor update methods now use `lock(_sensorLock)` for thread safety
  - Prevents crashes when UpdateAsync and StopMonitoringAsync run simultaneously
  - Enhanced stability during rapid game launches and closures

- **Simplified Continuous Monitoring**: Streamlined monitoring loop focusing only on RTSS-successful hooks
  - Removed complex state management and redundant process checking
  - Eliminated excessive logging of every window change on the system
  - Increased monitoring interval to 3 seconds for better stability
  - **Key Change**: Only logs when RTSS successfully hooks a process (no more noise!)

### 🏗️ **Architecture Cleanup**
- **Event-Driven Title Updates**: RTSS hook detection now fires events with PID and confirmed window title
  - `OnRTSSHooked` event handler ensures proper state synchronization
  - Performance.MonitoredProcessId automatically updated when RTSS hooks occur
  - Clean event subscription/unsubscription lifecycle management

- **Service Consolidation**: Removed redundant WindowDetectionService, using single stable detection service
- **RTSS-First Approach**: Prioritizes RTSS monitoring over traditional window detection
- **Reduced Complexity**: Simplified async task management and error handling
- **Enhanced Debug Logging**: Added comprehensive RTSS hook debugging and window title detection tracing

### 🐞 **Bug Fixes**
- **Window Title Timing Issues**: Fixed critical bug where window titles appeared as "-" instead of game names
  - Root cause: Missing synchronization between RTSS events and sensor update logic
  - Solution: Ensure Performance.MonitoredProcessId matches Window.ProcessId in RTSS event handler

- **Process Existence Validation**: Improved RTSS detection with proper process lifecycle checks
  - Enhanced stale RTSS entry filtering and logging
  - Eliminated infinite monitoring loops for dead processes
  - Better process existence validation in RTSS detection

- **Compilation Issues**: Fixed service reference conflicts and async method calls
- **Memory Management**: Better disposal of detection services and background tasks
- **Interface Resolution**: Added missing using statements for DXGIFrameMonitoringService references

---

## v1.1.1 (October 21, 2025)

**🐞 Debug Logging for User Troubleshooting**

### ✨ **New Features**
- **File-Based Debug Logging**: Added comprehensive debug logging to `debug.log` in plugin directory
  - **Purpose**: Help troubleshoot user issues where plugin shows no FPS
  - **Location**: `C:\ProgramData\InfoPanel\plugins\InfoPanel.RTSS\debug.log`
  - **Content**: Plugin initialization, RTSS detection attempts, FPS updates, system information, errors
  - **Safety**: Thread-safe file writing with proper exception handling

### 🔧 **Enhanced Debugging**
- **Plugin Lifecycle Logging**: Tracks constructor, initialization, service startup, and disposal
- **RTSS Detection Logging**: Detailed logs of RTSS shared memory attempts and hook detection
  - Logs retry attempts (e.g., "Waiting for RTSS to hook game... (5/60s)")
  - Records successful detection: "RTSS hook detected after X seconds"
  - Reports failures: "RTSS not detected after 60 seconds, falling back to GPU counters"
- **FPS Update Logging**: Records actual FPS values from RTSS with timestamps
- **System Information Logging**: GPU name, display resolution, and refresh rate
- **Error Logging**: Full exception details with stack traces for debugging

### 🛠️ **Technical Implementation**
- **FileLoggingService**: New service class for centralized file logging
- **Automatic Cleanup**: Proper disposal with session end markers
- **Log File Management**: Creates new session log on each plugin load
- **Fallback Safety**: Falls back to console logging if file writing fails
- **Integration**: Logging integrated into DXGIFrameMonitoringService and main plugin class

### 📝 **For Users Experiencing Issues**
When reporting "no FPS showing" problems, please share the `debug.log` file from:
`C:\ProgramData\InfoPanel\plugins\InfoPanel.RTSS\debug.log`

This will help identify:
- Whether RTSS is installed and running
- If RTSS successfully hooks the game
- Any initialization or service startup failures
- System configuration details

---

## v1.1.0 (October 20, 2025)

**🎯 Major FPS Accuracy & Consistency Improvements**

### ✨ **Enhanced FPS Calculation**
- **Period-Based FPS Algorithm**: Switched from instantaneous frame time calculation to period-based averaging
  - **Formula**: `(1000.0 * frameCount) / (time1 - time0)` - matches RTSS's averaging method
  - **Benefit**: Much smoother FPS display - eliminates rapid fluctuations seen with per-frame calculation
  - **Example**: 60 FPS locked now shows stable 60.0 instead of jumping 58-62
  - **Data Source**: Uses `dwTime0`, `dwTime1`, and `dwFrames` from RTSS shared memory (offsets 268, 272, 276)

### 🔧 **Frame Time Consistency Fix**
- **Derived Calculation**: Frame time now calculated directly from period FPS for perfect consistency
  - **Formula**: `frameTimeMs = 1000.0 / periodFps`
  - **Previous Issue**: Raw `dwFrameTime` (instantaneous) didn't match averaged period FPS
  - **Result**: Frame time and FPS values now perfectly aligned
  - **Example**: PeriodFPS=150.6 → FrameTime=6.64ms (1000/150.6) ✓

### 📊 **RTSS Built-In Statistics Integration**
- **New Sensors**: Added Min/Avg/Max FPS sensors from RTSS's pre-calculated statistics
  - **Offsets**: `dwStatFramerateMin` (304), `dwStatFramerateAvg` (308), `dwStatFramerateMax` (312)
  - **Format**: Statistics stored as millihertz (divided by 1000 for FPS display)
  - **Validation**: Proper checks for `dwStatFlags` (284) and `dwStatCount` (300) before reading
  - **Note**: Requires RTSS statistics to be manually enabled in RTSS settings

### 🛡️ **Statistics Validation**
- **Uninitialized Value Detection**: Added `0xFFFFFFFF` check to prevent displaying invalid statistics
  - **Previous Issue**: Invalid values showed as 4294967.3 FPS (max uint32 / 1000)
  - **Current Behavior**: Shows 0.0 when statistics unavailable or invalid
  - **Validation Flags**: Reads `dwStatFlags` and `dwStatCount` to verify statistics are ready
  - **Safety**: Only displays statistics when `statFlags != 0 && statCount > 0 && value != 0xFFFFFFFF`

### 📝 **Technical Improvements**
- **Debug Logging**: Enhanced logging with StatFlags and StatCount values for troubleshooting
- **Memory Safety**: All statistics reads protected by validation checks
- **Performance**: Period-based calculation reduces CPU overhead vs per-frame calculation

### 🗑️ **Removed Features**
- **GPU Frame Time Sensor**: Removed from roadmap (offset 679 reserved for future use)
  - Reason: Focus on core FPS metrics first, may revisit in future versions

