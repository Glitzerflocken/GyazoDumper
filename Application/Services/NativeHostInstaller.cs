using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Installs/Uninstalls the GyazoDumper Native Messaging Host.
/// 
/// Installation steps:
///   1. Create folder + copy EXE
///   2. Extract browser extension files
///   3. Load existing manifest origins
///   4. Register extension ID (Web Store ID + optional additional)
///   5. Create registry entries
///   6. Create/merge configuration
///   7. Create folder shortcut
/// 
/// Uninstall:
///   1. Remove registry entries
///   2. Delete entire %APPDATA%\GyazoDumper\ folder contents
/// </summary>
public static class NativeHostInstaller
{
    private const string HostName = "gyazodumper.nativeapp";
    private const string AppFolderName = "GyazoDumper";
    private const string ExeFileName = "GyazoDumper.exe";
    private const string ExtensionFolderName = "BrowserExtension";
    private const string ChromeRegistryPath = @"SOFTWARE\Google\Chrome\NativeMessagingHosts\" + HostName;
    private const string EdgeRegistryPath = @"SOFTWARE\Microsoft\Edge\NativeMessagingHosts\" + HostName;

    // Chrome Web Store Extension-ID (fixed since publication)
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
    //  Interactive Setup Wizard
    // ========================================================================

    /// <summary>
    /// Interactive setup wizard — runs when the EXE is double-clicked.
    /// </summary>
    public static void InteractiveInstall()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║       GyazoDumper Setup              ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"  Install folder: {AppDataDir}");
        Console.WriteLine();

        // --- Step 1: Create folder + copy EXE ---
        Console.Write("  [1/7] Installing application ...              ");
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

        // --- Step 2: Extract browser extension files ---
        Console.Write("  [2/7] Extracting browser extension ...        ");
        try
        {
            ExtractBrowserExtension(Path.Combine(AppDataDir, ExtensionFolderName));
            WriteOk();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            // Don't abort — extension is optional
        }

        // --- Step 3: Load existing manifest origins ---
        Console.Write("  [3/7] Loading existing configuration ...      ");
        var existingOrigins = LoadExistingOrigins();
        if (existingOrigins.Count > 0)
        {
            WriteOk();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"        Already registered: {existingOrigins.Count} origin(s)");
            Console.ResetColor();
        }
        else
        {
            WriteOk();
        }

        // --- Step 4: Register extension ID ---
        Console.WriteLine("  [4/7] Registering extension");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"        Chrome Web Store ID is already registered:");
        Console.WriteLine($"        {WebStoreExtensionId}");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("        Register additional ID? (e.g. for development)");
        Console.ResetColor();
        Console.Write("        Input (or press Enter to skip): ");

        var customId = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(customId))
        {
            customId = customId.Replace("chrome-extension://", "").TrimEnd('/');
        }

        Console.Write("        Creating manifest ...                   ");
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

        // --- Step 5: Registry entries ---
        Console.Write("  [5/7] Creating registry entries ...           ");
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

        // --- Step 6: Create/merge configuration ---
        Console.Write("  [6/7] Creating configuration ...              ");
        string savePath;
        try
        {
            savePath = MergeConfig();
            WriteOk();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"        Images will be saved to:");
            Console.WriteLine($"        {savePath}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("        (Can be changed later in the browser extension)");
            Console.ResetColor();
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            WriteFail(ex.Message);
            savePath = DefaultSavePath;
        }

        // --- Step 7: Folder shortcut ---
        Console.Write("  [7/7] Creating folder shortcut ...            ");
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

        // --- Completion ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ══════════════════════════════════════");
        Console.WriteLine("  Installation complete!");
        Console.WriteLine("  ══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Install the browser extension:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("  • Chrome Web Store: ");
        Console.ResetColor();
        Console.WriteLine(WebStoreUrl);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  • Or manually via developer mode:");
        Console.WriteLine("    1. Open chrome://extensions/ (or edge://extensions/)");
        Console.WriteLine("    2. Enable Developer mode (toggle in the top right)");
        Console.WriteLine("    3. Click 'Load unpacked'");
        Console.WriteLine($"    4. Select this folder: {Path.Combine(AppDataDir, ExtensionFolderName)}");
        Console.ResetColor();
        Console.WriteLine();

        // Optional actions — ask both, then execute
        Console.Write("  Open Chrome Web Store in browser? (y/n): ");
        var openStore = Console.ReadLine()?.Trim().ToLower() == "y";

        Console.Write("  Open install folder in Explorer? (y/n): ");
        var openFolder = Console.ReadLine()?.Trim().ToLower() == "y";

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
    //  Silent Installation (Command Line)
    // ========================================================================

    /// <summary>
    /// Command-line installation (silent, no interactive input).
    /// Web Store ID is registered automatically.
    /// Optional: additional extension ID as parameter.
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
    //  Uninstall
    // ========================================================================

    /// <summary>
    /// Fully uninstalls the Native Messaging Host.
    /// 
    /// Order:
    ///   1. Remove registry entries (immediate, no dependencies)
    ///   2. Start cmd.exe cleanup process (waits 3s, then deletes all files)
    ///   3. Kill running Native Host processes (last step before exit)
    /// 
    /// After exit all files are unlocked and the cleanup process
    /// deletes the entire folder contents. The empty folder remains.
    /// </summary>
    public static void Uninstall()
    {
        // 1. Remove registry entries
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(ChromeRegistryPath, false);
            Console.WriteLine("  Chrome registry entry removed");
        }
        catch { }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(EdgeRegistryPath, false);
            Console.WriteLine("  Edge registry entry removed");
        }
        catch { }

        // 2. Start cmd.exe cleanup process — waits 3 seconds, then deletes
        //    all files and subdirectories. The folder itself remains empty.
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
                Console.WriteLine("  Cleanup scheduled (in 3 seconds)");
            }
            catch { }
        }

        // 3. Kill running Native Host processes (last step)
        //    Releases file locks so the cleanup process can delete everything.
        var currentPid = Environment.ProcessId;
        foreach (var proc in Process.GetProcessesByName("GyazoDumper"))
        {
            if (proc.Id == currentPid) continue;
            try
            {
                proc.Kill();
                proc.WaitForExit(2900);
                Console.WriteLine($"  Native Host process terminated (PID {proc.Id})");
            }
            catch { }
        }
    }

    // ========================================================================
    //  Copy / Extract Files
    // ========================================================================

    /// <summary>
    /// Copies the current EXE to %APPDATA%\GyazoDumper\.
    /// If the target file is locked (Chrome is using the Native Host),
    /// the old file is renamed and the new one is copied.
    /// Also creates an Uninstall.bat in the install folder.
    /// </summary>
    private static void CopyToAppData()
    {
        Directory.CreateDirectory(AppDataDir);

        var currentExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine application path");

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
    /// Creates an Uninstall.bat in the install folder.
    /// The BAT calls the EXE with --uninstall, waits for it to finish,
    /// then the cleanup process deletes the remaining files.
    /// </summary>
    private static void CreateUninstallBat()
    {
        var batPath = Path.Combine(AppDataDir, "Uninstall.bat");
        var batContent = $"""
            @echo off
            echo.
            echo   GyazoDumper Uninstall
            echo   ══════════════════════════
            echo.
            "{InstalledExePath}" --uninstall
            echo.
            echo   Press any key to exit...
            pause >nul
            """;
        File.WriteAllText(batPath, batContent);
    }

    /// <summary>
    /// Extracts the embedded browser extension files.
    /// Overwrites existing files to allow updates.
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
    /// Loads existing allowed_origins from a previously created manifest.
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
    /// Creates the Native Messaging Manifest.
    /// The Web Store extension ID is always added automatically.
    /// An additional ID (e.g. for development) can optionally be specified.
    /// Existing origins are preserved.
    /// </summary>
    private static void CreateManifest(string? additionalExtensionId, List<string> existingOrigins)
    {
        var allowedOrigins = new HashSet<string>(existingOrigins);

        // Always add Web Store ID
        allowedOrigins.Add($"chrome-extension://{WebStoreExtensionId}/");

        // Add additional ID if specified
        if (!string.IsNullOrEmpty(additionalExtensionId))
        {
            allowedOrigins.Add($"chrome-extension://{additionalExtensionId}/");
        }

        var manifest = new NativeMessagingManifest
        {
            Name = HostName,
            Description = "GyazoDumper Native Messaging Host - Saves Gyazo images to any location",
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
    //  Configuration (config.json merge)
    // ========================================================================

    /// <summary>
    /// Loads an existing config.json or creates default values.
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
    /// Merges existing config.json with default values.
    /// Existing values are preserved, missing/empty keys are filled in.
    /// Returns the active save path.
    /// </summary>
    private static string MergeConfig()
    {
        var config = LoadConfig();

        // Fill empty values with defaults
        if (string.IsNullOrEmpty(config.SaveDirectory))
            config.SaveDirectory = DefaultSavePath;

        if (string.IsNullOrEmpty(config.FileNamePattern))
            config.FileNamePattern = "Gyazo_{timestamp}_{hash}{ext}";

        // Always write (ensures new keys are present)
        var json = JsonSerializer.Serialize(config, GyazoDumperJsonContext.Default.AppConfig);
        File.WriteAllText(ConfigPath, json);

        return config.SaveDirectory;
    }

    // ========================================================================
    //  Folder Shortcut (Junction)
    // ========================================================================

    /// <summary>
    /// Creates or updates the folder shortcut (junction) in the AppData folder.
    /// Called during installation and on every folder change.
    /// </summary>
    public static void UpdateFolderShortcut(string targetPath)
    {
        var linkPath = Path.Combine(AppDataDir, "Saved Images");

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
    //  Console Helpers
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
        Console.WriteLine("FAILED");
        Console.WriteLine($"        {message}");
        Console.ResetColor();
    }

    private static void WaitAndExit()
    {
        Console.WriteLine("  Press any key to exit...");
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
