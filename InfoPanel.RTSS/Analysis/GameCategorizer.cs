using System;
using System.Linq;
using InfoPanel.RTSS.Services;

namespace InfoPanel.RTSS.Analysis
{
    /// <summary>
    /// Categorizes games based on process names, paths, and graphics APIs.
    /// Supports custom user-defined categories and enhanced platform detection.
    /// </summary>
    public static class GameCategorizer
    {
        /// <summary>
        /// Categorizes game type based on custom user rules, process name patterns, and graphics API
        /// </summary>
        public static string GetGameCategory(string processName, string graphicsAPI, ConfigurationService? configService = null)
        {
            var lowerProcessName = processName.ToLowerInvariant();
            
            // First, check custom user-defined categories
            if (configService != null)
            {
                var customCategories = configService.GetCustomGameCategories();
                foreach (var category in customCategories)
                {
                    foreach (var pattern in category.Value)
                    {
                        if (IsPatternMatch(lowerProcessName, pattern))
                        {
                            return category.Key;
                        }
                    }
                }
            }
            
            // Fallback to default categorization logic
            return GetDefaultGameCategory(lowerProcessName, graphicsAPI);
        }
        
        /// <summary>
        /// Enhanced game categorization with process path analysis
        /// </summary>
        public static string GetEnhancedGameCategory(string processName, string processPath, string graphicsAPI)
        {
            var lowerProcessName = processName.ToLowerInvariant();
            var lowerProcessPath = processPath.ToLowerInvariant();
            
            // Steam games detection
            if (lowerProcessPath.Contains("steam") || lowerProcessPath.Contains("steamapps"))
            {
                return graphicsAPI switch
                {
                    "DirectX 12" or "DirectX 11" or "Vulkan" => "Steam AAA",
                    _ => "Steam Indie"
                };
            }
            
            // Epic Games Store detection
            if (lowerProcessPath.Contains("epic games") || lowerProcessPath.Contains("epicgames"))
            {
                return "Epic Games Store";
            }
            
            // Game Pass / Microsoft Store detection
            if (lowerProcessPath.Contains("windowsapps") || lowerProcessPath.Contains("microsoft"))
            {
                return "Game Pass / Microsoft Store";
            }
            
            // Fallback to original categorization
            return GetGameCategory(processName, graphicsAPI);
        }

        /// <summary>
        /// Default game categorization logic (used when no custom categories match)
        /// </summary>
        private static string GetDefaultGameCategory(string lowerProcessName, string graphicsAPI)
        {
            // AAA/Modern games typically use DX11/DX12/Vulkan
            if (graphicsAPI is "DirectX 12" or "DirectX 11" or "Vulkan")
            {
                return "AAA/Modern";
            }
            
            // Indie/Legacy detection
            if (graphicsAPI is "DirectX 9" or "DirectX 8" or "OpenGL")
            {
                return "Indie/Legacy";
            }
            
            // Specific game engine detection
            if (lowerProcessName.Contains("unity") || lowerProcessName.Contains("unreal"))
            {
                return "Engine-Based";
            }
            
            return "Standard";
        }
        
        /// <summary>
        /// Checks if a process name matches a pattern (supports wildcards * and exact matches)
        /// </summary>
        private static bool IsPatternMatch(string processName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;
                
            // Exact match
            if (pattern == processName)
                return true;
                
            // Simple wildcard support
            if (pattern.Contains('*'))
            {
                // Convert simple wildcards to regex-like matching
                var parts = pattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return true; // Pattern was just "*"
                    
                string currentProcess = processName;
                foreach (var part in parts)
                {
                    int index = currentProcess.IndexOf(part, StringComparison.OrdinalIgnoreCase);
                    if (index == -1)
                        return false;
                    currentProcess = currentProcess.Substring(index + part.Length);
                }
                return true;
            }
            
            // Contains match
            return processName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }
}