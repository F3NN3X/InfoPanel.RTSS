// BackgroundMonitor.cpp - Monitor ALL RTSS-hooked applications regardless of focus
// Shows performance data for background games, multi-game monitoring, and 24/7 tracking

#include <windows.h>
#include <stdio.h>
#include <psapi.h>
#include <float.h>

// Include SDK headers
#include "../Include/RTSSSharedMemory.h"

#pragma comment(lib, "psapi.lib")

// Global log file handle
FILE* g_logFile = NULL;

// Log with timestamp
void LogMessage(const char* format, ...) {
    if (!g_logFile) return;
    
    SYSTEMTIME st;
    GetLocalTime(&st);
    
    fprintf(g_logFile, "[%04d-%02d-%02d %02d:%02d:%02d.%03d] ",
            st.wYear, st.wMonth, st.wDay,
            st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    
    va_list args;
    va_start(args, format);
    vfprintf(g_logFile, format, args);
    va_end(args);
    
    fflush(g_logFile);
}

// Initialize logging
BOOL InitLogging(const char* filename) {
    fopen_s(&g_logFile, filename, "w");
    if (!g_logFile) {
        printf("WARNING: Could not open log file: %s\n", filename);
        return FALSE;
    }
    
    SYSTEMTIME st;
    GetLocalTime(&st);
    
    fprintf(g_logFile, "================================================================================\n");
    fprintf(g_logFile, "RTSS Background Application Monitor - Debug Log\n");
    fprintf(g_logFile, "Started: %04d-%02d-%02d %02d:%02d:%02d\n",
            st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    fprintf(g_logFile, "================================================================================\n\n");
    fflush(g_logFile);
    
    return TRUE;
}

// Close logging
void CloseLogging() {
    if (g_logFile) {
        LogMessage("Monitor stopped\n");
        fprintf(g_logFile, "\n================================================================================\n");
        fprintf(g_logFile, "End of log\n");
        fprintf(g_logFile, "================================================================================\n");
        fclose(g_logFile);
        g_logFile = NULL;
    }
}

// Get process name from PID
BOOL GetProcessName(DWORD dwPID, char* szName, DWORD dwSize) {
    HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, dwPID);
    if (!hProcess) return FALSE;
    
    char szPath[MAX_PATH];
    DWORD dwLen = MAX_PATH;
    if (QueryFullProcessImageNameA(hProcess, 0, szPath, &dwLen)) {
        // Extract just filename
        char* pFilename = strrchr(szPath, '\\');
        if (pFilename) {
            strncpy_s(szName, dwSize, pFilename + 1, _TRUNCATE);
        } else {
            strncpy_s(szName, dwSize, szPath, _TRUNCATE);
        }
        CloseHandle(hProcess);
        return TRUE;
    }
    
    CloseHandle(hProcess);
    return FALSE;
}

// Calculate framerate from RTSS timing data
float CalculateFramerate(DWORD dwTime0, DWORD dwTime1, DWORD dwFrames) {
    if (dwTime1 <= dwTime0 || dwFrames == 0) return 0.0f;
    DWORD dwDelta = dwTime1 - dwTime0;
    if (dwDelta == 0) return 0.0f;
    return (float)dwFrames * 1000.0f / (float)dwDelta;
}

// Get API name from flags
const char* GetAPIName(DWORD dwFlags) {
    DWORD apiType = dwFlags & 0x0000FFFF;
    switch (apiType) {
        case 0x00000001: return "OpenGL";
        case 0x00000002: return "DirectDraw";
        case 0x00000003: return "D3D8";
        case 0x00000004: return "D3D9";
        case 0x00000005: return "D3D9Ex";
        case 0x00000006: return "D3D10";
        case 0x00000007: return "D3D11";
        case 0x00000008: return "D3D12";
        case 0x00000009: return "D3D12AFR";
        case 0x0000000A: return "Vulkan";
        default: return "Unknown";
    }
}

// Main monitoring loop
void MonitorAllApps() {
    // Initialize logging
    InitLogging("rtss-background-monitor.log");
    LogMessage("=== RTSS Background Monitor Starting ===\n");
    
    // Open RTSS shared memory
    HANDLE hMapRTSS = OpenFileMapping(FILE_MAP_READ, FALSE, "RTSSSharedMemoryV2");
    if (!hMapRTSS) {
        printf("ERROR: Cannot open RTSSSharedMemoryV2\n");
        printf("Make sure RTSS is running!\n");
        LogMessage("ERROR: Cannot open RTSSSharedMemoryV2 - RTSS not running?\n");
        CloseLogging();
        printf("\nPress any key to exit...\n");
        getchar();
        return;
    }
    
    LogMessage("Successfully opened RTSSSharedMemoryV2\n");
    
    RTSS_SHARED_MEMORY* pMem = 
        (RTSS_SHARED_MEMORY*)MapViewOfFile(hMapRTSS, FILE_MAP_READ, 0, 0, 0);
    
    if (!pMem || pMem->dwSignature != 'RTSS') {
        printf("ERROR: Invalid RTSS shared memory signature\n");
        printf("Expected: 'RTSS' (0x52545353)\n");
        printf("Got:      0x%08X\n", pMem ? pMem->dwSignature : 0);
        LogMessage("ERROR: Invalid signature - Expected 'RTSS', Got 0x%08X\n", pMem ? pMem->dwSignature : 0);
        UnmapViewOfFile(pMem);
        CloseHandle(hMapRTSS);
        CloseLogging();
        printf("\nPress any key to exit...\n");
        getchar();
        return;
    }
    
    LogMessage("RTSS Version: 0x%08X (v%u.%u)\n", 
               pMem->dwVersion,
               (pMem->dwVersion >> 16) & 0xFFFF,
               pMem->dwVersion & 0xFFFF);
    LogMessage("App Array Offset: 0x%08X (%u bytes)\n", pMem->dwAppArrOffset, pMem->dwAppArrOffset);
    LogMessage("App Array Entries: %u (Entry Size: %u bytes, Total: %u bytes)\n", 
               pMem->dwAppArrSize, pMem->dwAppEntrySize, pMem->dwAppArrSize * pMem->dwAppEntrySize);
    
    printf("################################################################\n");
    printf("#        RTSS Background Application Monitor v1.0             #\n");
    printf("#      Monitors ALL hooked apps regardless of focus           #\n");
    printf("################################################################\n\n");
    
    printf("RTSS Version: 0x%08X (v%u.%u)\n", 
           pMem->dwVersion,
           (pMem->dwVersion >> 16) & 0xFFFF,
           pMem->dwVersion & 0xFFFF);
    
    printf("App Array Offset: 0x%08X (%u bytes)\n", pMem->dwAppArrOffset, pMem->dwAppArrOffset);
    printf("App Array Size: %u bytes\n", pMem->dwAppArrSize);
    printf("App Entry Size: %u bytes\n", pMem->dwAppEntrySize);
    printf("Max App Entries: %u\n\n", pMem->dwAppArrSize / pMem->dwAppEntrySize);
    
    printf("DEBUG: Scanning for hooked applications...\n");
    
    // Quick scan to show ALL entries (including empty ones)
    RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY* pAppArray = 
        (RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY*)((LPBYTE)pMem + pMem->dwAppArrOffset);
    
    DWORD dwMaxApps = pMem->dwAppArrSize / pMem->dwAppEntrySize;
    if (dwMaxApps > 256) dwMaxApps = 256;
    
    DWORD dwFoundApps = 0;
    for (DWORD i = 0; i < dwMaxApps; i++) {
        RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY* pApp = 
            (RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY*)((LPBYTE)pAppArray + (i * pMem->dwAppEntrySize));
        
        if (pApp->dwProcessID != 0) {
            char szName[MAX_PATH] = {0};
            GetProcessName(pApp->dwProcessID, szName, sizeof(szName));
            printf("  [%u] PID: %u, Name: %s, Flags: 0x%08X\n", 
                   i, pApp->dwProcessID, szName, pApp->dwFlags);
            dwFoundApps++;
        }
    }
    
    if (dwFoundApps == 0) {
        printf("  WARNING: No applications found in RTSS array!\n");
        printf("  This could mean:\n");
        printf("    1. No games are running with RTSS hooks loaded\n");
        printf("    2. RTSS is not injecting into the game\n");
        printf("    3. The game is using an API RTSS doesn't support\n");
        LogMessage("WARNING: No hooked applications found in array\n");
    } else {
        printf("  Found %u hooked application(s)\n", dwFoundApps);
        LogMessage("Found %u hooked applications at startup\n", dwFoundApps);
    }
    printf("\n");
    
    printf("Monitoring started... (Press Ctrl+C to exit)\n\n");
    LogMessage("=== Monitoring loop started ===\n\n");
    
    Sleep(1500);
    
    DWORD dwLastFrame = pMem->dwOSDFrame;
    DWORD dwUpdateCount = 0;
    
    while (TRUE) {
        // Wait for frame update
        if (pMem->dwOSDFrame == dwLastFrame) {
            Sleep(16); // ~60Hz polling
            continue;
        }
        dwLastFrame = pMem->dwOSDFrame;
        dwUpdateCount++;
        
        system("cls");
        
        printf("################################################################\n");
        printf("#        RTSS Background Application Monitor                  #\n");
        printf("################################################################\n\n");
        printf("Frame: %u | Last Foreground PID: %u\n\n",
               pMem->dwOSDFrame,
               pMem->dwLastForegroundAppProcessID);
        
        // Get application array
        RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY* pAppArray = 
            (RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY*)((LPBYTE)pMem + pMem->dwAppArrOffset);
        
        DWORD dwMaxApps = pMem->dwAppArrSize; // This is the number of entries, NOT bytes!
        if (dwMaxApps > 256) dwMaxApps = 256; // Safety limit
        
        DWORD dwActiveApps = 0;
        DWORD dwActive3DApps = 0;
        
        printf("====================================================================================\n");
        printf("%-6s %-25s %-8s %-9s %-8s %-8s %-8s %-9s %-10s\n",
               "PID", "Name", "FPS", "FrmTime", "Min", "Avg", "Max", "1%Low", "API");
        printf("====================================================================================\n");
        
        // Scan all application entries
        for (DWORD i = 0; i < dwMaxApps; i++) {
            RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY* pApp = 
                (RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_APP_ENTRY*)((LPBYTE)pAppArray + (i * pMem->dwAppEntrySize));
            
            if (pApp->dwProcessID == 0) continue;
            
            dwActiveApps++;
            
            // Get process name
            char szProcessName[MAX_PATH] = {0};
            if (!GetProcessName(pApp->dwProcessID, szProcessName, sizeof(szProcessName))) {
                sprintf_s(szProcessName, "PID_%u", pApp->dwProcessID);
            }
            
            // Check if this is a 3D app (has frame data)
            BOOL bIs3DApp = (pApp->dwTime0 != 0 || pApp->dwTime1 != 0 || pApp->dwFrames != 0);
            if (bIs3DApp) dwActive3DApps++;
            
            // Truncate long names
            if (strlen(szProcessName) > 24) {
                szProcessName[21] = '.';
                szProcessName[22] = '.';
                szProcessName[23] = '.';
                szProcessName[24] = '\0';
            }
            
            // Calculate current framerate
            float fltFPS = CalculateFramerate(pApp->dwTime0, pApp->dwTime1, pApp->dwFrames);
            float fltFrameTime = pApp->dwFrameTime / 1000.0f; // µs to ms
            
            // Get statistics (stored as DWORD * 10)
            float fltMinFPS = pApp->dwStatFramerateMin / 10.0f;
            float fltAvgFPS = pApp->dwStatFramerateAvg / 10.0f;
            float fltMaxFPS = pApp->dwStatFramerateMax / 10.0f;
            float flt1PctLow = pApp->dwStatFramerate1Dot0PercentLow / 10.0f;
            
            // Check if foreground
            BOOL bIsForeground = (pApp->dwProcessID == pMem->dwLastForegroundAppProcessID);
            
            // Get API name
            const char* szAPI = GetAPIName(pApp->dwFlags);
            
            // Log every 3D app update to file (real-time)
            if (bIs3DApp) {
                LogMessage("[Frame %u] Slot=%u PID=%u %s %s | FPS=%.1f FrameTime=%.2fms | Min=%.1f Avg=%.1f Max=%.1f 1%%Low=%.1f | API=%s Res=%ux%u\n",
                           pMem->dwOSDFrame, i, pApp->dwProcessID, szProcessName, 
                           bIsForeground ? "[FG]" : "[BG]",
                           fltFPS, fltFrameTime, fltMinFPS, fltAvgFPS, fltMaxFPS, flt1PctLow,
                           szAPI, pApp->dwResolutionX, pApp->dwResolutionY);
            }
            
            printf("%s%-6u %-25s %7.1f  %8.2f  %7.1f  %7.1f  %7.1f  %8.1f  %-10s%s\n",
                   bIsForeground ? "[*] " : "    ",
                   pApp->dwProcessID,
                   szProcessName,
                   fltFPS,
                   fltFrameTime,
                   fltMinFPS,
                   fltAvgFPS,
                   fltMaxFPS,
                   flt1PctLow,
                   szAPI,
                   bIsForeground ? " [*]" : "");
            
            // Show additional info for foreground app
            if (bIsForeground && pApp->dwResolutionX > 0) {
                printf("     Resolution: %ux%u | ", pApp->dwResolutionX, pApp->dwResolutionY);
                if (pApp->dwFlags & APPFLAG_ARCHITECTURE_X64) {
                    printf("x64 ");
                }
                if (pApp->dwFlags & APPFLAG_ARCHITECTURE_UWP) {
                    printf("UWP ");
                }
                printf("\n");
            }
        }
        
        if (dwActiveApps == 0) {
            printf("  No applications currently hooked by RTSS\n");
            printf("  Start a game or 3D application to see monitoring data\n");
        }
        
        // Log summary every 60 frames (~1 second at 60Hz)
        if (dwUpdateCount % 60 == 0) {
            LogMessage("=== Summary [Frame %u] === Total Apps: %u | 3D Apps: %u | Foreground PID: %u\n",
                       pMem->dwOSDFrame, dwActiveApps, dwActive3DApps, pMem->dwLastForegroundAppProcessID);
        }
        
        printf("====================================================================================\n");
        printf("\nActive Apps: %u (%u with 3D) | Polling Rate: ~60Hz | Background monitoring: ENABLED\n", 
               dwActiveApps, dwActive3DApps);
        printf("Note: [*] indicates foreground application | Apps with 0 FPS are hooked but not 3D\n");
        printf("Log file: rtss-background-monitor.log\n");
        printf("\n[Press Ctrl+C to exit]\n");
        
        Sleep(16);
    }
    
    UnmapViewOfFile(pMem);
    CloseHandle(hMapRTSS);
    CloseLogging();
}

int main() {
    MonitorAllApps();
    return 0;
}
