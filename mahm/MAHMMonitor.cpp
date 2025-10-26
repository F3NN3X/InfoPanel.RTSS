// MAHMMonitor.cpp - Real-time system monitor using RTSS + MSI Afterburner
// Displays CPU, GPU, RAM usage, temperatures, clocks, and performance metrics

#include <windows.h>
#include <stdio.h>
#include <float.h>

// Include SDK headers
#include "RTSSSharedMemory.h"
#include "MAHMSharedMemory.h"

int main() {
    printf("==============================================\n");
    printf("  RTSS + MAHM Real-Time System Monitor\n");
    printf("==============================================\n\n");

    // Open RTSS (framerate data)
    HANDLE hMapRTSS = OpenFileMapping(FILE_MAP_READ, FALSE, "RTSSSharedMemoryV2");
    if (!hMapRTSS) {
        printf("Warning: RTSSSharedMemoryV2 not available\n");
        printf("Make sure RTSS is running\n\n");
    }
    
    RTSS_SHARED_MEMORY* pRTSS = NULL;
    if (hMapRTSS) {
        pRTSS = (RTSS_SHARED_MEMORY*)MapViewOfFile(hMapRTSS, FILE_MAP_READ, 0, 0, 0);
        if (pRTSS && pRTSS->dwSignature == 0x52545353) {
            printf("[OK] RTSS Connected (v0x%08X)\n", pRTSS->dwVersion);
        } else {
            printf("[FAIL] RTSS signature invalid\n");
            pRTSS = NULL;
        }
    }
    
    // Open MAHM (hardware sensors)
    HANDLE hMapMAHM = OpenFileMapping(FILE_MAP_READ, FALSE, "MAHMSharedMemory");
    if (!hMapMAHM) {
        printf("[FAIL] MAHMSharedMemory not available\n");
        printf("Make sure MSI Afterburner is running!\n\n");
        printf("Press any key to exit...\n");
        getchar();
        if (pRTSS) UnmapViewOfFile(pRTSS);
        if (hMapRTSS) CloseHandle(hMapRTSS);
        return 1;
    }
    
    MAHM_SHARED_MEMORY_HEADER* pMAHM = 
        (MAHM_SHARED_MEMORY_HEADER*)MapViewOfFile(hMapMAHM, FILE_MAP_READ, 0, 0, 0);
    
    if (!pMAHM || pMAHM->dwSignature != 'MAHM') {
        printf("[FAIL] MAHM signature invalid\n");
        printf("Expected: 'MAHM' (0x4D48414D)\n");
        printf("Got:      0x%08X\n", pMAHM ? pMAHM->dwSignature : 0);
        printf("\nPress any key to exit...\n");
        getchar();
        if (pMAHM) UnmapViewOfFile(pMAHM);
        if (pRTSS) UnmapViewOfFile(pRTSS);
        if (hMapMAHM) CloseHandle(hMapMAHM);
        if (hMapRTSS) CloseHandle(hMapRTSS);
        return 1;
    }
    
    printf("[OK] MAHM Connected (v0x%08X)\n", pMAHM->dwVersion);
    printf("[OK] Sensor entries: %u\n", pMAHM->dwNumEntries);
    printf("[OK] GPU entries: %u\n\n", pMAHM->dwNumGpuEntries);
    
    printf("Starting real-time monitoring...\n");
    printf("Press Ctrl+C to exit\n\n");
    
    Sleep(1500);
    
    // Monitoring loop
    while (true) {
        system("cls");
        
        printf("╔════════════════════════════════════════════╗\n");
        printf("║  Real-Time System Monitor (RTSS + MAHM)   ║\n");
        printf("╚════════════════════════════════════════════╝\n\n");
        
        // Get MAHM entries
        MAHM_SHARED_MEMORY_ENTRY* entries = 
            (MAHM_SHARED_MEMORY_ENTRY*)((BYTE*)pMAHM + pMAHM->dwHeaderSize);
        
        // Display key metrics
        bool foundData = false;
        for (DWORD i = 0; i < pMAHM->dwNumEntries; i++) {
            if (entries[i].data == FLT_MAX) continue;
            
            foundData = true;
            
            switch (entries[i].dwSrcId) {
                case MONITORING_SOURCE_ID_FRAMERATE:
                    printf("FPS:           %.1f\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMETIME:
                    printf("Frame Time:    %.2f ms\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_CPU_USAGE:
                    printf("CPU Usage:     %.1f%%\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_CPU_TEMPERATURE:
                    printf("CPU Temp:      %.1f°C\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_CPU_CLOCK:
                    printf("CPU Clock:     %.0f MHz\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_GPU_USAGE:
                    printf("GPU Usage:     %.1f%%\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_GPU_TEMPERATURE:
                    printf("GPU Temp:      %.1f°C\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_CORE_CLOCK:
                    printf("GPU Clock:     %.0f MHz\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_MEMORY_CLOCK:
                    printf("VRAM Clock:    %.0f MHz\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_MEMORY_USAGE:
                    printf("VRAM Usage:    %.1f%%\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_RAM_USAGE:
                    printf("RAM Usage:     %.1f%%\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_GPU_ABS_POWER:
                    printf("GPU Power:     %.1f W\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FAN_SPEED:
                    printf("GPU Fan:       %.0f%%\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMERATE_MIN:
                    printf("Min FPS:       %.1f\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMERATE_AVG:
                    printf("Avg FPS:       %.1f\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMERATE_MAX:
                    printf("Max FPS:       %.1f\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMERATE_1DOT0_PERCENT_LOW:
                    printf("1%% Low:        %.1f FPS\n", entries[i].data);
                    break;
                case MONITORING_SOURCE_ID_FRAMERATE_0DOT1_PERCENT_LOW:
                    printf("0.1%% Low:      %.1f FPS\n", entries[i].data);
                    break;
            }
        }
        
        if (!foundData) {
            printf("No sensor data available.\n");
            printf("Make sure MSI Afterburner is running and monitoring is enabled.\n");
        }
        
        printf("\n[Press Ctrl+C to exit]\n");
        Sleep(1000);
    }
    
    // Cleanup (unreachable due to infinite loop, but good practice)
    UnmapViewOfFile(pMAHM);
    UnmapViewOfFile(pRTSS);
    CloseHandle(hMapMAHM);
    CloseHandle(hMapRTSS);
    return 0;
}
