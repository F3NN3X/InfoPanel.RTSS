using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using InfoPanel.Presentmon.Models;
using Vanara.PInvoke;

namespace InfoPanel.Presentmon.Services
{
    public delegate bool EnumWindowsProc(HWND hWnd, IntPtr lParam);

    public class FullscreenDetectionService : IDisposable
    {
        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const int FullscreenTolerance = 12;

        private readonly HashSet<string> _processNameBlacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "audiodg", "backgroundtaskhost", "csrss", "ctfmon", "dasHost", "dllhost", "dwm",
            "explorer", "fontdrvhost", "gamebar", "gamebarftserver", "infopanel", "lsass",
            "mobsync", "msedge", "onedrive", "runtimebroker", "searchapp", "searchui",
            "services", "shellexperiencehost", "sihost", "sppsvc", "spoolsv", "startmenuexperiencehost",
            "steam", "system", "systemsettings", "taskhostw", "taskmgr", "textinputhost", "wininit",
            "winlogon", "wmpnetwk", "wudfhost",
            // Additional system processes
            "svchost", "conhost", "smss", "csrss", "wininit", "services", "lsass", "winlogon",
            "fontdrvhost", "dwm", "spoolsv", "msdtc", "dfssvc", "dns", "eventlog", "eventcreate",
            "gpsvc", "ikeext", "iphlpsvc", "keyiso", "kdc", "wkssvc", "lanmanserver", "lanmanworkstation",
            "lltdsvc", "lmhosts", "mpssvc", "msiserver", "napagent", "netlogon", "netman", "netprofm",
            "nlasvc", "nsi", "p2psvc", "pla", "plugplay", "policysvr", "profsvc", "protectedstorage",
            "rasauto", "rasman", "remoteaccess", "rpcss", "rsvp", "samss", "scardsvr", "schedule",
            "seclogon", "sens", "sessionenv", "sharedaccess", "shellhwdetection", "sis", "slsvc",
            "snmptrap", "spooler", "ssdpsrv", "stisvc", "swprv", "sysmain", "tabletpcinputservice",
            "tapisrv", "termdd", "termservice", "themes", "threadorder", "tiledatamodelsvc", "tlntsvr",
            "tmgmta", "tpm", "tpmd", "tpmd", "trkwks", "trustedinstaller", "tssdis", "tssdjet",
            "ui0detect", "umrdp", "upnphost", "upnp", "vaultsvc", "vds", "vmms", "vss", "w32time",
            "w3svc", "w3wp", "wbengine", "wcspluginservice", "wcncsvc", "webclient", "wecsvc",
            "wephostsvc", "wer", "wersvc", "wiaserv", "wlansvc", "wlidsvc", "wmi", "wmiapsrv",
            "wmic", "wmiprvse", "wmpnetworksvc", "wmsvc", "workfolderssvc", "wpcsvc", "wpdbusenum",
            "wsd", "wsearch", "wuauserv", "wudfsvc", "wudf", "wudfhost", "wudf", "xmlprov", "zeroconf",
            // Xbox and gaming related
            "xbox", "xboxgamebar", "xboxgamebarwidgets", "gamebar", "gamebarftserver"
        };

        private readonly string[] _systemPathPrefixes;
        private readonly uint _selfPid;
        private readonly int _selfSessionId;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowLong(HWND hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId);

        public FullscreenDetectionService()
        {
            _selfPid = (uint)Process.GetCurrentProcess().Id;
            _selfSessionId = Process.GetCurrentProcess().SessionId;
            _systemPathPrefixes = BuildSystemPathPrefixes();
        }

        public Task<MonitoringState?> DetectFullscreenProcessAsync()
        {
            return Task.Run(() =>
            {
                MonitoringState? detectedState = null;
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        if (IsFullscreenWindow(hWnd))
                        {
                            GetWindowThreadProcessId(hWnd, out uint pid);
                            if (pid != _selfPid && !IsBlacklistedProcess(pid))
                            {
                                var processName = GetProcessName(pid);
                                var windowTitle = GetWindowTitle(hWnd);
                                // Only log when we actually detect a fullscreen game, not every window
                                if (detectedState == null)
                                {
                                    Console.WriteLine($"Detected fullscreen window: {windowTitle} (PID: {pid}, Process: {processName})");
                                }
                                if (!string.IsNullOrEmpty(processName))
                                {
                                    detectedState = new MonitoringState
                                    {
                                        ProcessId = pid,
                                        ProcessName = processName,
                                        WindowTitle = windowTitle ?? processName,
                                        IsMonitoring = false
                                    };
                                    return false; // Stop enumeration when we find a fullscreen window
                                }
                            }
                        }
                    }
                    catch { }
                    return true; // Continue enumeration
                }, IntPtr.Zero);
                return detectedState;
            });
        }

        public Task<bool> IsProcessValidAsync(uint pid)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var process = Process.GetProcessById((int)pid);
                    return !process.HasExited;
                }
                catch { return false; }
            });
        }

        private bool IsFullscreenWindow(HWND hWnd)
        {
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

                if (!IsWithinTolerance(windowRect, monitorRect, FullscreenTolerance))
                {
                    return false;
                }

                uint style = GetWindowLong(hWnd, GWL_STYLE);
                bool hasBorder = (style & WS_CAPTION) != 0 || (style & WS_THICKFRAME) != 0;

                if (hasBorder && User32.GetClientRect(hWnd, out RECT clientRect))
                {
                    if (!IsSizeWithinTolerance(clientRect, monitorRect, FullscreenTolerance * 2))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch { return false; }
        }

        private bool IsBlacklistedProcess(uint pid)
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

                if (_processNameBlacklist.Contains(processName))
                {
                    return true;
                }

                var processPath = TryGetProcessPath(process);
                if (IsSystemPath(processPath))
                {
                    return true;
                }

                return false;
            }
            catch { return true; }
        }

        private static bool IsWithinTolerance(RECT windowRect, RECT monitorRect, int tolerance)
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

        private static bool IsSizeWithinTolerance(RECT rect, RECT referenceRect, int tolerance)
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

        private bool IsSystemPath(string? processPath)
        {
            if (string.IsNullOrWhiteSpace(processPath))
            {
                return false;
            }

            var normalized = NormalizePath(processPath);
            foreach (var prefix in _systemPathPrefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] BuildSystemPathPrefixes()
        {
            var prefixes = new List<string>();

            void AddIfValid(string? path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    prefixes.Add(NormalizePath(path));
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

        private static string NormalizePath(string path)
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

        private string? GetWindowTitle(HWND hWnd)
        {
            try
            {
                var sb = new StringBuilder(256);
                int length = GetWindowText(hWnd, sb, sb.Capacity);
                return length > 0 ? sb.ToString() : null;
            }
            catch { return null; }
        }

        public void Dispose() { }
    }
}
