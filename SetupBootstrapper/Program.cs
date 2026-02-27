using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace GyazoDumper.Bootstrapper;

/// <summary>
/// GyazoDumper Setup - Bootstrapper (Ansatz C)
/// 
/// Professioneller Installer der die .NET Runtime bei Bedarf mitinstalliert.
/// 
/// Ablauf:
///   Phase 1 - Runtime:      Prueft ob .NET 10 installiert ist.
///                            Falls nicht: Download von Microsoft + stille Installation.
///   Phase 2 - Deployment:   Extrahiert die framework-dependent GyazoDumper.exe (~1 MB)
///                            nach %APPDATA%\GyazoDumper\
///   Phase 3 - Extension:    Extrahiert BrowserExtension-Dateien
///   Phase 4 - Konfiguration: Manifest, Registry, Config, Junction
/// 
/// Groessen-Vergleich:
///   - Bootstrapper (self-contained):  ~12 MB  (einmalig, kann danach geloescht werden)
///   - Installierte App:                ~1 MB  (framework-dependent, braucht .NET Runtime)
///   - .NET Runtime (falls noetig):    ~28 MB  (Download von Microsoft, einmalig)
/// 
/// Vorteile:
///   - Installierte App ist winzig (~1 MB statt 12.5 MB)
///   - .NET Runtime wird mit anderen Apps geteilt
///   - Updates der Runtime kommen automatisch via Windows Update
/// 
/// Nachteile:
///   - Erstinstallation braucht Internet (Runtime-Download ~28 MB)
///   - Runtime-Installation braucht Admin-Rechte
///   - Bootstrapper selbst ist ~12 MB (enthaelt eigene .NET Runtime)
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

    // .NET Runtime Download — aka.ms leitet immer auf die neueste Version um
    private const string RuntimeDownloadUrl = "https://aka.ms/dotnet/10.0/dotnet-runtime-win-x64.exe";
    private const string RequiredRuntimeVersion = "10.0.";

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

    public static async Task Main(string[] args)
    {
        if (args.Contains("--uninstall"))
        {
            Uninstall();
            return;
        }

        if (args.Contains("--silent"))
        {
            await SilentInstall();
            Console.WriteLine("GyazoDumper wurde erfolgreich installiert.");
            return;
        }

        await InteractiveInstall();
    }

    // ========================================================================
    //  Interaktive Installation
    // ========================================================================

    private static async Task InteractiveInstall()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║  GyazoDumper Setup (Bootstrapper)    ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Installationsordner: {AppDataDir}");
        Console.WriteLine();

        // ================================================================
        //  Phase 1: .NET Runtime pruefen / installieren
        // ================================================================
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 1: .NET Runtime");
        Console.ResetColor();

        Console.Write("  [1/9] .NET 10 Runtime pruefen ...            ");
        var runtimeInstalled = IsRuntimeInstalled();

        if (runtimeInstalled)
        {
            WriteOk();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("        .NET 10 ist bereits installiert.");
            Console.ResetColor();
        }
        else
        {
            WriteWarning("FEHLT");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("        .NET 10 Runtime wird benoetigt.");
            Console.WriteLine("        Download von Microsoft (~28 MB)");
            Console.ResetColor();
            Console.Write("        Installation starten? (j/n): ");

            if (Console.ReadLine()?.Trim().ToLower() != "j")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("        Installation abgebrochen.");
                Console.ResetColor();
                WaitAndExit();
                return;
            }

            Console.Write("  [2/9] .NET Runtime herunterladen ...          ");
            string? installerPath;
            try
            {
                installerPath = await DownloadRuntimeAsync();
                WriteOk();
            }
            catch (Exception ex)
            {
                WriteFail(ex.Message);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("        Manuelle Installation: https://dotnet.microsoft.com/download/dotnet/10.0");
                Console.ResetColor();
                WaitAndExit();
                return;
            }

            Console.Write("  [3/9] .NET Runtime installieren ...          ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();
            Console.WriteLine("        (Windows fragt nach Admin-Rechten)");
            Console.ResetColor();
            try
            {
                var exitCode = await InstallRuntimeAsync(installerPath);
                if (exitCode == 0)
                {
                    Console.Write("        ");
                    WriteOk();
                }
                else if (exitCode == 3010)
                {
                    // 3010 = Neustart erforderlich, aber Installation OK
                    Console.Write("        ");
                    WriteOk();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("        Hinweis: Neustart empfohlen nach Installation.");
                    Console.ResetColor();
                }
                else
                {
                    WriteFail($"Exit-Code: {exitCode}");
                    WaitAndExit();
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteFail(ex.Message);
                WaitAndExit();
                return;
            }
            finally
            {
                // Installer aufraeumen
                try { File.Delete(installerPath); } catch { }
            }

            // Nochmal pruefen
            if (!IsRuntimeInstalled())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("        .NET Runtime konnte nicht verifiziert werden.");
                Console.WriteLine("        Bitte starte das Setup nach einem Neustart erneut.");
                Console.ResetColor();
                WaitAndExit();
                return;
            }
        }

        Console.WriteLine();

        // ================================================================
        //  Phase 2: Anwendung installieren
        // ================================================================
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 2: Anwendung");
        Console.ResetColor();

        Console.Write("  [4/9] Anwendung installieren ...             ");
        try
        {
            Directory.CreateDirectory(AppDataDir);
            ExtractResource("Payload.GyazoDumper.exe", InstalledExePath);
            WriteOk();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var appSize = new FileInfo(InstalledExePath).Length;
            Console.WriteLine($"        {appSize / 1024} KB nach {InstalledExePath}");
            Console.ResetColor();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        Console.Write("  [5/9] Browser-Extension extrahieren ...      ");
        try
        {
            ExtractExtensionFiles(Path.Combine(AppDataDir, ExtensionFolderName));
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); }

        Console.WriteLine();

        // ================================================================
        //  Phase 3: Konfiguration
        // ================================================================
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  Phase 3: Konfiguration");
        Console.ResetColor();

        Console.Write("  [6/9] Bestehende Konfiguration laden ...     ");
        var existingOrigins = LoadExistingOrigins();
        WriteOk();

        Console.WriteLine("  [7/9] Extension registrieren");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"        Chrome Web Store ID: {WebStoreExtensionId}");
        Console.ResetColor();
        Console.Write("        Zusaetzliche ID (Enter = ueberspringen): ");
        var customId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customId))
            customId = customId.Replace("chrome-extension://", "").TrimEnd('/');

        Console.Write("        Manifest + Registry erstellen ...       ");
        try
        {
            CreateManifest(customId, existingOrigins);
            RegisterInRegistry(ChromeRegistryPath, ManifestPath);
            RegisterInRegistry(EdgeRegistryPath, ManifestPath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); WaitAndExit(); return; }

        Console.Write("  [8/9] Konfiguration erstellen ...            ");
        string savePath;
        try
        {
            savePath = MergeConfig();
            WriteOk();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"        Speicherort: {savePath}");
            Console.ResetColor();
        }
        catch (Exception ex) { WriteFail(ex.Message); savePath = DefaultSavePath; }

        Console.Write("  [9/9] Ordnerverknuepfung erstellen ...       ");
        try
        {
            Directory.CreateDirectory(savePath);
            CreateJunction(savePath);
            WriteOk();
        }
        catch (Exception ex) { WriteFail(ex.Message); }

        // ================================================================
        //  Abschluss
        // ================================================================
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ══════════════════════════════════════");
        Console.WriteLine("  Installation abgeschlossen!");
        Console.WriteLine("  ══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Dieser Installer kann jetzt geloescht werden.");
        Console.WriteLine($"  Installierte App: {InstalledExePath}");
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

    private static async Task SilentInstall()
    {
        if (!IsRuntimeInstalled())
        {
            var installer = await DownloadRuntimeAsync();
            await InstallRuntimeAsync(installer);
            try { File.Delete(installer); } catch { }
        }

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
    //  .NET Runtime Erkennung
    // ========================================================================

    /// <summary>
    /// Prueft ob die .NET 10 Runtime installiert ist.
    /// Verwendet 'dotnet --list-runtimes' und sucht nach Microsoft.NETCore.App 10.0.x
    /// </summary>
    private static bool IsRuntimeInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return false;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            // Suche nach: Microsoft.NETCore.App 10.0.x
            return output.Contains($"Microsoft.NETCore.App {RequiredRuntimeVersion}");
        }
        catch
        {
            // 'dotnet' Befehl nicht gefunden → .NET nicht installiert
            return false;
        }
    }

    // ========================================================================
    //  .NET Runtime Download + Installation
    // ========================================================================

    /// <summary>
    /// Laedt den .NET Runtime Installer von Microsoft herunter.
    /// Verwendet aka.ms Redirect der immer auf die neueste Version zeigt.
    /// Zeigt Fortschritt in der Konsole an.
    /// </summary>
    private static async Task<string> DownloadRuntimeAsync()
    {
        var installerPath = Path.Combine(Path.GetTempPath(), "dotnet-runtime-10-win-x64.exe");

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        using var response = await httpClient.GetAsync(RuntimeDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        using var contentStream = await response.Content.ReadAsStreamAsync();
        using var fileStream = File.Create(installerPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloaded += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)(downloaded * 100 / totalBytes);
                var mb = downloaded / (1024.0 * 1024.0);
                var totalMb = totalBytes / (1024.0 * 1024.0);
                Console.Write($"\r        Fortschritt: {percent}% ({mb:F1}/{totalMb:F1} MB)   ");
            }
        }

        Console.Write("\r        ");  // Fortschrittszeile ueberschreiben
        return installerPath;
    }

    /// <summary>
    /// Fuehrt den .NET Runtime Installer aus.
    /// Verwendet /install /quiet fuer stille Installation.
    /// Der Installer fragt selbst nach Admin-Rechten (UAC Dialog).
    /// </summary>
    private static async Task<int> InstallRuntimeAsync(string installerPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/install /quiet /norestart",
            UseShellExecute = true,  // Noetig fuer UAC-Elevation
            Verb = "runas"           // Als Administrator ausfuehren
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Runtime-Installer konnte nicht gestartet werden");

        await process.WaitForExitAsync();
        return process.ExitCode;
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
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Hinweis: Die .NET Runtime wurde NICHT deinstalliert");
        Console.WriteLine("  (wird moeglicherweise von anderen Apps benoetigt).");
        Console.ResetColor();
        WaitAndExit();
    }

    // ========================================================================
    //  Resource-Extraktion
    // ========================================================================

    private static void ExtractResource(string resourceName, string targetPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Resource '{resourceName}' nicht gefunden.");

        try
        {
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
        catch (IOException)
        {
            var backup = targetPath + ".old";
            try { File.Delete(backup); } catch { }
            File.Move(targetPath, backup);
            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }

    private static void ExtractExtensionFiles(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var assembly = Assembly.GetExecutingAssembly();

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("Extension.")) continue;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) continue;
            using var fs = File.Create(Path.Combine(targetDir, name["Extension.".Length..]));
            stream.CopyTo(fs);
        }
    }

    // ========================================================================
    //  Konfiguration
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
            { $"chrome-extension://{WebStoreExtensionId}/" };
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
        File.WriteAllText(ManifestPath,
            JsonSerializer.Serialize(manifest, BootstrapperJsonContext.Default.ManifestModel));
    }

    private static void RegisterInRegistry(string path, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        key?.SetValue("", manifestPath);
    }

    private static string MergeConfig()
    {
        string saveDir = DefaultSavePath, pattern = "Gyazo_{timestamp}_{hash}{ext}";
        if (File.Exists(ConfigPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
                var root = doc.RootElement;
                if (root.TryGetProperty("saveDirectory", out var sd) && sd.GetString() is string s && s.Length > 0) saveDir = s;
                if (root.TryGetProperty("fileNamePattern", out var fp) && fp.GetString() is string f && f.Length > 0) pattern = f;
            }
            catch { }
        }
        var config = new ConfigModel { SaveDirectory = saveDir, FileNamePattern = pattern };
        File.WriteAllText(ConfigPath,
            JsonSerializer.Serialize(config, BootstrapperJsonContext.Default.ConfigModel));
        return saveDir;
    }

    private static void CreateJunction(string targetPath)
    {
        var linkPath = Path.Combine(AppDataDir, "Gespeicherte Bilder");
        if (Directory.Exists(linkPath)) try { Directory.Delete(linkPath, false); } catch { }
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

    private static void WriteWarning(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(msg);
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
