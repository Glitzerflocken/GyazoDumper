using Microsoft.Win32;
using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Registriert/Deregistriert den Native Messaging Host in Windows
/// 
/// Chrome sucht nach dem Host-Manifest unter:
///   HKCU\SOFTWARE\Google\Chrome\NativeMessagingHosts\{name}
/// 
/// Das Manifest zeigt auf die ausfuehrbare Datei und definiert
/// welche Chrome Extensions den Host verwenden duerfen.
/// </summary>
public static class NativeHostInstaller
{
    private const string HostName = "com.gyazodumper.nativehost";
    private const string ChromeRegistryPath = @"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName;
    private const string EdgeRegistryPath = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName;

    /// <summary>
    /// Installiert den Native Messaging Host
    /// </summary>
    public static void Install()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte Anwendungspfad nicht ermitteln");

        // Manifest erstellen
        var manifestPath = CreateManifest(exePath);

        // In Registry registrieren (Chrome und Edge)
        RegisterInRegistry(ChromeRegistryPath, manifestPath);
        RegisterInRegistry(EdgeRegistryPath, manifestPath);

        Console.WriteLine($"Manifest erstellt: {manifestPath}");
        Console.WriteLine($"Registry-Eintrag erstellt fuer Chrome und Edge");
    }

    /// <summary>
    /// Deinstalliert den Native Messaging Host
    /// </summary>
    public static void Uninstall()
    {
        // Registry-Eintraege entfernen
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ChromeRegistryPath, false);
            Console.WriteLine("Chrome Registry-Eintrag entfernt");
        }
        catch { /* Ignorieren wenn nicht vorhanden */ }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(EdgeRegistryPath, false);
            Console.WriteLine("Edge Registry-Eintrag entfernt");
        }
        catch { /* Ignorieren wenn nicht vorhanden */ }

        // Manifest-Datei entfernen
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var manifestPath = Path.Combine(appData, "GyazoDumper", $"{HostName}.json");
        
        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
            Console.WriteLine($"Manifest entfernt: {manifestPath}");
        }
    }

    /// <summary>
    /// Erstellt das Native Messaging Manifest
    /// </summary>
    private static string CreateManifest(string exePath)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var manifestDir = Path.Combine(appData, "GyazoDumper");
        Directory.CreateDirectory(manifestDir);

        var manifest = new NativeMessagingManifest
        {
            Name = HostName,
            Description = "GyazoDumper Native Messaging Host - Speichert Gyazo-Bilder an beliebigem Ort",
            Path = exePath,
            Type = "stdio",
            // Leeres Array - Benutzer muss Extension-ID manuell eintragen
            // Format: "chrome-extension://<EXTENSION_ID>/"
            AllowedOrigins = Array.Empty<string>()
        };

        var manifestPath = Path.Combine(manifestDir, $"{HostName}.json");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        var json = JsonSerializer.Serialize(manifest, options);
        File.WriteAllText(manifestPath, json);

        // Hinweis fuer den Benutzer
        Console.WriteLine();
        Console.WriteLine("WICHTIG: Extension-ID eintragen!");
        Console.WriteLine("----------------------------------------");
        Console.WriteLine("1. Oeffne chrome://extensions/");
        Console.WriteLine("2. Finde die GyazoDumper Extension");
        Console.WriteLine("3. Kopiere die Extension-ID");
        Console.WriteLine($"4. Oeffne: {manifestPath}");
        Console.WriteLine("5. Trage die ID in 'allowed_origins' ein:");
        Console.WriteLine("   \"chrome-extension://DEINE_EXTENSION_ID/\"");
        Console.WriteLine();

        return manifestPath;
    }

    /// <summary>
    /// Erstellt einen Registry-Eintrag
    /// </summary>
    private static void RegisterInRegistry(string registryPath, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        key?.SetValue("", manifestPath);
    }
}

/// <summary>
/// Native Messaging Manifest-Struktur
/// </summary>
public class NativeMessagingManifest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
