using Microsoft.Win32;
using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Installiert/Deinstalliert den GyazoDumper Native Messaging Host.
/// 
/// Ablauf bei Installation:
///   1. Kopiert die EXE nach %APPDATA%\GyazoDumper\
///   2. Erstellt das Native Messaging Manifest mit Extension-ID
///   3. Registriert den Host in der Windows Registry (Chrome + Edge)
/// 
/// Ablauf bei Deinstallation:
///   1. Entfernt Registry-Eintraege
///   2. Loescht den gesamten %APPDATA%\GyazoDumper\ Ordner
/// </summary>
public static class NativeHostInstaller
{
    private const string HostName = "gyazodumper.nativeApp";
    private const string AppFolderName = "GyazoDumper";
    private const string ExeFileName = "GyazoDumper.exe";
    private const string ChromeRegistryPath = @"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName;
    private const string EdgeRegistryPath = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    private static string InstalledExePath => Path.Combine(AppDataDir, ExeFileName);
    private static string ManifestPath => Path.Combine(AppDataDir, $"{HostName}.json");

    /// <summary>
    /// Interaktiver Setup-Assistent - wird bei Doppelklick auf die EXE ausgefuehrt.
    /// Fuehrt den Benutzer durch die Installation in 3 einfachen Schritten.
    /// </summary>
    public static void InteractiveInstall()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║       GyazoDumper Setup              ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Installationsordner: {AppDataDir}");
        Console.WriteLine();

        // Schritt 1: Dateien kopieren
        Console.Write("  [1/3] Dateien installieren ...       ");
        try
        {
            CopyToAppData();
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            WaitAndExit();
            return;
        }

        // Schritt 2: Extension-ID abfragen
        Console.WriteLine("  [2/3] Extension-ID registrieren");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  Oeffne die GyazoDumper Chrome-Extension und kopiere");
        Console.WriteLine("  die dort angezeigte Extension-ID.");
        Console.ResetColor();
        Console.WriteLine();

        // Bestehende IDs laden und anzeigen
        var existingOrigins = LoadExistingOrigins();
        if (existingOrigins.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Bereits registriert: {string.Join(", ", existingOrigins)}");
            Console.ResetColor();
        }

        Console.Write("  Extension-ID eingeben: ");
        var extensionId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(extensionId))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("  -> Uebersprungen. Du kannst das Setup spaeter erneut ausfuehren.");
            Console.ResetColor();
        }
        else
        {
            // Ungueltige Zeichen entfernen (falls Benutzer mehr als die ID kopiert)
            extensionId = extensionId.Replace("chrome-extension://", "").TrimEnd('/');
        }

        // Schritt 3: Manifest + Registry erstellen
        Console.Write("  [3/3] Registry-Eintraege erstellen ...");
        try
        {
            CreateManifest(extensionId, existingOrigins);
            RegisterInRegistry(ChromeRegistryPath, ManifestPath);
            RegisterInRegistry(EdgeRegistryPath, ManifestPath);
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            WaitAndExit();
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  Installation abgeschlossen!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Naechste Schritte:");
        Console.WriteLine("  1. Starte Chrome / Edge neu");
        Console.WriteLine("  2. Oeffne die GyazoDumper Extension");
        Console.WriteLine("  3. Aktiviere 'Desktop-App verwenden'");
        Console.WriteLine("  4. Setze den gewuenschten Speicherpfad");
        Console.WriteLine();
        WaitAndExit();
    }

    /// <summary>
    /// Kommandozeilen-Installation (still, ohne interaktive Eingabe)
    /// Akzeptiert Extension-ID als Parameter: --install &lt;extension-id&gt;
    /// </summary>
    public static void Install(string? extensionId = null)
    {
        CopyToAppData();
        var existingOrigins = LoadExistingOrigins();
        CreateManifest(extensionId, existingOrigins);
        RegisterInRegistry(ChromeRegistryPath, ManifestPath);
        RegisterInRegistry(EdgeRegistryPath, ManifestPath);
    }

    /// <summary>
    /// Deinstalliert den Native Messaging Host vollstaendig
    /// </summary>
    public static void Uninstall()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ChromeRegistryPath, false);
            Console.WriteLine("  Chrome Registry-Eintrag entfernt");
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(EdgeRegistryPath, false);
            Console.WriteLine("  Edge Registry-Eintrag entfernt");
        }
        catch { }

        if (Directory.Exists(AppDataDir))
        {
            try
            {
                Directory.Delete(AppDataDir, true);
                Console.WriteLine($"  Ordner entfernt: {AppDataDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ordner konnte nicht vollstaendig entfernt werden: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Kopiert die aktuelle EXE nach %APPDATA%\GyazoDumper\
    /// </summary>
    private static void CopyToAppData()
    {
        Directory.CreateDirectory(AppDataDir);

        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte Anwendungspfad nicht ermitteln");

        // Nur kopieren wenn wir nicht bereits aus AppData laufen
        var normalizedCurrent = Path.GetFullPath(currentExe).ToLowerInvariant();
        var normalizedTarget = Path.GetFullPath(InstalledExePath).ToLowerInvariant();

        if (normalizedCurrent != normalizedTarget)
        {
            File.Copy(currentExe, InstalledExePath, overwrite: true);
        }
    }

    /// <summary>
    /// Laedt bereits registrierte allowed_origins aus einem bestehenden Manifest
    /// </summary>
    private static List<string> LoadExistingOrigins()
    {
        if (!File.Exists(ManifestPath))
            return new List<string>();

        try
        {
            var json = File.ReadAllText(ManifestPath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                PropertyNameCaseInsensitive = true
            };
            var existing = JsonSerializer.Deserialize<NativeMessagingManifest>(json, options);
            if (existing?.AllowedOrigins != null)
                return new List<string>(existing.AllowedOrigins);
        }
        catch { }

        return new List<string>();
    }

    /// <summary>
    /// Erstellt das Native Messaging Manifest inkl. allowed_origins
    /// </summary>
    private static void CreateManifest(string? extensionId, List<string> existingOrigins)
    {
        var allowedOrigins = new List<string>(existingOrigins);

        if (!string.IsNullOrEmpty(extensionId))
        {
            var origin = $"chrome-extension://{extensionId}/";
            if (!allowedOrigins.Contains(origin))
            {
                allowedOrigins.Add(origin);
            }
        }

        var manifest = new NativeMessagingManifest
        {
            Name = HostName,
            Description = "GyazoDumper Native Messaging Host - Speichert Gyazo-Bilder an beliebigem Ort",
            Path = InstalledExePath,
            Type = "stdio",
            AllowedOrigins = allowedOrigins.ToArray()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var json = JsonSerializer.Serialize(manifest, options);
        File.WriteAllText(ManifestPath, json);
    }

    /// <summary>
    /// Erstellt einen Registry-Eintrag fuer den Native Messaging Host
    /// </summary>
    private static void RegisterInRegistry(string registryPath, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        key?.SetValue("", manifestPath);
    }

    private static void WriteOk()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(" OK");
        Console.ResetColor();
    }

    private static void WriteFail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(" FEHLER");
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private static void WaitAndExit()
    {
        Console.WriteLine("  Druecke eine beliebige Taste zum Beenden...");
        Console.ReadKey(true);
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
