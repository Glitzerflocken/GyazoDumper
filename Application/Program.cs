using GyazoDumper.Services;

namespace GyazoDumper;

/// <summary>
/// GyazoDumper - Native Messaging Host fuer Chrome Extension
/// 
/// Verwendung:
///   GyazoDumper.exe --install     Registriert den Native Host in Windows
///   GyazoDumper.exe --uninstall   Entfernt die Registrierung
///   GyazoDumper.exe               Startet im Native Messaging Modus (von Chrome aufgerufen)
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Installation/Deinstallation
            if (args.Contains("--install"))
            {
                NativeHostInstaller.Install();
                Console.WriteLine("GyazoDumper Native Messaging Host wurde erfolgreich registriert.");
                Console.WriteLine();
                Console.WriteLine("Naechste Schritte:");
                Console.WriteLine("1. Oeffne die GyazoDumper Chrome Extension");
                Console.WriteLine("2. Aktiviere 'Desktop-App verwenden'");
                Console.WriteLine("3. Setze den gewuenschten Speicherpfad");
                Console.WriteLine();
                Console.WriteLine("Druecke eine beliebige Taste zum Beenden...");
                Console.ReadKey();
                return;
            }

            if (args.Contains("--uninstall"))
            {
                NativeHostInstaller.Uninstall();
                Console.WriteLine("GyazoDumper Native Messaging Host wurde entfernt.");
                Console.WriteLine("Druecke eine beliebige Taste zum Beenden...");
                Console.ReadKey();
                return;
            }

            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            // Native Messaging Modus (wird von Chrome gestartet)
            // Keine Konsolenausgabe hier, da Chrome stdin/stdout verwendet
            var host = new NativeMessagingHost();
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            // Fehler loggen (nicht auf Console, da das Native Messaging stoert)
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
        Console.WriteLine("  GyazoDumper.exe --install     Registriert den Native Host");
        Console.WriteLine("  GyazoDumper.exe --uninstall   Entfernt die Registrierung");
        Console.WriteLine("  GyazoDumper.exe --help        Zeigt diese Hilfe");
        Console.WriteLine();
        Console.WriteLine("Nach der Installation kann die Desktop-App in der");
        Console.WriteLine("Chrome Extension aktiviert werden.");
    }
}
