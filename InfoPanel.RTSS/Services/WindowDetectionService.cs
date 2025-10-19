using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using InfoPanel.RTSS.Constants;
using InfoPanel.RTSS.Interfaces;
using InfoPanel.RTSS.Models;
using Vanara.PInvoke;
using static Vanara.PInvoke.DwmApi;
using static Vanara.PInvoke.User32;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Service responsible for detecting fullscreen windows and monitoring window changes.
    /// Uses Windows API hooks to detect when applications enter or exit fullscreen mode.
    /// </summary>
    public class WindowDetectionService : IWindowDetectionService
    {
        private IntPtr _eventHook;
        private readonly User32.WinEventProc _winEventProcDelegate;
        private DateTime _lastEventTime = DateTime.MinValue;
        private volatile bool _isMonitoring;

        /// <summary>
        /// Event fired when a fullscreen window is detected or lost.
        /// </summary>
        public event Action<WindowInformation>? WindowChanged;

        /// <summary>
        /// Initializes a new instance of the WindowDetectionService.
        /// </summary>
        public WindowDetectionService()
        {
            _winEventProcDelegate = new User32.WinEventProc(WinEventProc);
        }

        /// <summary>
        /// Starts monitoring for fullscreen window changes.
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring)
            {
                Console.WriteLine("Window detection already started");
                return;
            }

            SetupEventHook();
            _isMonitoring = true;
            Console.WriteLine("Window detection service started");
        }

        /// <summary>
        /// Stops monitoring for window changes.
        /// </summary>
        public void StopMonitoring()
        {
            if (_eventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_eventHook);
                _eventHook = IntPtr.Zero;
            }
            _isMonitoring = false;
            Console.WriteLine("Window detection service stopped");
        }

        /// <summary>
        /// Gets information about the current fullscreen window using enhanced detection.
        /// </summary>
        /// <returns>Window information or null if no fullscreen window is detected.</returns>
        public WindowInformation? GetCurrentFullscreenWindow()
        {
            // First try enhanced detection which enumerates all windows
            var enhancedResult = DetectFullscreenWindowEnhanced();
            if (enhancedResult != null)
            {
                Console.WriteLine($"Enhanced detection found: {enhancedResult.WindowTitle} (PID: {enhancedResult.ProcessId})");
                return enhancedResult;
            }

            // Fallback to original foreground window detection
            Console.WriteLine("Enhanced detection found nothing, trying foreground window detection");
            return GetCurrentFullscreenWindowLegacy();
        }

        /// <summary>
        /// Legacy method for detecting fullscreen windows (foreground window approach).
        /// </summary>
        private WindowInformation? GetCurrentFullscreenWindowLegacy()
        {
            HWND hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                Console.WriteLine("GetCurrentFullscreenWindowLegacy: No foreground window");
                return null;
            }

            if (!GetWindowRect(hWnd, out RECT windowRect))
            {
                Console.WriteLine($"GetCurrentFullscreenWindow: Failed to get window rectangle for hWnd {hWnd}");
                return null;
            }

            // Get the monitor hosting the window for fullscreen detection
            HMONITOR hMonitor = MonitorFromWindow(hWnd, MonitorFlags.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                Console.WriteLine($"GetCurrentFullscreenWindow: No monitor found for hWnd {hWnd}");
                return null;
            }

            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                Console.WriteLine($"GetCurrentFullscreenWindow: Failed to get monitor info for monitor {hMonitor}");
                return null;
            }

            // Only monitor applications on the primary display
            bool isPrimaryMonitor = monitorInfo.dwFlags.HasFlag(User32.MonitorInfoFlags.MONITORINFOF_PRIMARY);
            if (!isPrimaryMonitor)
            {
                Console.WriteLine($"GetCurrentFullscreenWindow: Window {hWnd} is not on primary monitor, skipping");
                return null;
            }

            var monitorRect = monitorInfo.rcMonitor;
            
            // Calculate areas for fullscreen detection
            long windowArea = (long)(windowRect.right - windowRect.left) * (windowRect.bottom - windowRect.top);
            long monitorArea = (long)(monitorRect.right - monitorRect.left) * (monitorRect.bottom - monitorRect.top);
            bool isFullscreen = windowArea >= monitorArea * MonitoringConstants.FullscreenAreaThreshold;

            // Get process ID for anti-cheat game detection
            GetWindowThreadProcessId(hWnd, out uint pid);
            Console.WriteLine($"WindowDetection: Checking PID {pid} for anti-cheat game detection");
            bool isAntiCheatGame = IsAntiCheatProtectedGame(pid);
            Console.WriteLine($"WindowDetection: PID {pid} - IsAntiCheatGame: {isAntiCheatGame}");

            if (!isFullscreen && !isAntiCheatGame) // Check extended bounds for borderless fullscreen (unless anti-cheat game)
            {
                if (DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT extendedFrameBounds).Succeeded)
                {
                    long extendedArea = (long)(extendedFrameBounds.right - extendedFrameBounds.left) * 
                                      (extendedFrameBounds.bottom - extendedFrameBounds.top);
                    isFullscreen = extendedArea >= monitorArea * MonitoringConstants.FullscreenAreaThreshold;
                    
                    Console.WriteLine($"Extended bounds check - hWnd: {hWnd}, Area: {extendedArea}, Monitor: {monitorArea}, Fullscreen: {isFullscreen}");
                }
                else
                {
                    Console.WriteLine($"Failed to get extended frame bounds for hWnd {hWnd}");
                }
            }
            else if (isAntiCheatGame)
            {
                Console.WriteLine($"Anti-cheat game detected - bypassing fullscreen check for hWnd: {hWnd}, Area: {windowArea}, Monitor: {monitorArea}");
                isFullscreen = true; // Force fullscreen detection for anti-cheat games
            }
            else
            {
                Console.WriteLine($"Window bounds check - hWnd: {hWnd}, Area: {windowArea}, Monitor: {monitorArea}, Fullscreen: {isFullscreen}");
            }

            if (!isFullscreen)
            {
                Console.WriteLine($"Window hWnd {hWnd} is not fullscreen");
                return null;
            }

            // Validate PID (already obtained above)
            if (!IsValidApplicationPid(pid))
            {
                Console.WriteLine($"Invalid PID {pid} for hWnd {hWnd}");
                return null;
            }

            // Get window title
            string windowTitle = GetWindowTitle(hWnd);

            var windowInfo = new WindowInformation
            {
                ProcessId = pid,
                WindowHandle = (IntPtr)hWnd,
                WindowTitle = windowTitle,
                IsFullscreen = true
            };

            Console.WriteLine($"Fullscreen window detected - hWnd: {hWnd}, PID: {pid}, Title: {windowTitle}");
            return windowInfo;
        }

        /// <summary>
        /// Sets up the Windows event hook to monitor foreground window changes.
        /// </summary>
        private void SetupEventHook()
        {
            _eventHook = SetWinEventHook(
                    User32.EventConstants.EVENT_SYSTEM_FOREGROUND,
                    User32.EventConstants.EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    _winEventProcDelegate,
                    0,
                    0,
                    User32.WINEVENT.WINEVENT_OUTOFCONTEXT
                )
                .DangerousGetHandle();

            if (_eventHook == IntPtr.Zero)
            {
                Console.WriteLine("Failed to set up window event hook");
            }
            else
            {
                Console.WriteLine("Window event hook established");
            }
        }

        /// <summary>
        /// Handles foreground window change events with debouncing.
        /// </summary>
        private void WinEventProc(
            HWINEVENTHOOK hWinEventHook,
            uint eventType,
            HWND hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint dwmsEventTime)
        {
            DateTime now = DateTime.Now;
            if ((now - _lastEventTime).TotalMilliseconds < MonitoringConstants.EventDebounceMs)
                return;

            _lastEventTime = now;
            _ = Task.Run(() => HandleWindowChangeAsync(hwnd));
        }

        /// <summary>
        /// Handles window change events asynchronously.
        /// </summary>
        private async Task HandleWindowChangeAsync(HWND hwnd)
        {
            try
            {
                await Task.Delay(50).ConfigureAwait(false); // Small delay for window stabilization
                
                var windowInfo = AnalyzeWindow(hwnd);
                
                Console.WriteLine($"Window change detected - PID: {windowInfo.ProcessId}, " +
                                $"Title: {windowInfo.WindowTitle}, " +
                                $"Fullscreen: {windowInfo.IsFullscreen}");

                WindowChanged?.Invoke(windowInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling window change: {ex}");
            }
        }

        /// <summary>
        /// Analyzes a window to determine if it's fullscreen and gathers information.
        /// </summary>
        private WindowInformation AnalyzeWindow(HWND hWnd)
        {
            var windowInfo = new WindowInformation
            {
                WindowHandle = (IntPtr)hWnd
            };

            if (hWnd == IntPtr.Zero)
                return windowInfo;

            try
            {
                // Get process ID
                GetWindowThreadProcessId(hWnd, out uint pid);
                windowInfo.ProcessId = pid;

                // Get window title
                windowInfo.WindowTitle = GetWindowTitle(hWnd);

                // Check if window is fullscreen
                windowInfo.IsFullscreen = IsWindowFullscreen(hWnd);

                // Validate process
                if (!IsValidApplicationPid(pid))
                {
                    windowInfo.ProcessId = 0;
                    windowInfo.IsFullscreen = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing window {hWnd}: {ex}");
            }

            return windowInfo;
        }

        /// <summary>
        /// Gets the window title for a specific process ID by finding its main window.
        /// </summary>
        /// <param name="processId">The process ID to get the window title for.</param>
        /// <returns>The window title, or empty string if not found.</returns>
        public string GetWindowTitleByPID(uint processId)
        {
            try
            {
                var process = Process.GetProcessById((int)processId);
                if (process != null && process.MainWindowHandle != IntPtr.Zero)
                {
                    string title = GetWindowTitle(new HWND(process.MainWindowHandle));
                    Console.WriteLine($"WindowDetectionService: Found title for PID {processId}: '{title}'");
                    return title;
                }

                // If MainWindowHandle is zero, try to enumerate all windows for this process
                string foundTitle = "";
                EnumWindows((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint windowPid);
                    if (windowPid == processId)
                    {
                        string title = GetWindowTitle(hWnd);
                        if (!string.IsNullOrWhiteSpace(title) && title != "Untitled")
                        {
                            foundTitle = title;
                            Console.WriteLine($"WindowDetectionService: Found title for PID {processId} via enumeration: '{title}'");
                            return false; // Stop enumeration
                        }
                    }
                    return true; // Continue enumeration
                }, IntPtr.Zero);

                return foundTitle;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WindowDetectionService: Error getting title for PID {processId}: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Gets the title of the specified window.
        /// </summary>
        private static string GetWindowTitle(HWND hWnd)
        {
            try
            {
                int length = GetWindowTextLength(hWnd);
                if (length <= 0)
                    return "Untitled";

                StringBuilder title = new StringBuilder(length + 1);
                GetWindowText(hWnd, title, title.Capacity);
                return title.ToString();
            }
            catch
            {
                return "Untitled";
            }
        }

        /// <summary>
        /// Determines if a window is currently fullscreen.
        /// </summary>
        private static bool IsWindowFullscreen(HWND hWnd)
        {
            try
            {
                if (!GetWindowRect(hWnd, out RECT windowRect))
                    return false;

                // Get the monitor hosting the window
                HMONITOR hMonitor = MonitorFromWindow(hWnd, MonitorFlags.MONITOR_DEFAULTTONEAREST);
                if (hMonitor == IntPtr.Zero)
                    return false;

                var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(hMonitor, ref monitorInfo))
                    return false;

                // Only consider fullscreen on primary monitor
                bool isPrimaryMonitor = monitorInfo.dwFlags.HasFlag(User32.MonitorInfoFlags.MONITORINFOF_PRIMARY);
                if (!isPrimaryMonitor)
                {
                    Console.WriteLine($"IsWindowFullscreen: Window {hWnd} is not on primary monitor");
                    return false;
                }

                var monitorRect = monitorInfo.rcMonitor;
                
                // Calculate areas for fullscreen detection
                long windowArea = (long)(windowRect.right - windowRect.left) * (windowRect.bottom - windowRect.top);
                long monitorArea = (long)(monitorRect.right - monitorRect.left) * (monitorRect.bottom - monitorRect.top);
                bool isFullscreen = windowArea >= monitorArea * MonitoringConstants.FullscreenAreaThreshold;

                if (!isFullscreen)
                {
                    // Check extended bounds for borderless fullscreen
                    if (DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, out RECT extendedFrameBounds).Succeeded)
                    {
                        long extendedArea = (long)(extendedFrameBounds.right - extendedFrameBounds.left) * 
                                          (extendedFrameBounds.bottom - extendedFrameBounds.top);
                        isFullscreen = extendedArea >= monitorArea * MonitoringConstants.FullscreenAreaThreshold;
                        
                        Console.WriteLine($"Extended bounds check - Area: {extendedArea}, Monitor: {monitorArea}, Fullscreen: {isFullscreen}");
                    }
                }
                else
                {
                    Console.WriteLine($"Window bounds check - Area: {windowArea}, Monitor: {monitorArea}, Fullscreen: {isFullscreen}");
                }

                return isFullscreen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking fullscreen status for window {hWnd}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Validates if a PID belongs to a legitimate application with a main window.
        /// </summary>
        private static bool IsValidApplicationPid(uint pid)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                
                // Exclude InfoPanel's own process
                uint currentProcessId = (uint)Environment.ProcessId;
                if (pid == currentProcessId)
                {
                    Console.WriteLine($"PID validation - Excluding own process: {pid}");
                    return false;
                }
                
                // Check if this is an anti-cheat protected game first
                string processName = process.ProcessName.ToLowerInvariant();
                bool isAntiCheatGame = IsAntiCheatProtectedGameByName(processName);
                
                // For anti-cheat games, skip the MainWindow requirement as they often have special window handling
                if (isAntiCheatGame)
                {
                    Console.WriteLine($"PID validation - Anti-cheat game detected: {pid} ({processName}), skipping MainWindow check");
                    // Still check basic PID validity
                    if (pid <= 4)
                    {
                        Console.WriteLine($"PID validation - PID: {pid}, Invalid: PID too low");
                        return false;
                    }
                }
                else
                {
                    // Basic validation for regular applications
                    if (pid <= 4 || process.MainWindowHandle == IntPtr.Zero)
                    {
                        Console.WriteLine($"PID validation - PID: {pid}, MainWindow: {process.MainWindowHandle}, Invalid: basic validation failed");
                        return false;
                    }
                }
                
                // Exclude common system processes and overlays
                string[] excludedProcesses = 
                {
                    "dwm", "explorer", "winlogon", "csrss", "lsass", "services", "svchost",
                    "infopanel", "displaywindow", "windowsshell", "systemsettings", "shell"
                };
                
                foreach (string excluded in excludedProcesses)
                {
                    if (processName.Contains(excluded))
                    {
                        Console.WriteLine($"PID validation - Excluding process: {pid} ({processName})");
                        return false;
                    }
                }
                
                Console.WriteLine($"PID validation - PID: {pid}, Process: {processName}, Valid: true");
                return true;
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"PID {pid} not found");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating PID {pid}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a process name belongs to a known anti-cheat protected game.
        /// </summary>
        private static bool IsAntiCheatProtectedGameByName(string processName)
        {
            // List of known anti-cheat protected games that may block window detection
            string[] antiCheatGames = 
            {
                "bf6",           // Battlefield 6 (EAC)
                "battlefield",   // Battlefield series
                "valorant",      // Valorant (Vanguard)
                "apex",          // Apex Legends (EAC)
                "fortnite",      // Fortnite (BattlEye)
                "pubg",          // PUBG (BattlEye)
                "tslgame_be",    // PUBG: Battlegrounds (BattlEye)
                "rainbow6",      // Rainbow Six Siege (BattlEye)
                "r6",            // Rainbow Six Siege
                "hunt",          // Hunt: Showdown (EAC)
                "deadbydaylight", // Dead by Daylight (EAC)
                "rust",          // Rust (EAC)
                "tarkov",        // Escape from Tarkov (BattlEye)
                "squad",         // Squad (EAC)
                "rocketleague",  // Rocket League (Psyonix anti-cheat)
                "gzw",           // Gray Zone Warfare
                "greyzonewarf",  // Gray Zone Warfare (alternative name)
                "grayzonewarfare" // Gray Zone Warfare (alternative name)
            };
            
            foreach (string antiCheatGame in antiCheatGames)
            {
                if (processName.Contains(antiCheatGame))
                {
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Checks if a process is a known anti-cheat protected game that should be monitored regardless of fullscreen detection.
        /// </summary>
        private static bool IsAntiCheatProtectedGame(uint pid)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                string processName = process.ProcessName.ToLowerInvariant();
                
                Console.WriteLine($"IsAntiCheatProtectedGame: Checking process '{processName}' (PID: {pid})");
                
                // List of known anti-cheat protected games that may block window detection
                string[] antiCheatGames = 
                {
                    "bf6",           // Battlefield 6 (EAC)
                    "battlefield",   // Battlefield series
                    "valorant",      // Valorant (Vanguard)
                    "apex",          // Apex Legends (EAC)
                    "fortnite",      // Fortnite (BattlEye)
                    "pubg",          // PUBG (BattlEye)
                    "tslgame_be",    // PUBG: Battlegrounds (BattlEye)
                    "rainbow6",      // Rainbow Six Siege (BattlEye)
                    "r6",            // Rainbow Six Siege
                    "hunt",          // Hunt: Showdown (EAC)
                    "deadbydaylight", // Dead by Daylight (EAC)
                    "rust",          // Rust (EAC)
                    "tarkov",        // Escape from Tarkov (BattlEye)
                    "destiny2",      // Destiny 2 (BattlEye)
                    "warframe"       // Warframe (custom anti-cheat)
                };
                
                foreach (string gameName in antiCheatGames)
                {
                    if (processName.Contains(gameName))
                    {
                        Console.WriteLine($"Anti-cheat protected game detected: {processName} (PID: {pid}) - bypassing fullscreen check");
                        return true;
                    }
                }
                
                Console.WriteLine($"IsAntiCheatProtectedGame: Process '{processName}' is not an anti-cheat game");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking anti-cheat game status for PID {pid}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Enhanced fullscreen detection that enumerates all windows for better game detection.
        /// Uses improved blacklisting and process filtering from FullscreenDetectionService.
        /// </summary>
        public WindowInformation? DetectFullscreenWindowEnhanced()
        {
            WindowInformation? detectedWindow = null;
            uint currentProcessId = (uint)Environment.ProcessId;
            int windowsChecked = 0;
            int fullscreenWindows = 0;
            
            Console.WriteLine($"Enhanced detection starting - current PID: {currentProcessId}");
            
            // Enhanced blacklist from the new detection service (simplified)
            var enhancedBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "audiodg", "backgroundtaskhost", "csrss", "ctfmon", "dasHost", "dllhost", "dwm",
                "explorer", "fontdrvhost", "gamebar", "gamebarftserver", "infopanel", "lsass",
                "mobsync", "msedge", "onedrive", "runtimebroker", "searchapp", "searchui",
                "services", "shellexperiencehost", "sihost", "sppsvc", "spoolsv", "startmenuexperiencehost",
                "steam", "system", "systemsettings", "taskhostw", "taskmgr", "textinputhost", "wininit",
                "winlogon", "wmpnetwk", "wudfhost", "svchost", "conhost", "smss", "wininit", 
                "services", "lsass", "winlogon", "fontdrvhost", "dwm", "spoolsv", "msdtc", 
                "dfssvc", "dns", "eventlog", "eventcreate", "gpsvc", "ikeext", "iphlpsvc", 
                "keyiso", "kdc", "wkssvc", "lanmanserver", "lanmanworkstation", "lltdsvc", 
                "lmhosts", "mpssvc", "msiserver", "napagent", "netlogon", "netman", "netprofm",
                "nlasvc", "nsi", "p2psvc", "pla", "plugplay", "policysvr", "profsvc", 
                "protectedstorage", "rasauto", "rasman", "remoteaccess", "rpcss", "rsvp", 
                "samss", "scardsvr", "schedule", "seclogon", "sens", "sessionenv", "sharedaccess",
                "shellhwdetection", "sis", "slsvc", "snmptrap", "spooler", "ssdpsrv", "stisvc",
                "swprv", "sysmain", "tabletpcinputservice", "tapisrv", "termdd", "termservice",
                "themes", "threadorder", "tiledatamodelsvc", "tlntsvr", "tmgmta", "tpm", "tpmd",
                "trkwks", "trustedinstaller", "tssdis", "tssdjet", "ui0detect", "umrdp", 
                "upnphost", "upnp", "vaultsvc", "vds", "vmms", "vss", "w32time", "w3svc",
                "w3wp", "wbengine", "wcspluginservice", "wcncsvc", "webclient", "wecsvc",
                "wephostsvc", "wer", "wersvc", "wiaserv", "wlansvc", "wlidsvc", "wmi", 
                "wmiapsrv", "wmic", "wmiprvse", "wmpnetworksvc", "wmsvc", "workfolderssvc",
                "wpcsvc", "wpdbusenum", "wsd", "wsearch", "wuauserv", "wudfsvc", "wudf",
                "wudfhost", "xmlprov", "zeroconf", "xbox", "xboxgamebar", "xboxgamebarwidgets"
            };

            // Use the working window enumeration pattern from the old service
            EnumWindows((hWnd, lParam) =>
            {
                windowsChecked++;
                try
                {
                    // Use the simpler, working fullscreen detection from the legacy method
                    if (IsWindowFullscreen(hWnd))  // Use the legacy method that was working
                    {
                        fullscreenWindows++;
                        User32.GetWindowThreadProcessId(hWnd, out uint pid);
                        Console.WriteLine($"Enhanced detection - Found fullscreen window PID {pid}");
                        
                        if (pid != currentProcessId && !IsSimpleBlacklistedProcess(pid, enhancedBlacklist))
                        {
                            var processName = GetProcessName(pid);
                            var windowTitle = GetWindowTitle(hWnd);
                            
                            if (detectedWindow == null)
                            {
                                Console.WriteLine($"Enhanced detection - Detected fullscreen window: {windowTitle} (PID: {pid}, Process: {processName})");
                            }
                            
                            if (!string.IsNullOrEmpty(processName))
                            {
                                detectedWindow = new WindowInformation
                                {
                                    ProcessId = pid,
                                    WindowTitle = windowTitle ?? processName,
                                    WindowHandle = (IntPtr)hWnd,
                                    IsFullscreen = true
                                };
                                return false; // Stop enumeration when we find a fullscreen window
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Enhanced detection - Fullscreen window PID {pid} was blacklisted or is current process");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Enhanced detection error: {ex.Message}");
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            Console.WriteLine($"Enhanced detection - Checked {windowsChecked} windows, found {fullscreenWindows} fullscreen windows");
            return detectedWindow;
        }

        /// <summary>
        /// Simplified blacklist checking based on working old service.
        /// </summary>
        private bool IsSimpleBlacklistedProcess(uint pid, HashSet<string> blacklist)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);

                if (process.HasExited)
                {
                    return true;
                }

                if (process.SessionId == 0) // System session
                {
                    return true;
                }

                var processName = process.ProcessName;
                if (string.IsNullOrWhiteSpace(processName))
                {
                    return true;
                }

                if (blacklist.Contains(processName))
                {
                    return true;
                }

                // Check if it's in a system path (simplified from old service)
                try
                {
                    var processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                        var systemDir = Environment.SystemDirectory;
                        var normalized = processPath.ToLowerInvariant();
                        
                        if (normalized.StartsWith(windowsDir.ToLowerInvariant()) || 
                            normalized.StartsWith(systemDir.ToLowerInvariant()))
                        {
                            return true;
                        }
                    }
                }
                catch { }

                return false;
            }
            catch { return true; }
        }

        /// <summary>
        /// Enhanced fullscreen window detection with improved tolerance and system path checks.
        /// </summary>
        private bool IsFullscreenWindowEnhanced(HWND hWnd)
        {
            const int FullscreenTolerance = 12;
            
            try
            {
                if (!User32.IsWindowVisible(hWnd) || User32.IsIconic(hWnd))
                {
                    return false;
                }

                if (!User32.GetWindowRect(hWnd, out RECT windowRect))
                {
                    return false;
                }

                var monitorHandle = User32.MonitorFromWindow(hWnd, User32.MonitorFlags.MONITOR_DEFAULTTONULL);
                if (monitorHandle.IsNull)
                {
                    return false;
                }

                var monitorInfo = new User32.MONITORINFO
                {
                    cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>()
                };

                if (!User32.GetMonitorInfo(monitorHandle, ref monitorInfo))
                {
                    return false;
                }

                var monitorRect = monitorInfo.rcMonitor;

                if (!IsWithinToleranceEnhanced(windowRect, monitorRect, FullscreenTolerance))
                {
                    return false;
                }

                uint style = GetWindowLong(hWnd, -16); // GWL_STYLE
                bool hasBorder = (style & 0x00C00000) != 0 || (style & 0x00040000) != 0; // WS_CAPTION | WS_THICKFRAME

                if (hasBorder && User32.GetClientRect(hWnd, out RECT clientRect))
                {
                    if (!IsSizeWithinToleranceEnhanced(clientRect, monitorRect, FullscreenTolerance * 2))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Enhanced blacklist checking with system path validation.
        /// </summary>
        private bool IsEnhancedBlacklistedProcess(uint pid, HashSet<string> blacklist)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);

                if (process.HasExited)
                {
                    return true;
                }

                if (process.SessionId == 0)
                {
                    return true;
                }

                var processName = process.ProcessName;
                if (string.IsNullOrWhiteSpace(processName))
                {
                    return true;
                }

                if (blacklist.Contains(processName))
                {
                    return true;
                }

                var processPath = TryGetProcessPath(process);
                if (IsSystemPathEnhanced(processPath))
                {
                    return true;
                }

                return false;
            }
            catch { return true; }
        }

        /// <summary>
        /// Enhanced system path checking.
        /// </summary>
        private bool IsSystemPathEnhanced(string? processPath)
        {
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return false;
            }

            var normalized = NormalizePathEnhanced(processPath);
            var systemPathPrefixes = BuildSystemPathPrefixes();
            
            foreach (var prefix in systemPathPrefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Helper methods from enhanced detection service.
        /// </summary>
        private static bool IsWithinToleranceEnhanced(RECT windowRect, RECT monitorRect, int tolerance)
        {
            int widthDifference = Math.Abs(windowRect.Width - monitorRect.Width);
            int heightDifference = Math.Abs(windowRect.Height - monitorRect.Height);

            if (widthDifference > tolerance || heightDifference > tolerance)
            {
                return false;
            }

            int leftDifference = Math.Abs(windowRect.left - monitorRect.left);
            int topDifference = Math.Abs(windowRect.top - monitorRect.top);

            return leftDifference <= tolerance && topDifference <= tolerance;
        }

        private static bool IsSizeWithinToleranceEnhanced(RECT rect, RECT referenceRect, int tolerance)
        {
            int widthDifference = Math.Abs(rect.Width - referenceRect.Width);
            int heightDifference = Math.Abs(rect.Height - referenceRect.Height);
            return widthDifference <= tolerance && heightDifference <= tolerance;
        }

        private static string? TryGetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private static string[] BuildSystemPathPrefixes()
        {
            var prefixes = new List<string>();

            void AddIfValid(string? path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    prefixes.Add(NormalizePathEnhanced(path));
                }
            }

            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            AddIfValid(windowsDir);
            AddIfValid(Environment.SystemDirectory);

            if (!string.IsNullOrWhiteSpace(windowsDir))
            {
                AddIfValid(Path.Combine(windowsDir, "SystemApps"));
                AddIfValid(Path.Combine(windowsDir, "WinSxS"));
            }

            return prefixes.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string NormalizePathEnhanced(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToLowerInvariant();
            }
            catch
            {
                return path.ToLowerInvariant();
            }
        }

        private string? GetProcessName(uint pid)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch { return null; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowLong(HWND hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(HWND hWnd, IntPtr lParam);

        /// <summary>
        /// Disposes the service and releases resources.
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
        }
    }
}