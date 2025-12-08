namespace Loupedeck.TimerPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Loupedeck.TimerPlugin.Models;

    /// <summary>
    /// Manages timer configuration persistence using the SDK-recommended plugin data directory.
    /// This follows Loupedeck SDK best practices by:
    /// - Using GetPluginDataDirectory() for storage location
    /// - Implementing FileSystemWatcher for external edit support
    /// - Providing thread-safe singleton access
    /// - Supporting hot-reload of configuration changes
    /// </summary>
    public class TimerConfigurationService
    {
        private static TimerConfigurationService _instance;
        private static readonly object _lock = new object();

        private readonly string _configFilePath;
        private TimerConfiguration _configuration;
        private FileSystemWatcher _fileWatcher;

        public event EventHandler ConfigurationChanged;

        private TimerConfigurationService(string pluginDataDirectory)
        {
            _configFilePath = Path.Combine(pluginDataDirectory, "timer-config.json");
            LoadConfiguration();
            SetupFileWatcher();
        }

        public static TimerConfigurationService Initialize(string pluginDataDirectory)
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new TimerConfigurationService(pluginDataDirectory);
                        // Trigger initial configuration load event
                        _instance.ConfigurationChanged?.Invoke(_instance, EventArgs.Empty);
                    }
                }
            }
            return _instance;
        }

        public static bool IsInitialized => _instance != null;

        public static TimerConfigurationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("TimerConfigurationService must be initialized before accessing Instance");
                }
                return _instance;
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    _configuration = JsonSerializer.Deserialize<TimerConfiguration>(json) ?? new TimerConfiguration();
                    PluginLog.Info($"Loaded {_configuration.Timers.Count} timer(s) from configuration");
                }
                else
                {
                    // Only create defaults if config file doesn't exist at all
                    _configuration = CreateDefaultConfiguration();
                    // Save defaults so they're available immediately
                    SaveConfiguration();
                    PluginLog.Info("Created and saved default timer configuration");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load timer configuration");
                _configuration = CreateDefaultConfiguration();
            }
        }

        private TimerConfiguration CreateDefaultConfiguration()
        {
            return new TimerConfiguration
            {
                Timers = new List<TimerPreset>
                {
                    new TimerPreset
                    {
                        Id = "default-5min",
                        Name = "Quick Break",
                        Hours = 0,
                        Minutes = 5,
                        Seconds = 0,
                        Haptic = "jingle",
                        IsActive = true
                    },
                    new TimerPreset
                    {
                        Id = "default-15min",
                        Name = "Short Session",
                        Hours = 0,
                        Minutes = 15,
                        Seconds = 0,
                        Haptic = "knock",
                        IsActive = true
                    },
                    new TimerPreset
                    {
                        Id = "default-30min",
                        Name = "Work Session",
                        Hours = 0,
                        Minutes = 30,
                        Seconds = 0,
                        Haptic = "ringing",
                        IsActive = true
                    },
                    new TimerPreset
                    {
                        Id = "default-1hour",
                        Name = "Long Session",
                        Hours = 1,
                        Minutes = 0,
                        Seconds = 0,
                        Haptic = "jingle",
                        IsActive = true
                    }
                }
            };
        }

        public void SaveConfiguration()
        {
            try
            {
                var json = JsonSerializer.Serialize(_configuration, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configFilePath, json);
                PluginLog.Info($"Saved {_configuration.Timers.Count} timer(s) to configuration");
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to save timer configuration");
            }
        }

        private void SetupFileWatcher()
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _fileWatcher = new FileSystemWatcher(directory)
                {
                    Filter = Path.GetFileName(_configFilePath),
                    NotifyFilter = NotifyFilters.LastWrite
                };

                _fileWatcher.Changed += (s, e) =>
                {
                    System.Threading.Thread.Sleep(100); // Debounce
                    LoadConfiguration();
                };

                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to setup file watcher");
            }
        }

        public TimerConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public void UpdateConfiguration(TimerConfiguration configuration)
        {
            _configuration = configuration;
            SaveConfiguration();
        }

        public TimerPreset GetTimer(string id)
        {
            return _configuration.Timers.FirstOrDefault(t => t.Id == id);
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }
}
