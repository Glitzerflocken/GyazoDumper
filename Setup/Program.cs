using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GyazoDumper.Setup;

/// <summary>
/// GyazoDumper Setup - Getrennter Installer
/// 
/// Ansatz: Der Installer ist ein eigenstaendiges Programm das die
/// Anwendung (GyazoDumper.exe) installiert. Nach der Installation
/// wird der Installer nicht mehr benoetigt und kann geloescht werden.
/// 
/// Vorteile:
///   - Klare Trennung: Installer ≠ Anwendung
///   - Die installierte GyazoDumper.exe enthaelt keine Setup-Logik
///   - Professioneller Ansatz (wie bei den meisten Desktop-Anwendungen)
/// 
/// Nachteile:
///   - Zwei EXE-Dateien noetig (Setup + Anwendung)
///   - Setup muss die Anwendung als Resource einbetten → groessere Datei
///   - Build-Reihenfolge wichtig: erst Application publishen, dann Setup
/// 
/// Build-Reihenfolge:
///   1. cd Application &amp;&amp; dotnet publish -c Release
///   2. cd Setup &amp;&amp; dotnet publish -c Release
/// </summary>
public class Program
{
    // Konstanten
    private const string HostName = "gyazodumper.nativeapp";
    private const string AppFolderName = "GyazoDumper";
    private const string ExeFileName = "GyazoDumper.exe";
    private const string ExtensionFolderName = "BrowserExtension";
    private const string ChromeRegistryPath = @"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName;
    private const string EdgeRegistryPath = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName;
    private const string WebStoreExtensionId = "nlpifdgajdjkmenmmbpfekfefmaancnc";
    private const string WebStoreUrl = "https://chromewebstore.google.com/detail/gyazodumper/" + WebStoreExtensionId;

    // Pfade
    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);
    private static string InstalledExePath => Path.Combine(AppDataDir, ExeFileName);
    private static string ManifestPath => Path.Combine(AppDataDir, $"{HostName}.json");
    private static string ConfigPath => Path.Combine(AppDataDir, "config.json");
    private static string DefaultSavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "GyazoDumps");

    // ========================================================================
    //  Entry Point
    // ========================================================================

    public static void Main(string[] args)
    {
        if (args.Contains("--uninstall"))
        {
            Uninstall();
            return;
        }

        if (args.Contains("--silent"))
        {
            SilentInstall();
            Console.WriteLine("GyazoDumper wurde erfolgreich installiert.");
            return;
        }

        InteractiveInstall();
    }

    // ========================================================================
    //  Interaktive Installation
    // ========================================================================

    private static void InteractiveInstall()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║     GyazoDumper Setup (Installer)    ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Installationsordner: {AppDataDir}");
        Console.WriteLine();

        // Schritt 1: Ordner erstellen + Anwendung extrahieren
        Console.Write("  [1/7] Anwendung installieren ...             ");
        try
        {
            Directory.CreateDirectory(AppDataDir);
            ExtractResource("Payload.GyazoDumper.exe", InstalledExePath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        // Schritt 2: Browser-Extension extrahieren
        Console.Write("  [2/7] Browser-Extension extrahieren ...      ");
        try
        {
            ExtractExtensionFiles(Path.Combine(AppDataDir, ExtensionFolderName));
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); }

        // Schritt 3: Bestehende Konfiguration laden
        Console.Write("  [3/7] Bestehende Konfiguration laden ...     ");
        var existingOrigins = LoadExistingOrigins();
        WriteOk();
        if (existingOrigins.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        Bereits registriert: {existingOrigins.Count} Origin(s)");
            Console.ResetColor();
        }

        // Schritt 4: Extension-ID registrieren
        Console.WriteLine("  [4/7] Extension registrieren");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"        Chrome Web Store ID: {WebStoreExtensionId}");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("        Zusaetzliche ID registrieren? (z.B. fuer Entwicklung)");
        Console.ResetColor();
        Console.Write("        Eingabe (oder Enter zum Ueberspringen): ");
        var customId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customId))
            customId = customId.Replace("chrome-extension://", "").TrimEnd('/');

        Console.Write("        Manifest erstellen ...                  ");
        try
        {
            CreateManifest(customId, existingOrigins);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        // Schritt 5: Registry
        Console.Write("  [5/7] Registry-Eintraege erstellen ...       ");
        try
        {
            RegisterInRegistry(ChromeRegistryPath, ManifestPath);
            RegisterInRegistry(EdgeRegistryPath, ManifestPath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        // Schritt 6: Konfiguration
        Console.Write("  [6/7] Konfiguration erstellen ...            ");
        string savePath;
        try
        {
            savePath = MergeConfig();
            WriteOk();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"        Bilder werden gespeichert in: {savePath}");
            Console.ResetColor();
        }
        catch (Exception ex) { WriteFail(ex.Message); savePath = DefaultSavePath; }

        // Schritt 7: Junction
        Console.Write("  [7/7] Ordnerverknuepfung erstellen ...       ");
        try
        {
            Directory.CreateDirectory(savePath);
            CreateJunction(savePath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); }

        // Abschluss
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ══════════════════════════════════════");
        Console.WriteLine("  Installation abgeschlossen!");
        Console.WriteLine("  ══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Dieser Installer kann jetzt geloescht werden.");
        Console.WriteLine("  Die Anwendung wurde installiert nach:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {InstalledExePath}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Browser-Extension installieren:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  • Chrome Web Store: ");
        Console.ResetColor();
        Console.WriteLine(WebStoreUrl);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  • Oder manuell: ");
        Console.WriteLine(Path.Combine(AppDataDir, ExtensionFolderName));
        Console.ResetColor();
        Console.WriteLine();

        Console.Write("  Chrome Web Store im Browser oeffnen? (j/n): ");
        var openStore = Console.ReadLine()?.Trim().ToLower() == "j";
        Console.Write("  Installationsordner im Explorer oeffnen? (j/n): ");
        var openFolder = Console.ReadLine()?.Trim().ToLower() == "j";

        if (openStore)
            try { Process.Start(new ProcessStartInfo { FileName = WebStoreUrl, UseShellExecute = true }); } catch { }
        if (openFolder)
            try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = AppDataDir }); } catch { }

        WaitAndExit();
    }

    // ========================================================================
    //  Stille Installation
    // ========================================================================

    private static void SilentInstall()
    {
        Directory.CreateDirectory(AppDataDir);
        ExtractResource("Payload.GyazoDumper.exe", InstalledExePath);
        ExtractExtensionFiles(Path.Combine(AppDataDir, ExtensionFolderName));
        CreateManifest(null, LoadExistingOrigins());
        RegisterInRegistry(ChromeRegistryPath, ManifestPath);
        RegisterInRegistry(EdgeRegistryPath, ManifestPath);
        var savePath = MergeConfig();
        Directory.CreateDirectory(savePath);
        try { CreateJunction(savePath); } catch { }
    }

    // ========================================================================
    //  Deinstallation
    // ========================================================================

    private static void Uninstall()
    {
        Console.WriteLine();
        Console.WriteLine("  GyazoDumper wird deinstalliert...");
        Console.WriteLine();

        try { Registry.CurrentUser.DeleteSubKeyTree(ChromeRegistryPath, false); Console.WriteLine("  Chrome Registry entfernt"); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(EdgeRegistryPath, false); Console.WriteLine("  Edge Registry entfernt"); } catch { }

        if (Directory.Exists(AppDataDir))
        {
            try { Directory.Delete(AppDataDir, true); Console.WriteLine($"  Ordner entfernt: {AppDataDir}"); }
            catch (Exception ex) { Console.WriteLine($"  Fehler: {ex.Message}"); }
        }

        Console.WriteLine();
        Console.WriteLine("  Deinstallation abgeschlossen.");
        WaitAndExit();
    }

    // ========================================================================
    //  Resource-Extraktion
    // ========================================================================

    /// <summary>
    /// Extrahiert eine einzelne eingebettete Resource in eine Datei.
    /// Ueberschreibt bestehende Dateien.
    /// </summary>
    private static void ExtractResource(string resourceName, string targetPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Resource '{resourceName}' nicht gefunden. Wurde die Application vorher gepublisht?");

        // Falls Zieldatei gesperrt ist (Chrome Native Host laeuft)
        try
        {
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
        catch (IOException)
        {
            var backupPath = targetPath + ".old";
            try { File.Delete(backupPath); } catch { }
            File.Move(targetPath, backupPath);
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }

    /// <summary>
    /// Extrahiert alle Browser-Extension Dateien in den Zielordner.
    /// </summary>
    private static void ExtractExtensionFiles(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var assembly = Assembly.GetExecutingAssembly();
        const string prefix = "Extension.";

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(prefix)) continue;
            var fileName = name[prefix.Length..];
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) continue;
            using var fileStream = File.Create(Path.Combine(targetDir, fileName));
            stream.CopyTo(fileStream);
        }
    }

    // ========================================================================
    //  Manifest, Registry, Config, Junction
    // ========================================================================

    private static List<string> LoadExistingOrigins()
    {
        if (!File.Exists(ManifestPath)) return [];
        try
        {
            var json = File.ReadAllText(ManifestPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("allowed_origins", out var origins))
                return origins.EnumerateArray().Select(e => e.GetString()!).ToList();
        }
        catch { }
        return [];
    }

    private static void CreateManifest(string? additionalId, List<string> existingOrigins)
    {
        var origins = new HashSet<string>(existingOrigins)
        {
            $"chrome-extension://{WebStoreExtensionId}/"
        };
        if (!string.IsNullOrEmpty(additionalId))
            origins.Add($"chrome-extension://{additionalId}/");

        var manifest = new ManifestModel
        {
            Name = HostName,
            Description = "GyazoDumper Native Messaging Host",
            Path = InstalledExePath,
            Type = "stdio",
            AllowedOrigins = origins.ToArray()
        };

        var json = JsonSerializer.Serialize(manifest, SetupJsonContext.Default.ManifestModel);
        File.WriteAllText(ManifestPath, json);
    }

    private static void RegisterInRegistry(string path, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key?.SetValue("", manifestPath);
    }

    private static string MergeConfig()
    {
        string saveDir = DefaultSavePath;
        string pattern = "Gyazo_{timestamp}_{hash}{ext}";

        if (File.Exists(ConfigPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("saveDirectory", out var sd) && sd.GetString() is string s && s.Length > 0)
                    saveDir = s;
                if (root.TryGetProperty("fileNamePattern", out var fp) && fp.GetString() is string f && f.Length > 0)
                    pattern = f;
            }
            catch { }
        }

        var config = new ConfigModel { SaveDirectory = saveDir, FileNamePattern = pattern };
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, SetupJsonContext.Default.ConfigModel));
        return saveDir;
    }

    private static void CreateJunction(string targetPath)
    {
        var linkPath = Path.Combine(AppDataDir, "Gespeicherte Bilder");
        if (Directory.Exists(linkPath))
            try { Directory.Delete(linkPath, false); } catch { }
        Directory.CreateDirectory(targetPath);
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        Process.Start(psi)?.WaitForExit(5000);
    }

    // ========================================================================
    //  UI-Hilfen
    // ========================================================================

    private static void WriteOk()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("OK");
        Console.ResetColor();
    }

    private static void WriteFail(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FEHLER");
        Console.WriteLine($"        {msg}");
        Console.ResetColor();
    }

    private static void WaitAndExit()
    {
        Console.WriteLine("  Druecke eine beliebige Taste zum Beenden...");
        Console.ReadKey(true);
    }
}
