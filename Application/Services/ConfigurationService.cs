using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Verwaltet die Konfiguration der Anwendung
/// Speichert Einstellungen in %APPDATA%\GyazoDumper\config.json
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
    /// Zielverzeichnis fuer gespeicherte Bilder
    /// </summary>
    public string SaveDirectory => _config.SaveDirectory;

    /// <summary>
    /// Muster fuer Dateinamen
    /// Platzhalter: {hash}, {timestamp}, {ext}
    /// </summary>
    public string FileNamePattern => _config.FileNamePattern;

    /// <summary>
    /// Aktualisiert das Speicherverzeichnis und die Ordnerverknuepfung in AppData
    /// </summary>
    public void UpdateSaveDirectory(string newPath)
    {
        _config.SaveDirectory = newPath;
        SaveConfig(_config);

        // Zielordner erstellen und Junction aktualisieren
        try
        {
            Directory.CreateDirectory(newPath);
            NativeHostInstaller.UpdateFolderShortcut(newPath);
        }
        catch { /* Nicht kritisch */ }
    }

    /// <summary>
    /// Laedt die Konfiguration oder erstellt Standardwerte
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
                // Bei Fehler Standardkonfiguration verwenden
            }
        }

        var defaultConfig = CreateDefaultConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    /// <summary>
    /// Erstellt eine Standardkonfiguration
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
    /// Speichert die Konfiguration in die Datei
    /// </summary>
    private void SaveConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, GyazoDumperJsonContext.Default.AppConfig);
        File.WriteAllText(_configPath, json);
    }
}

/// <summary>
/// Konfigurationsmodell
/// </summary>
public class AppConfig
{
    public string SaveDirectory { get; set; } = string.Empty;
    public string FileNamePattern { get; set; } = string.Empty;
}
