using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Installiert/Deinstalliert den GyazoDumper Native Messaging Host.
/// 
/// Installationsablauf:
///   1. Ordner erstellen + EXE kopieren
///   2. Browser-Extension Dateien extrahieren
///   3. Bestehende Manifest-Origins einlesen
///   4. Extension-ID registrieren (Web Store ID + optional weitere)
///   5. Registry-Eintraege erstellen
///   6. Konfiguration erstellen/mergen
///   7. Ordnerverknuepfung erstellen
/// 
/// Deinstallation:
///   1. Registry-Eintraege entfernen
///   2. Gesamten %APPDATA%\GyazoDumper\ Ordner loeschen
/// </summary>
public static class NativeHostInstaller
{
    private const string HostName = "gyazodumper.nativeapp";
    private const string AppFolderName = "GyazoDumper";
    private const string ExeFileName = "GyazoDumper.exe";
    private const string ExtensionFolderName = "BrowserExtension";
    private const string ChromeRegistryPath = @"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName;
    private const string EdgeRegistryPath = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName;

    // Chrome Web Store Extension-ID (fest seit Veroeffentlichung)
    private const string WebStoreExtensionId = "nlpifdgajdjkmenmmbpfekfefmaancnc";
    private const string WebStoreUrl = "https://chromewebstore.google.com/detail/gyazodumper/" + WebStoreExtensionId;

    private static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName);

    private static string InstalledExePath => Path.Combine(AppDataDir, ExeFileName);
    private static string ManifestPath => Path.Combine(AppDataDir, $"{HostName}.json");
    private static string ConfigPath => Path.Combine(AppDataDir, "config.json");

    private static string DefaultSavePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "GyazoDumps");

    // ========================================================================
    //  Interaktiver Setup-Assistent
    // ========================================================================

    /// <summary>
    /// Interaktiver Setup-Assistent - wird bei Doppelklick auf die EXE ausgefuehrt.
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

        // --- Schritt 1: Ordner erstellen + EXE kopieren ---
        Console.Write("  [1/7] Anwendung installieren ...             ");
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

        // --- Schritt 2: Browser-Extension Dateien extrahieren ---
        Console.Write("  [2/7] Browser-Extension extrahieren ...      ");
        try
        {
            ExtractBrowserExtension(Path.Combine(AppDataDir, ExtensionFolderName));
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            // Nicht abbrechen — Extension ist optional
        }

        // --- Schritt 3: Bestehende Manifest-Origins einlesen ---
        Console.Write("  [3/7] Bestehende Konfiguration laden ...     ");
        var existingOrigins = LoadExistingOrigins();
        if (existingOrigins.Count > 0)
        {
            WriteOk();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        Bereits registriert: {existingOrigins.Count} Origin(s)");
            Console.ResetColor();
        }
        else
        {
            WriteOk();
        }

        // --- Schritt 4: Extension-ID registrieren ---
        Console.WriteLine("  [4/7] Extension registrieren");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"        Chrome Web Store ID ist bereits hinterlegt:");
        Console.WriteLine($"        {WebStoreExtensionId}");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("        Zusaetzliche ID registrieren? (z.B. fuer Entwicklung)");
        Console.ResetColor();
        Console.Write("        Eingabe (oder Enter zum Ueberspringen): ");

        var customId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customId))
        {
            customId = customId.Replace("chrome-extension://", "").TrimEnd('/');
        }

        Console.Write("        Manifest erstellen ...                  ");
        try
        {
            CreateManifest(customId, existingOrigins);
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            WaitAndExit();
            return;
        }

        // --- Schritt 5: Registry-Eintraege ---
        Console.Write("  [5/7] Registry-Eintraege erstellen ...       ");
        try
        {
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

        // --- Schritt 6: Konfiguration erstellen/mergen ---
        Console.Write("  [6/7] Konfiguration erstellen ...            ");
        string savePath;
        try
        {
            savePath = MergeConfig();
            WriteOk();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"        Bilder werden gespeichert in:");
            Console.WriteLine($"        {savePath}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("        (Kann spaeter in der Browser-Extension geaendert werden)");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            savePath = DefaultSavePath;
        }

        // --- Schritt 7: Ordnerverknuepfung ---
        Console.Write("  [7/7] Ordnerverknuepfung erstellen ...       ");
        try
        {
            Directory.CreateDirectory(savePath);
            UpdateFolderShortcut(savePath);
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
        }

        // --- Abschluss ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ══════════════════════════════════════");
        Console.WriteLine("  Installation abgeschlossen!");
        Console.WriteLine("  ══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Browser-Extension installieren:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  • Chrome Web Store: ");
        Console.ResetColor();
        Console.WriteLine(WebStoreUrl);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  • Oder manuell ueber den Entwicklermodus:");
        Console.WriteLine("    1. Oeffne chrome://extensions/ (oder edge://extensions/)");
        Console.WriteLine("    2. Aktiviere den Entwicklermodus (Schalter oben rechts)");
        Console.WriteLine("    3. Klicke 'Entpackte Erweiterung laden'");
        Console.WriteLine($"    4. Waehle diesen Ordner: {Path.Combine(AppDataDir, ExtensionFolderName)}");
        Console.ResetColor();
        Console.WriteLine();

        // Optionale Aktionen - beide abfragen, dann ausfuehren
        Console.Write("  Chrome Web Store im Browser oeffnen? (j/n): ");
        var openStore = Console.ReadLine()?.Trim().ToLower() == "j";

        Console.Write("  Installationsordner im Explorer oeffnen? (j/n): ");
        var openFolder = Console.ReadLine()?.Trim().ToLower() == "j";

        Console.WriteLine();

        if (openStore)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = WebStoreUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        if (openFolder)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = AppDataDir
                });
            }
            catch { }
        }

        WaitAndExit();
    }

    // ========================================================================
    //  Stille Installation (Kommandozeile)
    // ========================================================================

    /// <summary>
    /// Kommandozeilen-Installation (still, ohne interaktive Eingabe).
    /// Web Store ID wird automatisch registriert.
    /// Optional: zusaetzliche Extension-ID als Parameter.
    /// </summary>
    public static void Install(string? additionalExtensionId = null)
    {
        CopyToAppData();
        ExtractBrowserExtension(Path.Combine(AppDataDir, ExtensionFolderName));
        var existingOrigins = LoadExistingOrigins();
        CreateManifest(additionalExtensionId, existingOrigins);
        RegisterInRegistry(ChromeRegistryPath, ManifestPath);
        RegisterInRegistry(EdgeRegistryPath, ManifestPath);
        MergeConfig();
        try
        {
            var config = LoadConfig();
            Directory.CreateDirectory(config.SaveDirectory);
            UpdateFolderShortcut(config.SaveDirectory);
        }
        catch { }
    }

    // ========================================================================
    //  Deinstallation
    // ========================================================================

    /// <summary>
    /// Deinstalliert den Native Messaging Host vollstaendig.
    /// 
    /// Reihenfolge:
    ///   1. Registry-Eintraege entfernen (sofort, keine Abhaengigkeiten)
    ///   2. cmd.exe Nachzuegler starten (wartet 3s, loescht dann alle Dateien)
    ///   3. Laufende Native Host Prozesse beenden (letzter Schritt vor Exit)
    /// 
    /// Nach dem Exit sind alle Dateien entsperrt und der Nachzuegler
    /// loescht den gesamten Ordnerinhalt. Der leere Ordner bleibt bestehen.
    /// </summary>
    public static void Uninstall()
    {
        // 1. Registry-Eintraege entfernen
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

        // 2. cmd.exe Nachzuegler starten — wartet 3 Sekunden, dann loescht
        //    alle Dateien und Unterordner. Der Ordner selbst bleibt leer bestehen.
        if (Directory.Exists(AppDataDir))
        {
            try
            {
                var cleanupCmd = $"timeout /t 3 /nobreak >nul " +
                    $"& del /f /q \"{AppDataDir}\\*\" 2>nul " +
                    $"& for /d %d in (\"{AppDataDir}\\*\") do @rmdir /s /q \"%d\" 2>nul";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {cleanupCmd}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                Console.WriteLine("  Bereinigung geplant (in 3 Sekunden)");
            }
            catch { }
        }

        // 3. Laufende Native Host Prozesse beenden (letzter Schritt)
        //    Gibt Datei-Locks frei damit der Nachzuegler alles loeschen kann.
        var currentPid = Environment.ProcessId;
        foreach (var proc in Process.GetProcessesByName("GyazoDumper"))
        {
            if (proc.Id == currentPid) continue;
            try
            {
                proc.Kill();
                proc.WaitForExit(3000);
                Console.WriteLine($"  Native Host Prozess beendet (PID {proc.Id})");
            }
            catch { }
        }
    }

    // ========================================================================
    //  Dateien kopieren / extrahieren
    // ========================================================================

    /// <summary>
    /// Kopiert die aktuelle EXE nach %APPDATA%\GyazoDumper\.
    /// Falls die Zieldatei gesperrt ist (Chrome nutzt den Native Host),
    /// wird die alte Datei umbenannt und dann die neue kopiert.
    /// Erstellt ausserdem eine Uninstall.bat im Installationsordner.
    /// </summary>
    private static void CopyToAppData()
    {
        Directory.CreateDirectory(AppDataDir);

        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Konnte Anwendungspfad nicht ermitteln");

        var normalizedCurrent = Path.GetFullPath(currentExe).ToLowerInvariant();
        var normalizedTarget = Path.GetFullPath(InstalledExePath).ToLowerInvariant();
        if (normalizedCurrent == normalizedTarget) return;

        try
        {
            File.Copy(currentExe, InstalledExePath, overwrite: true);
        }
        catch (IOException)
        {
            var backupPath = InstalledExePath + ".old";
            try { File.Delete(backupPath); } catch { }
            File.Move(InstalledExePath, backupPath);
            File.Copy(currentExe, InstalledExePath);
        }

        CreateUninstallBat();
    }

    /// <summary>
    /// Erstellt eine Uninstall.bat im Installationsordner.
    /// Die BAT ruft die EXE mit --uninstall auf, wartet auf das Ende,
    /// loescht den Ordner und sich selbst.
    /// </summary>
    private static void CreateUninstallBat()
    {
        var batPath = Path.Combine(AppDataDir, "Uninstall.bat");
        var batContent = $"""
            @echo off
            echo.
            echo   GyazoDumper Deinstallation
            echo   ══════════════════════════
            echo.
            "{InstalledExePath}" --uninstall
            echo.
            echo   Druecke eine beliebige Taste zum Beenden...
            pause >nul
            """;
        File.WriteAllText(batPath, batContent);
    }

    /// <summary>
    /// Extrahiert die eingebetteten Browser-Extension-Dateien.
    /// Ueberschreibt bestehende Dateien um Updates zu ermoeglichen.
    /// </summary>
    private static void ExtractBrowserExtension(string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        var assembly = Assembly.GetExecutingAssembly();
        var prefix = "BrowserExtension.";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix)) continue;

            var fileName = resourceName[prefix.Length..];
            var targetPath = Path.Combine(targetDir, fileName);

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue;

            using var fileStream = File.Create(targetPath);
            stream.CopyTo(fileStream);
        }
    }

    // ========================================================================
    //  Manifest (Native Messaging)
    // ========================================================================

    /// <summary>
    /// Laedt bestehende allowed_origins aus einem vorhandenen Manifest
    /// </summary>
    private static List<string> LoadExistingOrigins()
    {
        if (!File.Exists(ManifestPath))
            return new List<string>();

        try
        {
            var json = File.ReadAllText(ManifestPath);
            var existing = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.NativeMessagingManifest);
            if (existing?.AllowedOrigins != null)
                return new List<string>(existing.AllowedOrigins);
        }
        catch { }

        return new List<string>();
    }

    /// <summary>
    /// Erstellt das Native Messaging Manifest.
    /// Die Web Store Extension-ID wird immer automatisch hinzugefuegt.
    /// Optional kann eine zusaetzliche ID (z.B. fuer Entwicklung) angegeben werden.
    /// Bestehende Origins werden beibehalten.
    /// </summary>
    private static void CreateManifest(string? additionalExtensionId, List<string> existingOrigins)
    {
        var allowedOrigins = new HashSet<string>(existingOrigins);

        // Web Store ID immer hinzufuegen
        allowedOrigins.Add($"chrome-extension://{WebStoreExtensionId}/");

        // Zusaetzliche ID hinzufuegen falls angegeben
        if (!string.IsNullOrEmpty(additionalExtensionId))
        {
            allowedOrigins.Add($"chrome-extension://{additionalExtensionId}/");
        }

        var manifest = new NativeMessagingManifest
        {
            Name = HostName,
            Description = "GyazoDumper Native Messaging Host - Speichert Gyazo-Bilder an beliebigem Ort",
            Path = InstalledExePath,
            Type = "stdio",
            AllowedOrigins = allowedOrigins.ToArray()
        };

        var json = JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.NativeMessagingManifest);
        File.WriteAllText(ManifestPath, json);
    }

    // ========================================================================
    //  Registry
    // ========================================================================

    private static void RegisterInRegistry(string registryPath, string manifestPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(registryPath);
        key?.SetValue("", manifestPath);
    }

    // ========================================================================
    //  Konfiguration (config.json merge)
    // ========================================================================

    /// <summary>
    /// Laedt eine bestehende config.json oder erstellt Standardwerte.
    /// </summary>
    private static AppConfig LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize(json, GyazoDumperJsonContext.Default.AppConfig);
                if (config != null) return config;
            }
            catch { }
        }
        return new AppConfig();
    }

    /// <summary>
    /// Merged bestehende config.json mit Standardwerten.
    /// Vorhandene Werte bleiben erhalten, fehlende/leere Keys werden aufgefuellt.
    /// Gibt den aktiven Speicherpfad zurueck.
    /// </summary>
    private static string MergeConfig()
    {
        var config = LoadConfig();

        // Leere Werte mit Defaults fuellen
        if (string.IsNullOrEmpty(config.SaveDirectory))
            config.SaveDirectory = DefaultSavePath;

        if (string.IsNullOrEmpty(config.FileNamePattern))
            config.FileNamePattern = "Gyazo_{timestamp}_{hash}{ext}";

        // Immer schreiben (stellt sicher dass neue Keys vorhanden sind)
        var json = JsonSerializer.Serialize(config, GyazoDumperJsonContext.Default.AppConfig);
        File.WriteAllText(ConfigPath, json);

        return config.SaveDirectory;
    }

    // ========================================================================
    //  Ordnerverknuepfung (Junction)
    // ========================================================================

    /// <summary>
    /// Erstellt oder aktualisiert die Ordnerverknuepfung (Junction) im AppData-Ordner.
    /// Wird bei Installation und bei jedem Ordnerwechsel aufgerufen.
    /// </summary>
    public static void UpdateFolderShortcut(string targetPath)
    {
        var linkPath = Path.Combine(AppDataDir, "Gespeicherte Bilder");

        if (Directory.Exists(linkPath))
        {
            try { Directory.Delete(linkPath, false); } catch { }
        }

        try { Directory.CreateDirectory(targetPath); } catch { }

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var process = Process.Start(psi);
        process?.WaitForExit(5000);
    }

    // ========================================================================
    //  Konsolen-Hilfsfunktionen
    // ========================================================================

    private static void WriteOk()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("OK");
        Console.ResetColor();
    }

    private static void WriteFail(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FEHLER");
        Console.WriteLine($"        {message}");
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
