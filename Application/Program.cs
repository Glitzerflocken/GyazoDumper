using GyazoDumper.Services;

namespace GyazoDumper;

/// <summary>
/// GyazoDumper - Native Messaging Host fuer Chrome Extension
/// 
/// Verwendung:
///   GyazoDumper.exe                     Interaktiver Setup-Assistent (Doppelklick)
///   GyazoDumper.exe --install [ID]      Stille Installation (optional mit Extension-ID)
///   GyazoDumper.exe --uninstall         Entfernt die Installation
///   GyazoDumper.exe chrome-extension:// Wird von Chrome/Edge als Native Host gestartet
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Chrome/Edge startet den Native Host mit "chrome-extension://ID/" als Argument
            if (args.Any(a => a.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)))
            {
                var host = new NativeMessagingHost();
                await host.RunAsync();
                return;
            }

            if (args.Contains("--uninstall"))
            {
                Console.WriteLine();
                Console.WriteLine("  GyazoDumper wird deinstalliert...");
                Console.WriteLine();
                NativeHostInstaller.Uninstall();
                Console.WriteLine();
                Console.WriteLine("  Deinstallation abgeschlossen.");
                Console.WriteLine("  Druecke eine beliebige Taste zum Beenden...");
                Console.ReadKey(true);
                return;
            }

            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            if (args.Contains("--install"))
            {
                // Stille Installation mit optionaler Extension-ID
                var idIndex = Array.IndexOf(args, "--install") + 1;
                string? extensionId = (idIndex < args.Length && !args[idIndex].StartsWith("--"))
                    ? args[idIndex]
                    : null;

                NativeHostInstaller.Install(extensionId);
                Console.WriteLine("GyazoDumper wurde erfolgreich installiert.");
                return;
            }

            // Standard: Interaktiver Setup-Assistent (Doppelklick auf EXE)
            NativeHostInstaller.InteractiveInstall();
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
        Console.WriteLine("Verwendung:");
        Console.WriteLine("  GyazoDumper.exe                  Interaktiver Setup-Assistent");
        Console.WriteLine("  GyazoDumper.exe --install [ID]   Stille Installation");
        Console.WriteLine("  GyazoDumper.exe --uninstall      Entfernt die Installation");
        Console.WriteLine("  GyazoDumper.exe --help           Zeigt diese Hilfe");
        Console.WriteLine();
        Console.WriteLine("Setup: Starte die EXE per Doppelklick und folge den Anweisungen.");
        Console.WriteLine("Die Extension-ID findest du im GyazoDumper Browser-Popup.");
    }
}
