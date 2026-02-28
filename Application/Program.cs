using GyazoDumper.Services;

namespace GyazoDumper;

/// <summary>
/// GyazoDumper - Native Messaging Host for Chrome Extension
/// 
/// Start mode detection:
///   - Chrome/Edge redirects stdin (pipe) → Native Messaging mode
///   - User double-click: stdin is console → Interactive Setup
///   - --install / --uninstall → Command-line mode
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            if (args.Contains("--uninstall"))
            {
                Console.WriteLine();
                Console.WriteLine("  Uninstalling GyazoDumper...");
                Console.WriteLine();
                NativeHostInstaller.Uninstall();
                Console.WriteLine();
                Console.WriteLine("  Uninstall complete.");
                return;
            }

            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            if (args.Contains("--install"))
            {
                var idIndex = Array.IndexOf(args, "--install") + 1;
                string? extensionId = (idIndex < args.Length && !args[idIndex].StartsWith("--"))
                    ? args[idIndex]
                    : null;

                NativeHostInstaller.Install(extensionId);
                Console.WriteLine("GyazoDumper has been successfully installed.");
                return;
            }

            // Core distinction: Chrome redirects stdin as a pipe,
            // on double-click stdin is the console.
            if (Console.IsInputRedirected)
            {
                // Chrome/Edge started us → Native Messaging mode
                var host = new NativeMessagingHost();
                await host.RunAsync();
            }
            else
            {
                // User started the EXE directly → Setup
                NativeHostInstaller.InteractiveInstall();
            }
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GyazoDumper",
                "error.log"
            );
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.AppendAllTextAsync(logPath, $"{DateTime.Now}: {ex}\n");
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("GyazoDumper - Native Messaging Host");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  GyazoDumper.exe                  Interactive setup wizard");
        Console.WriteLine("  GyazoDumper.exe --install [ID]   Silent installation");
        Console.WriteLine("  GyazoDumper.exe --uninstall      Removes the installation");
        Console.WriteLine("  GyazoDumper.exe --help           Shows this help");
        Console.WriteLine();
        Console.WriteLine("Automatically started by Chrome/Edge as Native Host");
        Console.WriteLine("when stdin is redirected (pipe).");
    }
}
