using System.Globalization;

namespace InfoPanel.RTSS.Services
{
    /// <summary>
    /// Service for reading configuration from InfoPanel.RTSS.ini file.
    /// </summary>
    public class ConfigurationService
    {
        private readonly string _configFilePath;
        private readonly Dictionary<string, Dictionary<string, string>> _configData;

        public ConfigurationService()
        {
            // Try multiple possible locations for the config file
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath) ?? Environment.CurrentDirectory;
            
            // First try the assembly directory (where the plugin DLL is)
            _configFilePath = Path.Combine(assemblyDirectory, "InfoPanel.RTSS.ini");
            
            // Note: Cannot use file logger here as it depends on configuration being loaded first
            
            // If not found there, try the InfoPanel plugin data directory
            if (!File.Exists(_configFilePath))
            {
                var infoPanelConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "InfoPanel", "plugins", "InfoPanel.RTSS", "InfoPanel.RTSS.ini");
                
                // Config search logic - no console output to keep InfoPanel clean
                
                if (File.Exists(infoPanelConfigPath))
                {
                    _configFilePath = infoPanelConfigPath;
                }
                else
                {
                    // Creating default config - no console output to keep InfoPanel clean
                    CreateDefaultConfigFile();
                }
            }
            
            _configData = LoadConfiguration();
        }



        /// <summary>
        /// Whether RTSS monitoring should be used (always true in v1.1.6+).
        /// Legacy property kept for configuration compatibility.
        /// </summary>
        [Obsolete("RTSS is now the only monitoring method. This property is kept for config compatibility.")]
        public bool UsePresentMon => true; // Always true, RTSS is the only method

        /// <summary>
        /// Update interval in milliseconds.
        /// </summary>
        public int UpdateInterval => GetIntValue("Display", "updateInterval", 1000);

        /// <summary>
        /// Number of frames to use for smoothing calculations.
        /// </summary>
        public int SmoothingFrames => GetIntValue("Display", "smoothingFrames", 120);

        /// <summary>
        /// Whether debug logging to debug.log file is enabled.
        /// </summary>
        public bool IsDebugEnabled => GetBoolValue("Debug", "debug", false);

        /// <summary>
        /// The default message to display when no game is being captured.
        /// </summary>
        public string DefaultCaptureMessage => GetStringValue("Display", "defaultCaptureMessage", "Nothing to capture");

        /// <summary>
        /// Comma-separated list of process names to ignore (case-insensitive, without .exe).
        /// </summary>
        public string IgnoredProcesses => GetStringValue("Application_Filtering", "ignored_processes", "");

        /// <summary>
        /// Minimum FPS threshold for applications to be considered (applications below this are ignored).
        /// </summary>
        public double MinimumFpsThreshold => GetDoubleValue("Application_Filtering", "minimum_fps_threshold", 1.0);

        /// <summary>
        /// Whether to prefer fullscreen applications over windowed ones.
        /// </summary>
        public bool PreferFullscreen => GetBoolValue("Application_Filtering", "prefer_fullscreen", true);

        /// <summary>
        /// Gets custom game category rules from the configuration.
        /// Returns a dictionary where key is the category name and value is a list of process patterns.
        /// </summary>
        public Dictionary<string, List<string>> GetCustomGameCategories()
        {
            var categories = new Dictionary<string, List<string>>();
            
            // Look for all sections that start with "Game_Category_"
            foreach (var sectionName in _configData.Keys)
            {
                if (sectionName.StartsWith("Game_Category_", StringComparison.OrdinalIgnoreCase))
                {
                    var categoryName = sectionName.Substring("Game_Category_".Length);
                    var patterns = new List<string>();
                    
                    var section = _configData[sectionName];
                    foreach (var kvp in section)
                    {
                        // Support multiple patterns: pattern1, pattern2, etc. OR processes=pattern1,pattern2,pattern3
                        if (kvp.Key.Equals("processes", StringComparison.OrdinalIgnoreCase))
                        {
                            // Comma-separated list in processes key
                            var processPatterns = kvp.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim().ToLowerInvariant())
                                .Where(p => !string.IsNullOrEmpty(p));
                            patterns.AddRange(processPatterns);
                        }
                        else if (kvp.Key.StartsWith("pattern", StringComparison.OrdinalIgnoreCase) || 
                                 kvp.Key.StartsWith("process", StringComparison.OrdinalIgnoreCase))
                        {
                            // Individual pattern keys: pattern1=game.exe, pattern2=*steam*, etc.
                            if (!string.IsNullOrWhiteSpace(kvp.Value))
                            {
                                patterns.Add(kvp.Value.Trim().ToLowerInvariant());
                            }
                        }
                    }
                    
                    if (patterns.Count > 0)
                    {
                        categories[categoryName] = patterns;
                    }
                }
            }
            
            return categories;
        }

        /// <summary>
        /// Loads configuration from INI file.
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> LoadConfiguration()
        {
            var config = new Dictionary<string, Dictionary<string, string>>();

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // Config file not found, using defaults - no console output to keep InfoPanel clean
                    return config;
                }

                // Loading configuration - no console output to keep InfoPanel clean

                var lines = File.ReadAllLines(_configFilePath);
                string? currentSection = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    // Check for section headers
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine[1..^1];
                        if (!config.ContainsKey(currentSection))
                        {
                            config[currentSection] = new Dictionary<string, string>();
                        }
                        continue;
                    }

                    // Parse key-value pairs
                    if (currentSection != null && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            config[currentSection][key] = value;
                        }
                    }
                }

                // Configuration sections loaded - no console output to keep InfoPanel clean
            }
            catch (Exception)
            {
                // Configuration loading error - no console output to keep InfoPanel clean
                // Stack trace suppressed to keep InfoPanel clean
            }

            return config;
        }

        /// <summary>
        /// Logs current configuration settings (called after _configData is initialized).
        /// </summary>
        public void LogCurrentSettings()
        {
            try
            {
                // Configuration values loaded - no console output to keep InfoPanel clean
                // Debug status, update interval, smoothing frames, and capture message loaded
                // Values available through properties but not logged to console
                // File logger not available during configuration initialization
            }
            catch (Exception)
            {
                // Settings reading error - no console output to keep InfoPanel clean
            }
        }

        /// <summary>
        /// Gets a boolean value from configuration.
        /// </summary>
        private bool GetBoolValue(string section, string key, bool defaultValue)
        {
            if (_configData.TryGetValue(section, out var sectionData) && 
                sectionData.TryGetValue(key, out var value))
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets an integer value from configuration.
        /// </summary>
        private int GetIntValue(string section, string key, int defaultValue)
        {
            if (_configData.TryGetValue(section, out var sectionData) && 
                sectionData.TryGetValue(key, out var value))
            {
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    return intValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets a double value from configuration.
        /// </summary>
        private double GetDoubleValue(string section, string key, double defaultValue)
        {
            if (_configData.TryGetValue(section, out var sectionData) && 
                sectionData.TryGetValue(key, out var value))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    return doubleValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets a string value from configuration.
        /// </summary>
        private string GetStringValue(string section, string key, string defaultValue)
        {
            if (_configData.TryGetValue(section, out var sectionData) && 
                sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// Creates a default configuration file.
        /// </summary>
        private void CreateDefaultConfigFile()
        {
            try
            {
                var defaultConfig = @"[FPS_Monitoring]
# InfoPanel.RTSS v1.1.6+ uses RTSS (RivaTuner Statistics Server) exclusively
# RTSS must be running for FPS monitoring to work
# Download RTSS: https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html
# Or install MSI Afterburner (includes RTSS): https://www.msi.com/Landing/afterburner

[Debug]
# Enable/disable debug logging to debug.log file
# Set to true to enable detailed logging for troubleshooting
# Set to false to disable logging for production use
debug=true

[Display]
# Update interval in milliseconds (1000 = 1 second)
updateInterval=1000

# Number of frames to use for smoothing calculations (used for 1% low calculation)
smoothingFrames=100

# Default message to display when no game is being captured
defaultCaptureMessage=Nothing to capture

[Application_Filtering]
# Comma-separated list of process names to ignore (case-insensitive, without .exe)
# Example: StreamDeck,Teams,Teams.msix,Discord,Chrome
ignored_processes=

# Only monitor applications with FPS above this threshold
# Applications with 0.0 FPS (like Stream Deck) will be ignored
minimum_fps_threshold=1.0

# Prefer fullscreen applications over windowed ones
prefer_fullscreen=true";

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(_configFilePath, defaultConfig);
                // Default config file created - no console output to keep InfoPanel clean
            }
            catch (Exception)
            {
                // Failed to create default config file - no console output to keep InfoPanel clean
            }
        }

        /// <summary>
        /// Reloads configuration from file.
        /// </summary>
        public void ReloadConfiguration()
        {
            _configData.Clear();
            var newConfig = LoadConfiguration();
            
            foreach (var section in newConfig)
            {
                _configData[section.Key] = section.Value;
            }
            
            // Configuration reloaded - no console output to keep InfoPanel clean
        }
    }
}