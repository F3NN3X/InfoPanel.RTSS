// Option 1 with Auto-Enable Benchmark Mode
// Automatically enables RTSS benchmark mode for the target app

#include <windows.h>
#include <stdio.h>
#include <algorithm>
#include <vector>
#include <time.h>
#include <stdarg.h>

// Include SDK headers
#include "../../Include/RTSSSharedMemory.h"
#include "../../Include/MSIAB/MAHMSharedMemory.h"

// Global log file
FILE* g_logFile = NULL;

// Logging function with timestamp
void LogMessage(const char* format, ...) {
    if (!g_logFile) return;
    
    // Get current time
    SYSTEMTIME st;
    GetLocalTime(&st);
    
    // Write timestamp
    fprintf(g_logFile, "[%02d:%02d:%02d.%03d] ", st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    
    // Write formatted message
    va_list args;
    va_start(args, format);
    vfprintf(g_logFile, format, args);
    va_end(args);
    
    fflush(g_logFile); // Force write to disk immediately
}

// Helper function to calculate percentile
float CalculatePercentile(std::vector<DWORD>& sortedFrameTimes, float percentile) {
    if (sortedFrameTimes.empty()) return 0.0f;
    
    size_t index = (size_t)(sortedFrameTimes.size() * percentile);
    if (index >= sortedFrameTimes.size()) index = sortedFrameTimes.size() - 1;
    
    return sortedFrameTimes[index] / 1000.0f; // us to ms
}

int main() {
    printf("##############################################################################\n");
    printf("#  RTSS Frame Time Statistics Monitor v1.0                                  #\n");
    printf("#  (Automatically enables RTSS benchmark mode for target app)               #\n");
    printf("##############################################################################\n\n");

    // Open RTSS with WRITE access to enable benchmark mode
    HANDLE hMapRTSS = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");
    if (!hMapRTSS) {
        printf("[FAIL] RTSSSharedMemoryV2 not available\n");
        printf("Make sure RTSS is running!\n\n");
        printf("Press any key to exit...\n");
        getchar();
        return 1;
    }
    
    RTSS_SHARED_MEMORY* pRTSS = (RTSS_SHARED_MEMORY*)MapViewOfFile(hMapRTSS, FILE_MAP_ALL_ACCESS, 0, 0, 0);
    if (!pRTSS || pRTSS->dwSignature != 0x52545353) {
        printf("[FAIL] RTSS signature invalid\n");
        if (pRTSS) UnmapViewOfFile(pRTSS);
        CloseHandle(hMapRTSS);
        return 1;
    }
    
    printf("[OK] RTSS Connected (v0x%08X) with WRITE access\n", pRTSS->dwVersion);
    
    // Open log file
    char logPath[MAX_PATH];
    GetCurrentDirectoryA(MAX_PATH, logPath);
    strcat_s(logPath, "\\rtss-auto.log");
    
    errno_t err = fopen_s(&g_logFile, logPath, "w");
    if (g_logFile) {
        printf("[OK] Logging to: %s\n", logPath);
        LogMessage("=== RTSS Auto-Enable Benchmark Mode Log Started ===\n");
        LogMessage("RTSS Version: 0x%08X\n", pRTSS->dwVersion);
        LogMessage("App Array Size: %u\n", pRTSS->dwAppArrSize);
        LogMessage("App Entry Size: %u bytes\n", pRTSS->dwAppEntrySize);
        LogMessage("Memory opened with FILE_MAP_ALL_ACCESS (write enabled)\n");
    } else {
        printf("[WARNING] Failed to create log file: %s (error %d)\n", logPath, err);
        printf("Continuing without logging...\n");
    }
    
    printf("\nStarting real-time monitoring with auto-enabled benchmark mode...\n");
    printf("Press Ctrl+C to exit\n\n");
    
    Sleep(1500);
    
    BYTE* pAppBytes = NULL;
    bool benchmarkModeEnabled = false;
    
    // Monitoring loop
    while (true) {
        system("cls");
        
        printf("##############################################################################\n");
        printf("#  Frame Time Statistics with Auto-Enabled Benchmark Mode                  #\n");
        printf("##############################################################################\n\n");
        
        // Access application array
        BYTE* pAppArrayBase = (BYTE*)pRTSS + pRTSS->dwAppArrOffset;
        bool foundApp = false;
        int appsFound = 0;
        
        // Find foreground 3D app
        for (DWORD i = 0; i < pRTSS->dwAppArrSize; i++) {
            pAppBytes = pAppArrayBase + (i * pRTSS->dwAppEntrySize);
            
            DWORD dwProcessID = *(DWORD*)(pAppBytes + 0);
            DWORD dwFlags = *(DWORD*)(pAppBytes + 260);
            
            // Read frame timing fields to detect 3D apps
            DWORD dwTime0 = *(DWORD*)(pAppBytes + 264);
            DWORD dwTime1 = *(DWORD*)(pAppBytes + 268);
            DWORD dwFrames = *(DWORD*)(pAppBytes + 272);
            
            if (dwProcessID != 0) {
                appsFound++;
            }
            
            if (dwProcessID != 0 && dwProcessID == pRTSS->dwLastForegroundAppProcessID) {
                // Check if 3D app
                BOOL is3DApp = (dwTime0 != 0 || dwTime1 != 0 || dwFrames != 0);
                if (is3DApp) {
                    foundApp = true;
                    char* szName = (char*)(pAppBytes + 4);
                    
                    printf("=== Application: %s (PID: %u) ===\n\n", szName, dwProcessID);
                    
                    LogMessage("\n=== Frame Data Snapshot ===\n");
                    LogMessage("App: %s (PID: %u)\n", szName, dwProcessID);
                    LogMessage("dwTime0:     %u\n", dwTime0);
                    LogMessage("dwTime1:     %u\n", dwTime1);
                    LogMessage("dwFrames:    %u\n", dwFrames);
                    
                    // Read current dwStatFlags
                    DWORD* pStatFlags = (DWORD*)(pAppBytes + 284);
                    DWORD currentFlags = *pStatFlags;
                    
                    LogMessage("\n=== Benchmark Mode Check ===\n");
                    LogMessage("dwStatFlags (current): 0x%08X\n", currentFlags);
                    LogMessage("STATFLAG_RECORD bit: %s\n", (currentFlags & STATFLAG_RECORD) ? "SET" : "NOT SET");
                    
                    // Check if benchmark mode is enabled
                    if (!(currentFlags & STATFLAG_RECORD)) {
                        printf("[ACTION] Enabling benchmark mode for this app...\n");
                        LogMessage("[ACTION] Benchmark mode NOT enabled, enabling now...\n");
                        
                        *pStatFlags = currentFlags | STATFLAG_RECORD;
                        benchmarkModeEnabled = true;
                        
                        LogMessage("[SUCCESS] dwStatFlags updated: 0x%08X -> 0x%08X\n", currentFlags, *pStatFlags);
                        printf("[OK] Benchmark mode enabled! (dwStatFlags: 0x%08X -> 0x%08X)\n\n", currentFlags, *pStatFlags);
                        Sleep(500); // Give RTSS time to start recording
                    } else {
                        if (!benchmarkModeEnabled) {
                            printf("[INFO] Benchmark mode already enabled (dwStatFlags: 0x%08X)\n\n", currentFlags);
                            LogMessage("[INFO] Benchmark mode already enabled\n");
                            benchmarkModeEnabled = true;
                        }
                    }
                    
                    // Read frame time buffer (corrected offsets from testing)
                    DWORD* dwStatFrameTimeBuf = (DWORD*)(pAppBytes + 5080);
                    DWORD dwStatFrameTimeBufPos = *(DWORD*)(pAppBytes + 9176);
                    DWORD dwStatFrameTimeBufFramerate = *(DWORD*)(pAppBytes + 9180);
                    
                    DWORD dwFrameTime = *(DWORD*)(pAppBytes + 280);
                    float currentFrameTime = dwFrameTime / 1000.0f;
                    float currentFPS = (dwFrameTime > 0) ? (1000000.0f / dwFrameTime) : 0.0f;
                    
                    LogMessage("\n=== Buffer Info ===\n");
                    LogMessage("  Position:     %u / 1024\n", dwStatFrameTimeBufPos);
                    LogMessage("  Framerate:    %u FPS\n", dwStatFrameTimeBufFramerate);
                    LogMessage("  Current FPS:  %.1f (%.2f ms)\n", currentFPS, currentFrameTime);
                    
                    printf("Current Performance:\n");
                    printf("  FPS:        %.1f\n", currentFPS);
                    printf("  Frame Time: %.2f ms\n\n", currentFrameTime);
                    
                    printf("Buffer Info:\n");
                    printf("  Position:     %u / 1024\n", dwStatFrameTimeBufPos);
                    printf("  Framerate:    %u FPS\n\n", dwStatFrameTimeBufFramerate);
                    
                    // Copy frame times to vector for analysis
                    std::vector<DWORD> frameTimes;
                    frameTimes.reserve(1024);
                    
                    for (int j = 0; j < 1024; j++) {
                        DWORD frameTime = dwStatFrameTimeBuf[j];
                        if (frameTime > 0 && frameTime < 1000000) {
                            frameTimes.push_back(frameTime);
                        }
                    }
                    
                    if (frameTimes.size() > 0) {
                        printf("=== Frame Time Statistics (from %zu samples) ===\n\n", frameTimes.size());
                        LogMessage("\n=== Frame Time Statistics ===\n");
                        LogMessage("Sample count: %zu / 1024\n", frameTimes.size());
                        
                        // Sort for percentile calculations
                        std::vector<DWORD> sortedFrameTimes = frameTimes;
                        std::sort(sortedFrameTimes.begin(), sortedFrameTimes.end());
                        
                        // Calculate statistics
                        DWORD minFrameTime = sortedFrameTimes.front();
                        DWORD maxFrameTime = sortedFrameTimes.back();
                        
                        ULONGLONG sum = 0;
                        for (DWORD ft : frameTimes) {
                            sum += ft;
                        }
                        float avgFrameTime = (float)sum / frameTimes.size() / 1000.0f;
                        
                        // Calculate percentiles
                        float p99_9 = CalculatePercentile(sortedFrameTimes, 0.999f);  // 99.9th (0.1% low)
                        float p99 = CalculatePercentile(sortedFrameTimes, 0.99f);     // 99th (1% low)
                        
                        // Convert to FPS
                        float minFPS = (minFrameTime > 0) ? (1000000.0f / minFrameTime) : 0.0f;
                        float maxFPS = (maxFrameTime > 0) ? (1000000.0f / maxFrameTime) : 0.0f;
                        float avgFPS = (avgFrameTime > 0) ? (1000.0f / avgFrameTime) : 0.0f;
                        float fps_1_percent_low = (p99 > 0) ? (1000.0f / p99) : 0.0f;
                        float fps_0_1_percent_low = (p99_9 > 0) ? (1000.0f / p99_9) : 0.0f;
                        
                        // Log all statistics
                        LogMessage("\nFrame Time (ms):\n");
                        LogMessage("  Min:     %6.2f (Max FPS: %.1f)\n", minFrameTime / 1000.0f, maxFPS);
                        LogMessage("  Avg:     %6.2f (Avg FPS: %.1f)\n", avgFrameTime, avgFPS);
                        LogMessage("  Max:     %6.2f (Min FPS: %.1f)\n", maxFrameTime / 1000.0f, minFPS);
                        LogMessage("  99th%%:   %6.2f (1%% Low:  %.1f FPS)\n", p99, fps_1_percent_low);
                        LogMessage("  99.9th%%: %6.2f (0.1%% Low: %.1f FPS)\n", p99_9, fps_0_1_percent_low);
                        
                        LogMessage("\nSummary:\n");
                        LogMessage("  Average FPS:    %6.1f\n", avgFPS);
                        LogMessage("  1%% Low FPS:     %6.1f\n", fps_1_percent_low);
                        LogMessage("  0.1%% Low FPS:   %6.1f\n", fps_0_1_percent_low);
                        
                        printf("Frame Time:\n");
                        printf("  Min:     %6.2f ms  (Max FPS: %.1f)\n", minFrameTime / 1000.0f, maxFPS);
                        printf("  Avg:     %6.2f ms  (Avg FPS: %.1f)\n", avgFrameTime, avgFPS);
                        printf("  Max:     %6.2f ms  (Min FPS: %.1f)\n", maxFrameTime / 1000.0f, minFPS);
                        printf("  99th%%:   %6.2f ms  (1%% Low:  %.1f FPS)\n", p99, fps_1_percent_low);
                        printf("  99.9th%%: %6.2f ms  (0.1%% Low: %.1f FPS)\n\n", p99_9, fps_0_1_percent_low);
                        
                        printf("Summary:\n");
                        printf("  Average FPS:    %6.1f\n", avgFPS);
                        printf("  1%% Low FPS:     %6.1f\n", fps_1_percent_low);
                        printf("  0.1%% Low FPS:   %6.1f\n", fps_0_1_percent_low);
                    } else {
                        printf("\n[INFO] Waiting for frame time data to accumulate...\n");
                        printf("(Buffer fills over time as game runs)\n");
                        LogMessage("\n[INFO] No frame time data yet (buffer empty or filling)\n");
                    }
                    
                    break;
                }
            }
        }
        
        printf("\nTotal apps found: %d\n", appsFound);
        
        if (!foundApp) {
            printf("\n[INFO] No 3D application detected in foreground.\n");
            printf("Launch a game to see statistics.\n");
            if (benchmarkModeEnabled) {
                LogMessage("\n[INFO] App lost/closed - no 3D app detected\n");
                benchmarkModeEnabled = false;
            }
        }
        
        printf("\n\n[Press Ctrl+C to exit]\n");
        Sleep(1000);
    }
    
    // Cleanup
    if (g_logFile) {
        LogMessage("\n=== Log Ended ===\n");
        fclose(g_logFile);
    }
    UnmapViewOfFile(pRTSS);
    CloseHandle(hMapRTSS);
    return 0;
}
