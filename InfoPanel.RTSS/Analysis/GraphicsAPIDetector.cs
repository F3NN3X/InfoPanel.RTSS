namespace InfoPanel.RTSS.Analysis
{
    /// <summary>
    /// Detects graphics APIs and process architecture from RTSS shared memory flags.
    /// Handles DirectX, Vulkan, OpenGL detection with architecture analysis.
    /// </summary>
    public static class GraphicsAPIDetector
    {
        // RTSS APPFLAG constants for graphics API detection (from RTSSSharedMemory.h v2.10+)
        private const uint APPFLAG_OGL = 0x00000001;
        private const uint APPFLAG_DD = 0x00000002;
        private const uint APPFLAG_D3D8 = 0x00000003;
        private const uint APPFLAG_D3D9 = 0x00000004;
        private const uint APPFLAG_D3D9EX = 0x00000005;
        private const uint APPFLAG_D3D10 = 0x00000006;
        private const uint APPFLAG_D3D11 = 0x00000007;
        private const uint APPFLAG_D3D12 = 0x00000008;
        private const uint APPFLAG_D3D12AFR = 0x00000009;
        private const uint APPFLAG_VULKAN = 0x0000000A;
        
        // Masks and architecture flags
        private const uint APPFLAG_API_USAGE_MASK = 0x0000FFFF;
        private const uint APPFLAG_ARCHITECTURE_X64 = 0x00010000;
        private const uint APPFLAG_ARCHITECTURE_UWP = 0x00020000;

        /// <summary>
        /// Analyzes RTSS APPFLAG values to determine graphics API (RTSS v2.10+ format)
        /// </summary>
        public static string GetGraphicsAPI(uint rtssFlags)
        {
            // Extract the API value using the API usage mask (lower 16 bits)
            uint apiValue = rtssFlags & APPFLAG_API_USAGE_MASK;
            
            string result = apiValue switch
            {
                APPFLAG_D3D12 => "DirectX 12",
                APPFLAG_D3D12AFR => "DirectX 12 AFR",
                APPFLAG_D3D11 => "DirectX 11", 
                APPFLAG_D3D10 => "DirectX 10",
                APPFLAG_D3D9EX => "DirectX 9Ex",
                APPFLAG_D3D9 => "DirectX 9",
                APPFLAG_D3D8 => "DirectX 8",
                APPFLAG_VULKAN => "Vulkan",
                APPFLAG_OGL => "OpenGL",
                APPFLAG_DD => "DirectDraw",
                _ => "Unknown"
            };
            
            return result;
        }
        
        /// <summary>
        /// Extracts process architecture information from RTSS flags
        /// </summary>
        public static string GetProcessArchitecture(uint rtssFlags)
        {
            bool isX64 = (rtssFlags & APPFLAG_ARCHITECTURE_X64) != 0;
            bool isUWP = (rtssFlags & APPFLAG_ARCHITECTURE_UWP) != 0;
            
            return (isX64, isUWP) switch
            {
                (true, true) => "x64 UWP",
                (true, false) => "x64",
                (false, true) => "UWP",
                (false, false) => "x86"
            };
        }
        
        /// <summary>
        /// Determines architecture type based on graphics API and RTSS flags
        /// </summary>
        public static string GetArchitecture(string graphicsAPI, uint rtssFlags)
        {
            string processArch = GetProcessArchitecture(rtssFlags);
            string apiEra = graphicsAPI switch
            {
                "DirectX 12" or "DirectX 12 AFR" or "Vulkan" => "Modern Low-Level",
                "DirectX 11" => "DirectX 11 Era", 
                "DirectX 10" => "DirectX 10 Era",
                "DirectX 9" or "DirectX 9Ex" => "Legacy DirectX",
                "DirectX 8" => "Legacy DirectX",
                "OpenGL" => "OpenGL",
                "DirectDraw" => "Legacy DirectDraw",
                _ => "Unknown API"
            };
            
            return $"{apiEra} ({processArch})";
        }

        /// <summary>
        /// Detects VSync status based on RTSS flags, FPS, and refresh rate correlation
        /// </summary>
        public static bool GetVSyncStatus(uint rtssFlags, double fps, double refreshRate)
        {
            // Simple VSync detection: if FPS is close to refresh rate (within 5%), likely VSync is on
            if (refreshRate > 0 && fps > 0)
            {
                double fpsRefreshRatio = fps / refreshRate;
                // VSync typically locks to refresh rate or half refresh rate
                return (fpsRefreshRatio >= 0.95 && fpsRefreshRatio <= 1.05) || 
                       (fpsRefreshRatio >= 0.48 && fpsRefreshRatio <= 0.52);
            }
            
            return false;
        }
    }
}