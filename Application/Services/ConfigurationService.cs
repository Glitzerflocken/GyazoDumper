using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Manages the application configuration.
/// Stores settings in %APPDATA%\GyazoDumper\config.json.
/// </summary>
public class ConfigurationService
{
    private readonly string _configPath;
    private AppConfig _config;

    public ConfigurationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "GyazoDumper");
        Directory.CreateDirectory(configDir);

        _configPath = Path.Combine(configDir, "config.json");
        _config = LoadOrCreateConfig();
    }

    /// <summary>
    /// Target directory for saved images.
    /// </summary>
    public string SaveDirectory => _config.SaveDirectory;

    /// <summary>
    /// Filename pattern.
    /// Placeholders: {hash}, {timestamp}, {ext}
    /// </summary>
    public string FileNamePattern => _config.FileNamePattern;

    /// <summary>
    /// Updates the save directory and the folder shortcut in AppData.
    /// </summary>
    public void UpdateSaveDirectory(string newPath)
    {
        _config.SaveDirectory = newPath;
        SaveConfig(_config);

        // Create target folder and update junction
        try
        {
            Directory.CreateDirectory(newPath);
            NativeHostInstaller.UpdateFolderShortcut(newPath);
        }
        catch { /* Not critical */ }
    }

    /// <summary>
    /// Loads the configuration or creates default values.
    /// </summary>
    private AppConfig LoadOrCreateConfig()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize(json, GyazoDumperJsonContext.Default.AppConfig);
                if (config != null)
                {
                    return config;
                }
            }
            catch
            {
                // On error, use default configuration
            }
        }

        var defaultConfig = CreateDefaultConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    private static AppConfig CreateDefaultConfig()
    {
        var picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return new AppConfig
        {
            SaveDirectory = Path.Combine(picturesPath, "GyazoDumps"),
            FileNamePattern = "Gyazo_{timestamp}_{hash}{ext}"
        };
    }

    /// <summary>
    /// Saves the configuration to disk.
    /// </summary>
    private void SaveConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, GyazoDumperJsonContext.Default.AppConfig);
        File.WriteAllText(_configPath, json);
    }
}

/// <summary>
/// Configuration model.
/// </summary>
public class AppConfig
{
    public string SaveDirectory { get; set; } = string.Empty;
    public string FileNamePattern { get; set; } = string.Empty;
}
