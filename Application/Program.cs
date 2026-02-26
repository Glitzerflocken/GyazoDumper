using GyazoDumper.Services;

namespace GyazoDumper;

/// <summary>
/// GyazoDumper - Native Messaging Host fuer Chrome Extension
/// 
/// Erkennung des Startmodus:
///   - Chrome/Edge leitet stdin um (Pipe) → Native Messaging Modus
///   - Benutzer-Doppelklick: stdin ist Konsole → Interaktiver Setup
///   - --install / --uninstall → Kommandozeilen-Modus
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
                var idIndex = Array.IndexOf(args, "--install") + 1;
                string? extensionId = (idIndex < args.Length && !args[idIndex].StartsWith("--"))
                    ? args[idIndex]
                    : null;

                NativeHostInstaller.Install(extensionId);
                Console.WriteLine("GyazoDumper wurde erfolgreich installiert.");
                return;
            }

            // Kernunterscheidung: Chrome leitet stdin als Pipe um,
            // bei Doppelklick ist stdin die Konsole.
            if (Console.IsInputRedirected)
            {
                // Chrome/Edge hat uns gestartet → Native Messaging Modus
                var host = new NativeMessagingHost();
                await host.RunAsync();
            }
            else
            {
                // Benutzer hat die EXE direkt gestartet → Setup
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
        Console.WriteLine("Verwendung:");
        Console.WriteLine("  GyazoDumper.exe                  Interaktiver Setup-Assistent");
        Console.WriteLine("  GyazoDumper.exe --install [ID]   Stille Installation");
        Console.WriteLine("  GyazoDumper.exe --uninstall      Entfernt die Installation");
        Console.WriteLine("  GyazoDumper.exe --help           Zeigt diese Hilfe");
        Console.WriteLine();
        Console.WriteLine("Wird automatisch von Chrome/Edge als Native Host gestartet");
        Console.WriteLine("wenn stdin umgeleitet ist (Pipe).");
    }
}
