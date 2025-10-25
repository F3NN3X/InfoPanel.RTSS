using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using InfoPanel.RTSS.Services;

namespace InfoPanel.RTSS.Analysis
{
    /// <summary>
    /// Detects window modes and display properties using Win32 API analysis.
    /// Provides accurate detection of borderless fullscreen vs windowed modes.
    /// </summary>
    public static class WindowModeDetector
    {
        // Win32 API Constants for Enhanced Window Detection
        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_CHILD = 0x40000000;
        private const uint WS_MAXIMIZE = 0x01000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;

        // Win32 API DLL Imports for Enhanced Window Detection
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Determines display mode based on fullscreen state and window properties
        /// </summary>
        public static string GetDisplayMode(bool isFullscreen, int resX, int resY, double refreshRate)
        {
            if (isFullscreen)
            {
                // True fullscreen typically matches native resolution
                if (resX >= 1920 && resY >= 1080) // Modern fullscreen resolutions
                {
                    return "Fullscreen";
                }
                return "Fullscreen (Low-Res)";
            }
            else
            {
                // Windowed modes
                if (resX >= 1920 && resY >= 1080)
                {
                    return "Borderless Windowed";
                }
                return "Windowed";
            }
        }

        /// <summary>
        /// Enhanced display mode detection using Win32 API window style analysis
        /// Provides more accurate detection of borderless fullscreen vs windowed modes
        /// </summary>
        public static string GetEnhancedDisplayMode(int processId, bool isFullscreen, double refreshRate, FileLoggingService? logger = null)
        {
            try
            {
                // Get the main window handle for the process
                var process = Process.GetProcessById(processId);
                IntPtr mainWindow = process.MainWindowHandle;
                
                if (mainWindow == IntPtr.Zero)
                {
                    logger?.LogInfo($"GetEnhancedDisplayMode: No main window for PID {processId}, falling back to basic detection");
                    return GetDisplayMode(isFullscreen, 0, 0, refreshRate);
                }

                // Get window style information
                uint windowStyle = GetWindowLong(mainWindow, GWL_STYLE);
                
                // Get window and client rectangles
                if (!User32.GetWindowRect(mainWindow, out var windowRect))
                {
                    logger?.LogInfo($"GetEnhancedDisplayMode: Failed to get window rect for PID {processId}");
                    return GetDisplayMode(isFullscreen, 0, 0, refreshRate);
                }

                if (!GetClientRect(mainWindow, out var clientRect))
                {
                    logger?.LogInfo($"GetEnhancedDisplayMode: Failed to get client rect for PID {processId}");
                    return GetDisplayMode(isFullscreen, 0, 0, refreshRate);
                }

                // Calculate window dimensions
                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;
                int clientWidth = clientRect.Right - clientRect.Left;
                int clientHeight = clientRect.Bottom - clientRect.Top;

                // Get monitor information
                var monitor = User32.MonitorFromWindow(mainWindow, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new User32.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>();
                
                if (!User32.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    logger?.LogInfo($"GetEnhancedDisplayMode: Failed to get monitor info for PID {processId}");
                    return GetDisplayMode(isFullscreen, 0, 0, refreshRate);
                }

                int monitorWidth = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
                int monitorHeight = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;

                // Enhanced analysis
                bool hasPopupStyle = (windowStyle & WS_POPUP) != 0;
                bool hasOverlappedStyle = (windowStyle & WS_OVERLAPPEDWINDOW) != 0;
                bool isMaximized = (windowStyle & WS_MAXIMIZE) != 0;
                bool isChild = (windowStyle & WS_CHILD) != 0;

                // Window and client area match monitor size (borderless fullscreen indicator)
                bool windowMatchesMonitor = (windowWidth == monitorWidth && windowHeight == monitorHeight);
                bool clientMatchesMonitor = (clientWidth == monitorWidth && clientHeight == monitorHeight);
                
                // Window positioned at monitor origin (0,0 relative to monitor)
                bool atMonitorOrigin = (windowRect.Left == monitorInfo.rcMonitor.left && windowRect.Top == monitorInfo.rcMonitor.top);

                // Calculate decoration size
                int decorationHeight = windowHeight - clientHeight;
                int decorationWidth = windowWidth - clientWidth;

                // **ENHANCED DEBUG LOGGING** - This will help us understand what's happening
                logger?.LogInfo($"=== DETAILED WINDOW ANALYSIS for PID {processId} ===");
                logger?.LogInfo($"Window Style: 0x{windowStyle:X8}");
                logger?.LogInfo($"Style Flags: Popup={hasPopupStyle}, Overlapped={hasOverlappedStyle}, Maximized={isMaximized}, Child={isChild}");
                logger?.LogInfo($"Window Rect: {windowRect.Left},{windowRect.Top} to {windowRect.Right},{windowRect.Bottom} (Size: {windowWidth}x{windowHeight})");
                logger?.LogInfo($"Client Rect: {clientRect.Left},{clientRect.Top} to {clientRect.Right},{clientRect.Bottom} (Size: {clientWidth}x{clientHeight})");
                logger?.LogInfo($"Monitor: {monitorInfo.rcMonitor.left},{monitorInfo.rcMonitor.top} to {monitorInfo.rcMonitor.right},{monitorInfo.rcMonitor.bottom} (Size: {monitorWidth}x{monitorHeight})");
                logger?.LogInfo($"Decorations: Width={decorationWidth}px, Height={decorationHeight}px");
                logger?.LogInfo($"Position: AtOrigin={atMonitorOrigin}");
                logger?.LogInfo($"Size Match: Window={windowMatchesMonitor}, Client={clientMatchesMonitor}");

                // **CORRECTED DETECTION LOGIC** - Based on actual debug log patterns
                
                // 1. BORDERLESS FULLSCREEN - Has Popup style (0x94000000 pattern)
                if (hasPopupStyle && !hasOverlappedStyle && !isMaximized && !isChild &&
                    windowMatchesMonitor && clientMatchesMonitor && atMonitorOrigin)
                {
                    logger?.LogInfo($">>> DETECTION: BORDERLESS FULLSCREEN (popup style, matches monitor)");
                    return "Borderless Fullscreen";
                }
                
                // 2. EXCLUSIVE FULLSCREEN - No Popup, No Overlapped (0x14000000 pattern)
                if (!hasPopupStyle && !hasOverlappedStyle && !isMaximized && !isChild &&
                    windowMatchesMonitor && clientMatchesMonitor && atMonitorOrigin)
                {
                    logger?.LogInfo($">>> DETECTION: EXCLUSIVE FULLSCREEN (no popup, no overlapped, matches monitor)");
                    return "Exclusive Fullscreen";
                }
                
                // 3. MAXIMIZED WINDOW - Has maximize flag set
                if (isMaximized)
                {
                    logger?.LogInfo($">>> DETECTION: MAXIMIZED WINDOW (maximize flag set)");
                    return "Maximized Window";
                }
                
                // 4. STANDARD WINDOWED - Has overlapped window style (0x14CF0000 pattern)
                if (hasOverlappedStyle)
                {
                    if (windowWidth >= 1920 && windowHeight >= 1080)
                    {
                        logger?.LogInfo($">>> DETECTION: BORDERED (large overlapped window)");
                        return "Bordered";
                    }
                    else
                    {
                        logger?.LogInfo($">>> DETECTION: WINDOWED (small overlapped window)");
                        return "Windowed";
                    }
                }
                
                // 5. POPUP WINDOWS - Popup style but smaller than monitor
                if (hasPopupStyle && (windowWidth < monitorWidth * 0.95 || windowHeight < monitorHeight * 0.95))
                {
                    logger?.LogInfo($">>> DETECTION: POPUP WINDOW (popup style, smaller than monitor)");
                    return $"Popup Window ({windowWidth}x{windowHeight})";
                }
                
                // 6. FALLBACK ANALYSIS
                double sizeRatio = (double)(windowWidth * windowHeight) / (monitorWidth * monitorHeight);
                logger?.LogInfo($"Size ratio: {sizeRatio:F3}");
                
                if (sizeRatio >= 0.98)
                {
                    logger?.LogInfo($">>> DETECTION: UNKNOWN FULLSCREEN (size ratio {sizeRatio:F3} >= 0.98)");
                    return "Unknown Fullscreen";
                }
                else if (sizeRatio >= 0.80)
                {
                    logger?.LogInfo($">>> DETECTION: LARGE WINDOW (size ratio {sizeRatio:F3} >= 0.80)");
                    return "Large Window";
                }
                else
                {
                    logger?.LogInfo($">>> DETECTION: WINDOWED (size ratio {sizeRatio:F3} < 0.80)");
                    return $"Windowed ({windowWidth}x{windowHeight})";
                }
            }
            catch (Exception ex)
            {
                logger?.LogInfo($"GetEnhancedDisplayMode Exception for PID {processId}: {ex.Message}");
                return GetDisplayMode(isFullscreen, 0, 0, refreshRate);
            }
        }

        /// <summary>
        /// Gets the display resolution for the monitor containing the specified window
        /// </summary>
        public static (int width, int height) GetDisplayResolutionForWindow(IntPtr windowHandle)
        {
            try
            {
                var monitor = User32.MonitorFromWindow(windowHandle, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);
                var monitorInfo = new User32.MONITORINFO();
                monitorInfo.cbSize = (uint)Marshal.SizeOf<User32.MONITORINFO>();
                
                if (User32.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    int width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
                    int height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
                    return (width, height);
                }
            }
            catch
            {
                // Ignore errors and return default
            }
            
            // Default to common resolution if detection fails
            return (1920, 1080);
        }
    }
}