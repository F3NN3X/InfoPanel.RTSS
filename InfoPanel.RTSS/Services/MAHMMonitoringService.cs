using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using InfoPanel.RTSS.Models;
using Vanara.PInvoke;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Service for accessing MSI Afterburner Hardware Monitoring (MAHM) shared memory.
    /// Provides framerate statistics (Min/Avg/Max/1% Low) without requiring RTSS benchmark mode.
    /// Also provides access to 40+ hardware sensors (CPU/GPU temperature, usage, clocks, etc.).
    /// 
    /// Key advantage over RTSS statistics:
    /// - RTSS benchmark mode auto-disables after game exit (requires manual re-enable)
    /// - MAHM statistics are always available when MSI Afterburner is running
    /// - No user configuration needed beyond running Afterburner
    /// 
    /// Version: 1.2.0
    /// Author: InfoPanel.RTSS Plugin
    /// </summary>
    public class MAHMMonitoringService : IDisposable
    {
        private const string MAHM_SHARED_MEMORY_NAME = "MAHMSharedMemory";
        private const uint MAHM_SIGNATURE = 0x4D48414D; // 'MAHM' in little-endian
        private const uint MAHM_VERSION_2_0 = 0x00020000;

        private Kernel32.SafeHSECTION? _hMapFile;
        private IntPtr _pMemory;
        private bool _isInitialized;
        private readonly FileLoggingService? _fileLoggingService;

        /// <summary>
        /// Indicates whether MAHM shared memory is successfully connected and valid.
        /// </summary>
        public bool IsConnected => _isInitialized && _pMemory != IntPtr.Zero;

        /// <summary>
        /// Number of sensor entries available in MAHM shared memory.
        /// </summary>
        public uint EntryCount { get; private set; }

        /// <summary>
        /// Number of GPU entries available in MAHM shared memory.
        /// </summary>
        public uint GpuEntryCount { get; private set; }

        /// <summary>
        /// MAHM header version (expected: 0x00020000 for v2.0).
        /// </summary>
        public uint Version { get; private set; }

        public MAHMMonitoringService(FileLoggingService? fileLoggingService = null)
        {
            _fileLoggingService = fileLoggingService;
            Initialize();
        }

        /// <summary>
        /// Initializes connection to MAHM shared memory.
        /// Call this once during service startup or when reconnection is needed.
        /// </summary>
        private void Initialize()
        {
            try
            {
                _fileLoggingService?.LogInfo("[MAHM] Opening MAHMSharedMemory file mapping...");

                // Open existing shared memory object (read-only)
                _hMapFile = Kernel32.OpenFileMapping(
                    Kernel32.FILE_MAP.FILE_MAP_READ,
                    false,
                    MAHM_SHARED_MEMORY_NAME
                );

                if (_hMapFile == null || _hMapFile.IsInvalid)
                {
                    _fileLoggingService?.LogError($"[MAHM] Failed to open shared memory. Error: {Marshal.GetLastWin32Error()}");
                    _fileLoggingService?.LogWarning("[MAHM] Make sure MSI Afterburner is running!");
                    return;
                }

                _fileLoggingService?.LogInfo("[MAHM] File mapping opened successfully");

                // Map view of shared memory into process address space
                _pMemory = Kernel32.MapViewOfFile(
                    _hMapFile,
                    Kernel32.FILE_MAP.FILE_MAP_READ,
                    0,
                    0,
                    UIntPtr.Zero
                );

                if (_pMemory == IntPtr.Zero)
                {
                    _fileLoggingService?.LogError($"[MAHM] Failed to map view of file. Error: {Marshal.GetLastWin32Error()}");
                    return;
                }

                _fileLoggingService?.LogInfo($"[MAHM] Memory mapped at 0x{_pMemory:X}");

                // Read and validate header
                var header = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_HEADER>(_pMemory);

                _fileLoggingService?.LogInfo($"[MAHM] Signature: 0x{header.dwSignature:X8} (expected: 0x{MAHM_SIGNATURE:X8})");
                _fileLoggingService?.LogInfo($"[MAHM] Version: 0x{header.dwVersion:X8} (expected: 0x{MAHM_VERSION_2_0:X8})");
                _fileLoggingService?.LogInfo($"[MAHM] Header size: {header.dwHeaderSize} bytes");
                _fileLoggingService?.LogInfo($"[MAHM] Entry count: {header.dwNumEntries}");
                _fileLoggingService?.LogInfo($"[MAHM] Entry size: {header.dwEntrySize} bytes");
                _fileLoggingService?.LogInfo($"[MAHM] GPU entries: {header.dwNumGpuEntries}");
                _fileLoggingService?.LogInfo($"[MAHM] GPU entry size: {header.dwGpuEntrySize} bytes");

                if (header.dwSignature != MAHM_SIGNATURE)
                {
                    _fileLoggingService?.LogError($"[MAHM] Invalid signature! Memory may be corrupted or MSI Afterburner not running properly.");
                    _pMemory = IntPtr.Zero;
                    return;
                }

                if (header.dwVersion < MAHM_VERSION_2_0)
                {
                    _fileLoggingService?.LogError($"[MAHM] Unsupported version! This service requires v2.0 or newer.");
                    _pMemory = IntPtr.Zero;
                    return;
                }

                // Store metadata
                EntryCount = header.dwNumEntries;
                GpuEntryCount = header.dwNumGpuEntries;
                Version = header.dwVersion;

                _isInitialized = true;
                _fileLoggingService?.LogInfo("[MAHM] ✓ Initialization successful");
            }
            catch (Exception ex)
            {
                _fileLoggingService?.LogError($"[MAHM] Initialization exception: {ex.Message}");
                _pMemory = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Reads all sensor entries from MAHM shared memory and returns them as a dictionary.
        /// Dictionary is keyed by dwSrcId (see MAHMMonitoringSourceId constants).
        /// Returns null if MAHM is not connected or an error occurs.
        /// 
        /// Key source IDs for framerate statistics:
        /// - MAHMMonitoringSourceId.FramerateMin (0x00000052): Minimum FPS
        /// - MAHMMonitoringSourceId.FramerateAvg (0x00000053): Average FPS
        /// - MAHMMonitoringSourceId.FramerateMax (0x00000054): Maximum FPS
        /// - MAHMMonitoringSourceId.Framerate1Dot0PercentLow (0x00000055): 1% Low FPS
        /// </summary>
        public Dictionary<uint, float>? GetSensorData()
        {
            if (!IsConnected)
            {
                return null;
            }

            try
            {
                // Read header to get entry array offset
                var header = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_HEADER>(_pMemory);

                // Validate header signature (check for memory corruption or Afterburner restart)
                if (header.dwSignature != MAHM_SIGNATURE)
                {
                    _fileLoggingService?.LogInfo("[MAHM] Signature changed! MSI Afterburner may have restarted. Reinitializing...");
                    Cleanup();
                    Initialize();
                    return null;
                }

                // Calculate pointer to first entry (entries array immediately follows header)
                IntPtr pEntries = IntPtr.Add(_pMemory, (int)header.dwHeaderSize);

                // Create dictionary to store sensor data (dwSrcId -> data value)
                var sensorData = new Dictionary<uint, float>();

                // Read all entries
                int entrySize = (int)header.dwEntrySize;
                for (uint i = 0; i < header.dwNumEntries; i++)
                {
                    IntPtr pEntry = IntPtr.Add(pEntries, (int)(i * entrySize));
                    var entry = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_ENTRY>(pEntry);

                    // Skip entries with invalid data (FLT_MAX indicates data not available)
                    if (entry.data == float.MaxValue)
                    {
                        continue;
                    }

                    // Add to dictionary (if duplicate dwSrcId, keep first occurrence)
                    if (!sensorData.ContainsKey(entry.dwSrcId))
                    {
                        sensorData[entry.dwSrcId] = entry.data;
                    }
                }

                return sensorData;
            }
            catch (Exception ex)
            {
                _fileLoggingService?.LogInfo($"[MAHM] GetSensorData exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a specific sensor value by source ID.
        /// Returns null if sensor is not found or data is unavailable.
        /// 
        /// Example usage:
        /// <code>
        /// float? minFps = GetSensorValue(MAHMMonitoringSourceId.FramerateMin);
        /// float? avgFps = GetSensorValue(MAHMMonitoringSourceId.FramerateAvg);
        /// float? onePercentLow = GetSensorValue(MAHMMonitoringSourceId.Framerate1Dot0PercentLow);
        /// </code>
        /// </summary>
        public float? GetSensorValue(uint sourceId)
        {
            var sensorData = GetSensorData();
            if (sensorData == null)
            {
                return null;
            }

            return sensorData.TryGetValue(sourceId, out float value) ? value : null;
        }

        /// <summary>
        /// Gets framerate statistics from MAHM (replaces RTSS benchmark mode dependency).
        /// Returns null if MAHM is not connected or framerate data is unavailable.
        /// 
        /// Note: MSI Afterburner must be running and framerate monitoring enabled.
        /// Statistics are calculated by Afterburner automatically (no benchmark mode needed).
        /// </summary>
        public (float? minFps, float? avgFps, float? maxFps, float? onePercentLow)? GetFramerateStatistics()
        {
            if (!IsConnected)
            {
                return null;
            }

            var sensorData = GetSensorData();
            if (sensorData == null)
            {
                return null;
            }

            // Extract framerate statistics using MAHM source IDs
            sensorData.TryGetValue(MAHMMonitoringSourceId.FramerateMin, out float minFps);
            sensorData.TryGetValue(MAHMMonitoringSourceId.FramerateAvg, out float avgFps);
            sensorData.TryGetValue(MAHMMonitoringSourceId.FramerateMax, out float maxFps);
            sensorData.TryGetValue(MAHMMonitoringSourceId.Framerate1Dot0PercentLow, out float onePercentLow);

            // Return null if all statistics are zero (no game running or monitoring not started)
            if (minFps == 0 && avgFps == 0 && maxFps == 0 && onePercentLow == 0)
            {
                return null;
            }

            return (minFps, avgFps, maxFps, onePercentLow);
        }

        /// <summary>
        /// Gets GPU information from MAHM (GPU entries array).
        /// Returns null if MAHM is not connected or GPU data is unavailable.
        /// </summary>
        public List<MAHM_SHARED_MEMORY_GPU_ENTRY>? GetGpuEntries()
        {
            if (!IsConnected)
            {
                return null;
            }

            try
            {
                var header = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_HEADER>(_pMemory);

                // Validate signature
                if (header.dwSignature != MAHM_SIGNATURE)
                {
                    return null;
                }

                // Calculate pointer to GPU entries array
                // GPU entries follow immediately after sensor entries array
                int sensorArraySize = (int)(header.dwNumEntries * header.dwEntrySize);
                IntPtr pGpuEntries = IntPtr.Add(_pMemory, (int)header.dwHeaderSize + sensorArraySize);

                var gpuEntries = new List<MAHM_SHARED_MEMORY_GPU_ENTRY>();

                // Read all GPU entries
                int gpuEntrySize = (int)header.dwGpuEntrySize;
                for (uint i = 0; i < header.dwNumGpuEntries; i++)
                {
                    IntPtr pEntry = IntPtr.Add(pGpuEntries, (int)(i * gpuEntrySize));
                    var entry = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_GPU_ENTRY>(pEntry);
                    gpuEntries.Add(entry);
                }

                return gpuEntries;
            }
            catch (Exception ex)
            {
                _fileLoggingService?.LogInfo($"[MAHM] GetGpuEntries exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs all available MAHM sensors to the debug log.
        /// Useful for troubleshooting and discovering available sensor IDs.
        /// </summary>
        public void LogAllSensors()
        {
            if (!IsConnected)
            {
                _fileLoggingService?.LogInfo("[MAHM] Cannot log sensors - not connected");
                return;
            }

            try
            {
                var header = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_HEADER>(_pMemory);
                IntPtr pEntries = IntPtr.Add(_pMemory, (int)header.dwHeaderSize);

                _fileLoggingService?.LogInfo($"[MAHM] === Sensor Dump ({header.dwNumEntries} entries) ===");

                int entrySize = (int)header.dwEntrySize;
                for (uint i = 0; i < header.dwNumEntries; i++)
                {
                    IntPtr pEntry = IntPtr.Add(pEntries, (int)(i * entrySize));
                    var entry = Marshal.PtrToStructure<MAHM_SHARED_MEMORY_ENTRY>(pEntry);

                    if (entry.data == float.MaxValue)
                    {
                        continue; // Skip unavailable sensors
                    }

                    _fileLoggingService?.LogInfo($"[MAHM] [{i}] SrcId=0x{entry.dwSrcId:X8} | {entry.szSrcName} = {entry.data:F2} {entry.szSrcUnits} | GPU={entry.dwGpu}");
                }

                _fileLoggingService?.LogInfo("[MAHM] === End Sensor Dump ===");
            }
            catch (Exception ex)
            {
                _fileLoggingService?.LogInfo($"[MAHM] LogAllSensors exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to reconnect to MAHM shared memory.
        /// Call this if IsConnected becomes false or after MSI Afterburner restart.
        /// </summary>
        public void Reconnect()
        {
            _fileLoggingService?.LogInfo("[MAHM] Reconnecting...");
            Cleanup();
            Initialize();
        }

        /// <summary>
        /// Cleans up MAHM resources without disposing the service.
        /// </summary>
        private void Cleanup()
        {
            if (_pMemory != IntPtr.Zero)
            {
                Kernel32.UnmapViewOfFile(_pMemory);
                _pMemory = IntPtr.Zero;
            }

            _hMapFile?.Dispose();
            _hMapFile = null;
            _isInitialized = false;
        }

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }
    }
}

