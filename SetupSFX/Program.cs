using Microsoft.Win32;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace GyazoDumper.SetupSFX;

/// <summary>
/// GyazoDumper Setup - Self-Extracting Archive (SFX)
/// 
/// Ansatz: Alle Installationsdateien sind in einem ZIP-Archiv komprimiert
/// und als Resource in diese EXE eingebettet. Bei Ausfuehrung wird das
/// Archiv in einen temporaeren Ordner entpackt, dann nach AppData kopiert
/// und konfiguriert.
/// 
/// Ablauf:
///   Phase 1 - Extraktion:  ZIP entpacken nach %TEMP%\GyazoDumper-Setup\
///   Phase 2 - Deployment:  Dateien nach %APPDATA%\GyazoDumper\ kopieren
///   Phase 3 - Konfiguration: Manifest, Registry, Config, Junction
///   Phase 4 - Cleanup:     Temporaeren Ordner loeschen
/// 
/// Vorteile:
///   - Alle Dateien komprimiert → kleinere Setup-Datei
///   - Traditioneller Installer-Ablauf (Entpacken → Installieren)
///   - Integritaetspruefung moeglich (ZIP-Checksumme)
///   - Einfach erweiterbar (weitere Dateien ins ZIP packen)
/// 
/// Nachteile:
///   - ZIP muss zur Build-Zeit erstellt werden (MSBuild Target)
///   - Braucht temporaeren Speicher waehrend der Installation
///   - Etwas komplexerer Build-Prozess
/// 
/// Build-Reihenfolge:
///   1. cd Application &amp;&amp; dotnet publish -c Release
///   2. cd SetupSFX &amp;&amp; dotnet publish -c Release
///      (erstellt payload.zip automatisch via MSBuild Target)
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
    private static string TempDir => Path.Combine(Path.GetTempPath(), "GyazoDumper-Setup");

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
        Console.WriteLine("║  GyazoDumper Setup (Self-Extracting) ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Installationsordner: {AppDataDir}");
        Console.WriteLine();

        // ---- Phase 1: Extraktion ----
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 1: Archiv entpacken");
        Console.ResetColor();

        Console.Write("  [1/8] Archiv extrahieren ...                 ");
        int fileCount;
        try
        {
            fileCount = ExtractArchive();
            WriteOk();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        {fileCount} Dateien nach {TempDir}");
            Console.ResetColor();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        // ---- Phase 2: Deployment ----
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 2: Dateien installieren");
        Console.ResetColor();

        Console.Write("  [2/8] Anwendung kopieren ...                 ");
        try
        {
            Directory.CreateDirectory(AppDataDir);
            DeployFile(ExeFileName);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        Console.Write("  [3/8] Browser-Extension kopieren ...         ");
        try
        {
            DeployExtensionFiles();
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); }

        // ---- Phase 3: Konfiguration ----
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 3: Konfiguration");
        Console.ResetColor();

        Console.Write("  [4/8] Bestehende Konfiguration laden ...     ");
        var existingOrigins = LoadExistingOrigins();
        WriteOk();

        Console.WriteLine("  [5/8] Extension registrieren");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"        Chrome Web Store ID: {WebStoreExtensionId}");
        Console.ResetColor();
        Console.Write("        Zusaetzliche ID (Enter = ueberspringen): ");
        var customId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customId))
            customId = customId.Replace("chrome-extension://", "").TrimEnd('/');

        Console.Write("        Manifest erstellen ...                  ");
        try { CreateManifest(customId, existingOrigins); WriteOk(); }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        Console.Write("  [6/8] Registry-Eintraege erstellen ...       ");
        try
        {
            RegisterInRegistry(ChromeRegistryPath, ManifestPath);
            RegisterInRegistry(EdgeRegistryPath, ManifestPath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        Console.Write("  [7/8] Konfiguration erstellen ...            ");
        string savePath;
        try
        {
            savePath = MergeConfig();
            WriteOk();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"        Speicherort: {savePath}");
            Console.ResetColor();
            Directory.CreateDirectory(savePath);
            CreateJunction(savePath);
        }
        catch (Exception ex) { WriteFail(ex.Message); savePath = DefaultSavePath; }

        // ---- Phase 4: Cleanup ----
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 4: Aufraeumen");
        Console.ResetColor();

        Console.Write("  [8/8] Temporaere Dateien loeschen ...        ");
        try { Directory.Delete(TempDir, true); WriteOk(); }
        catch { WriteOk(); /* Nicht kritisch */ }

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
        ExtractArchive();
        Directory.CreateDirectory(AppDataDir);
        DeployFile(ExeFileName);
        DeployExtensionFiles();
        CreateManifest(null, LoadExistingOrigins());
        RegisterInRegistry(ChromeRegistryPath, ManifestPath);
        RegisterInRegistry(EdgeRegistryPath, ManifestPath);
        var savePath = MergeConfig();
        Directory.CreateDirectory(savePath);
        try { CreateJunction(savePath); } catch { }
        try { Directory.Delete(TempDir, true); } catch { }
    }

    // ========================================================================
    //  Deinstallation
    // ========================================================================

    private static void Uninstall()
    {
        Console.WriteLine("\n  GyazoDumper wird deinstalliert...\n");
        try { Registry.CurrentUser.DeleteSubKeyTree(ChromeRegistryPath, false); Console.WriteLine("  Chrome Registry entfernt"); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(EdgeRegistryPath, false); Console.WriteLine("  Edge Registry entfernt"); } catch { }
        if (Directory.Exists(AppDataDir))
        {
            try { Directory.Delete(AppDataDir, true); Console.WriteLine($"  Ordner entfernt: {AppDataDir}"); }
            catch (Exception ex) { Console.WriteLine($"  Fehler: {ex.Message}"); }
        }
        Console.WriteLine("\n  Deinstallation abgeschlossen.");
        WaitAndExit();
    }

    // ========================================================================
    //  Phase 1: Archiv-Extraktion
    // ========================================================================

    /// <summary>
    /// Entpackt das eingebettete ZIP-Archiv in den temporaeren Ordner.
    /// Gibt die Anzahl der extrahierten Dateien zurueck.
    /// </summary>
    private static int ExtractArchive()
    {
        if (Directory.Exists(TempDir))
            Directory.Delete(TempDir, true);
        Directory.CreateDirectory(TempDir);

        var assembly = Assembly.GetExecutingAssembly();
        using var zipStream = assembly.GetManifestResourceStream("Payload.zip")
            ?? throw new FileNotFoundException("Payload.zip nicht im Archiv gefunden. Wurde die Application vorher gepublisht?");

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        int count = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories
            var targetPath = Path.Combine(TempDir, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: true);
            count++;
        }
        return count;
    }

    // ========================================================================
    //  Phase 2: Deployment (Temp → AppData)
    // ========================================================================

    /// <summary>
    /// Kopiert eine Datei vom Temp-Ordner nach AppData.
    /// Behandelt gesperrte Dateien (Chrome Native Host laeuft).
    /// </summary>
    private static void DeployFile(string fileName)
    {
        var source = Path.Combine(TempDir, fileName);
        var target = Path.Combine(AppDataDir, fileName);

        try
        {
            File.Copy(source, target, overwrite: true);
        }
        catch (IOException)
        {
            var backup = target + ".old";
            try { File.Delete(backup); } catch { }
            File.Move(target, backup);
            File.Copy(source, target);
        }
    }

    /// <summary>
    /// Kopiert alle Extension-Dateien vom Temp-Ordner nach AppData.
    /// </summary>
    private static void DeployExtensionFiles()
    {
        var sourceDir = Path.Combine(TempDir, ExtensionFolderName);
        var targetDir = Path.Combine(AppDataDir, ExtensionFolderName);
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);
        }
    }

    // ========================================================================
    //  Phase 3: Konfiguration
    // ========================================================================

    private static List<string> LoadExistingOrigins()
    {
        if (!File.Exists(ManifestPath)) return [];
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(ManifestPath));
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
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, SfxJsonContext.Default.ManifestModel));
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
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, SfxJsonContext.Default.ConfigModel));
        return saveDir;
    }

    private static void CreateJunction(string targetPath)
    {
        var linkPath = Path.Combine(AppDataDir, "Gespeicherte Bilder");
        if (Directory.Exists(linkPath))
            try { Directory.Delete(linkPath, false); } catch { }
        Directory.CreateDirectory(targetPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        })?.WaitForExit(5000);
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
