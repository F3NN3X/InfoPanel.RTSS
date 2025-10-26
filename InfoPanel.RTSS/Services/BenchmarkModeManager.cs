using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Manages automatic RTSS benchmark mode enablement via shared memory writes.
    /// 
    /// Based on rtss-auto.cpp solution - automatically enables benchmark mode
    /// (dwStatFlags |= STATFLAG_RECORD) for all detected 3D applications.
    /// 
    /// Why: RTSS frame time statistics (dwStatFrameTimeBuf, percentiles) ONLY work
    /// when benchmark mode is enabled. The flag resets to 0x00000000 on app close,
    /// so this manager continuously monitors and re-enables per session.
    /// 
    /// Version: 1.2.0
    /// Credit: rtss-auto.cpp - Auto-Benchmark Mode Discovery
    /// </summary>
    public class BenchmarkModeManager : IDisposable
    {
        // RTSS Shared Memory Constants
        private const string RTSS_SHARED_MEMORY_NAME = "RTSSSharedMemoryV2";
        private const uint RTSS_SIGNATURE = 0x52545353; // 'RTSS'
        private const uint STATFLAG_RECORD = 0x00000001; // Enable frame time recording
        
        // Critical Offset (from rtss-auto.cpp testing)
        private const int OFFSET_DWSTATFLAGS = 284; // Per-app benchmark mode control flags
        
        private Kernel32.SafeHSECTION? _hMapFile;
        private IntPtr _pMemory;
        private bool _hasWriteAccess;
        private readonly FileLoggingService? _fileLogger;
        private readonly object _lock = new object();
        
        public bool IsInitialized { get; private set; }
        public bool HasWriteAccess => _hasWriteAccess;
        
        public BenchmarkModeManager(FileLoggingService? fileLogger = null)
        {
            _fileLogger = fileLogger;
            Initialize();
        }
        
        /// <summary>
        /// Initialize connection to RTSS shared memory with write access.
        /// Falls back to read-only if write access fails.
        /// </summary>
        private void Initialize()
        {
            lock (_lock)
            {
                try
                {
                    _fileLogger?.LogInfo("[BenchmarkMode] Initializing with FILE_MAP_ALL_ACCESS (write permission)...");
                    
                    // Attempt to open with write access (FILE_MAP_ALL_ACCESS)
                    _hMapFile = Kernel32.OpenFileMapping(
                        (Kernel32.FILE_MAP)0x000F001F, // FILE_MAP_ALL_ACCESS = 0x000F001F
                        false,
                        RTSS_SHARED_MEMORY_NAME
                    );
                    
                    if (_hMapFile == null || _hMapFile.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error();
                        _fileLogger?.LogWarning($"[BenchmarkMode] Failed to open with write access (error {error}), trying read-only...");
                        
                        // Fallback to read-only
                        _hMapFile = Kernel32.OpenFileMapping(
                            Kernel32.FILE_MAP.FILE_MAP_READ,
                            false,
                            RTSS_SHARED_MEMORY_NAME
                        );
                        
                        if (_hMapFile == null || _hMapFile.IsInvalid)
                        {
                            _fileLogger?.LogError($"[BenchmarkMode] Failed to open shared memory at all. Error: {Marshal.GetLastWin32Error()}");
                            return;
                        }
                        
                        _hasWriteAccess = false;
                        _fileLogger?.LogWarning("[BenchmarkMode] ⚠ Opened read-only. Auto-enable will NOT work! Run as Administrator for write access.");
                    }
                    else
                    {
                        _hasWriteAccess = true;
                        _fileLogger?.LogInfo("[BenchmarkMode] ✓ Opened with FILE_MAP_ALL_ACCESS (write enabled)");
                    }
                    
                    // Map view with appropriate permissions
                    _pMemory = Kernel32.MapViewOfFile(
                        _hMapFile,
                        _hasWriteAccess ? (Kernel32.FILE_MAP)0x000F001F : Kernel32.FILE_MAP.FILE_MAP_READ,
                        0,
                        0,
                        UIntPtr.Zero
                    );
                    
                    if (_pMemory == IntPtr.Zero)
                    {
                        _fileLogger?.LogError($"[BenchmarkMode] Failed to map view of file. Error: {Marshal.GetLastWin32Error()}");
                        _hMapFile?.Dispose();
                        _hMapFile = null;
                        return;
                    }
                    
                    // Validate signature
                    uint signature = (uint)Marshal.ReadInt32(_pMemory);
                    if (signature != RTSS_SIGNATURE)
                    {
                        _fileLogger?.LogError($"[BenchmarkMode] Invalid signature: 0x{signature:X8} (expected 0x{RTSS_SIGNATURE:X8})");
                        Cleanup();
                        return;
                    }
                    
                    IsInitialized = true;
                    _fileLogger?.LogInfo($"[BenchmarkMode] ✓ Initialization complete. Write access: {_hasWriteAccess}");
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"[BenchmarkMode] Initialization exception: {ex.Message}");
                    Cleanup();
                }
            }
        }
        
        /// <summary>
        /// Enable benchmark mode for a specific application entry.
        /// Returns true if enabled successfully, false if write access denied or error.
        /// 
        /// Algorithm from rtss-auto.cpp:
        /// 1. Read current dwStatFlags at offset 284
        /// 2. Check if STATFLAG_RECORD (0x00000001) is already set
        /// 3. If not set, enable via bitwise OR: dwStatFlags |= STATFLAG_RECORD
        /// 4. Write back to shared memory
        /// </summary>
        /// <param name="pAppBytes">Pointer to RTSS_SHARED_MEMORY_APP_ENTRY</param>
        /// <param name="processName">Application name for logging</param>
        /// <param name="processId">Application PID for logging</param>
        /// <returns>True if enabled or already enabled, false on failure</returns>
        public unsafe bool EnableBenchmarkMode(IntPtr pAppBytes, string processName, uint processId)
        {
            if (!IsInitialized || !_hasWriteAccess)
            {
                return false; // Can't enable without write access
            }
            
            lock (_lock)
            {
                try
                {
                    // Calculate pointer to dwStatFlags (offset 284)
                    IntPtr pStatFlags = IntPtr.Add(pAppBytes, OFFSET_DWSTATFLAGS);
                    
                    // Read current flags
                    uint currentFlags = (uint)Marshal.ReadInt32(pStatFlags);
                    
                    // Check if benchmark mode already enabled
                    bool isEnabled = (currentFlags & STATFLAG_RECORD) != 0;
                    
                    if (isEnabled)
                    {
                        // Already enabled, no action needed
                        return true;
                    }
                    
                    // Enable benchmark mode (set bit 0)
                    uint newFlags = currentFlags | STATFLAG_RECORD;
                    
                    _fileLogger?.LogInfo($"[BenchmarkMode] Enabling for {processName} (PID: {processId})");
                    _fileLogger?.LogInfo($"[BenchmarkMode]   dwStatFlags: 0x{currentFlags:X8} -> 0x{newFlags:X8}");
                    
                    // Write new flags
                    Marshal.WriteInt32(pStatFlags, (int)newFlags);
                    
                    // Verify write succeeded
                    uint verifyFlags = (uint)Marshal.ReadInt32(pStatFlags);
                    if ((verifyFlags & STATFLAG_RECORD) != 0)
                    {
                        _fileLogger?.LogInfo($"[BenchmarkMode] ✓ SUCCESS - Benchmark mode enabled!");
                        return true;
                    }
                    else
                    {
                        _fileLogger?.LogError($"[BenchmarkMode] ✗ FAILED - Write did not take effect (read back: 0x{verifyFlags:X8})");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _fileLogger?.LogError($"[BenchmarkMode] Exception while enabling: {ex.Message}");
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Check if benchmark mode is enabled for a specific application entry.
        /// </summary>
        /// <param name="pAppBytes">Pointer to RTSS_SHARED_MEMORY_APP_ENTRY</param>
        /// <returns>True if enabled, false otherwise</returns>
        public bool IsBenchmarkModeEnabled(IntPtr pAppBytes)
        {
            if (!IsInitialized || pAppBytes == IntPtr.Zero)
            {
                return false;
            }
            
            lock (_lock)
            {
                try
                {
                    IntPtr pStatFlags = IntPtr.Add(pAppBytes, OFFSET_DWSTATFLAGS);
                    uint currentFlags = (uint)Marshal.ReadInt32(pStatFlags);
                    return (currentFlags & STATFLAG_RECORD) != 0;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Get current dwStatFlags value for diagnostic purposes.
        /// </summary>
        public uint? GetStatFlags(IntPtr pAppBytes)
        {
            if (!IsInitialized || pAppBytes == IntPtr.Zero)
            {
                return null;
            }
            
            lock (_lock)
            {
                try
                {
                    IntPtr pStatFlags = IntPtr.Add(pAppBytes, OFFSET_DWSTATFLAGS);
                    return (uint)Marshal.ReadInt32(pStatFlags);
                }
                catch
                {
                    return null;
                }
            }
        }
        
        private void Cleanup()
        {
            if (_pMemory != IntPtr.Zero)
            {
                Kernel32.UnmapViewOfFile(_pMemory);
                _pMemory = IntPtr.Zero;
            }
            
            _hMapFile?.Dispose();
            _hMapFile = null;
            IsInitialized = false;
            _hasWriteAccess = false;
        }
        
        public void Dispose()
        {
            lock (_lock)
            {
                Cleanup();
            }
            GC.SuppressFinalize(this);
        }
    }
}
