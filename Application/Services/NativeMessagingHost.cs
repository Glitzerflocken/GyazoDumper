using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace GyazoDumper.Services;

/// <summary>
/// Native Messaging Host - Kommuniziert mit der Chrome Extension
/// 
/// Protokoll:
///   - Empfaengt JSON-Nachrichten von Chrome ueber stdin
///   - Sendet JSON-Antworten ueber stdout
///   - Format: 4-Byte Laenge (Little Endian) + JSON
/// </summary>
public class NativeMessagingHost
{
    private readonly ImageDownloader _downloader;
    private readonly ConfigurationService _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public NativeMessagingHost()
    {
        _config = new ConfigurationService();
        _downloader = new ImageDownloader(_config);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Startet die Hauptschleife zum Empfangen und Verarbeiten von Nachrichten
    /// </summary>
    public async Task RunAsync()
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        while (true)
        {
            try
            {
                var message = await ReadMessageAsync(stdin);
                if (message == null) break;

                var response = await ProcessMessageAsync(message);
                await WriteMessageAsync(stdout, response);
            }
            catch (Exception ex)
            {
                var errorResponse = new NativeResponse
                {
                    Success = false,
                    Error = ex.Message
                };
                
                try
                {
                    await WriteMessageAsync(Console.OpenStandardOutput(), errorResponse);
                }
                catch
                {
                    // Wenn auch das fehlschlaegt, beenden
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Liest eine Nachricht aus dem stdin-Stream
    /// </summary>
    private async Task<NativeMessage?> ReadMessageAsync(Stream stdin)
    {
        // Native Messaging Protokoll: 4-Byte Laenge (Little Endian) + JSON
        var lengthBytes = new byte[4];
        var bytesRead = await stdin.ReadAsync(lengthBytes.AsMemory(0, 4));

        if (bytesRead == 0) return null;
        if (bytesRead < 4) return null;

        var length = BitConverter.ToInt32(lengthBytes, 0);
        
        // Sicherheitspruefung
        if (length <= 0 || length > 1024 * 1024) // Max 1MB
        {
            return null;
        }

        var messageBytes = new byte[length];
        var totalRead = 0;
        while (totalRead < length)
        {
            var read = await stdin.ReadAsync(messageBytes.AsMemory(totalRead, length - totalRead));
            if (read == 0) break;
            totalRead += read;
        }

        var json = Encoding.UTF8.GetString(messageBytes, 0, totalRead);
        return JsonSerializer.Deserialize<NativeMessage>(json, _jsonOptions);
    }

    /// <summary>
    /// Schreibt eine Antwort in den stdout-Stream
    /// </summary>
    private async Task WriteMessageAsync(Stream stdout, NativeResponse response)
    {
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var messageBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

        await stdout.WriteAsync(lengthBytes.AsMemory(0, 4));
        await stdout.WriteAsync(messageBytes);
        await stdout.FlushAsync();
    }

    /// <summary>
    /// Verarbeitet eine eingehende Nachricht
    /// </summary>
    private async Task<NativeResponse> ProcessMessageAsync(NativeMessage message)
    {
        switch (message.Action?.ToLower())
        {
            case "ping":
                return new NativeResponse
                {
                    Success = true,
                    Message = "pong"
                };

            case "saveimage":
                return await HandleSaveImageAsync(message);

            case "getconfig":
                return new NativeResponse
                {
                    Success = true,
                    Message = _config.SaveDirectory
                };

            case "setconfig":
                if (!string.IsNullOrEmpty(message.SaveDirectory))
                {
                    _config.UpdateSaveDirectory(message.SaveDirectory);
                    return new NativeResponse
                    {
                        Success = true,
                        Message = "Konfiguration aktualisiert"
                    };
                }
                return new NativeResponse
                {
                    Success = false,
                    Error = "Kein Pfad angegeben"
                };

            case "selectfolder":
                return await HandleSelectFolderAsync();

            default:
                return new NativeResponse
                {
                    Success = false,
                    Error = $"Unbekannte Aktion: {message.Action}"
                };
        }
    }

    /// <summary>
    /// Behandelt die saveImage Aktion
    /// </summary>
    private async Task<NativeResponse> HandleSaveImageAsync(NativeMessage message)
    {
        if (string.IsNullOrEmpty(message.ImageUrl))
        {
            return new NativeResponse
            {
                Success = false,
                Error = "Keine Bild-URL angegeben"
            };
        }

        try
        {
            var filePath = await _downloader.DownloadImageAsync(
                message.ImageUrl, 
                message.GyazoId ?? Guid.NewGuid().ToString("N")
            );

            return new NativeResponse
            {
                Success = true,
                FilePath = filePath,
                Message = "Bild erfolgreich gespeichert"
            };
        }
        catch (Exception ex)
        {
            return new NativeResponse
            {
                Success = false,
                Error = $"Download fehlgeschlagen: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Oeffnet einen Windows-Ordnerauswahl-Dialog im Vordergrund.
    /// Da der Native Host ein Hintergrund-Prozess ist (von Chrome gestartet),
    /// muss der Input-Thread an den Vordergrund-Thread angehaengt werden
    /// damit Windows das SetForegroundWindow erlaubt.
    /// </summary>
    private Task<NativeResponse> HandleSelectFolderAsync()
    {
        return Task.Run(() =>
        {
            string? selectedPath = null;

            var thread = new Thread(() =>
            {
                var ownerForm = new Form
                {
                    TopMost = true,
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Size = System.Drawing.Size.Empty,
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-9999, -9999)
                };
                ownerForm.Show();

                // Input-Thread an den aktuellen Vordergrund-Thread anhaengen,
                // damit unser Prozess SetForegroundWindow aufrufen darf
                ForceForeground(ownerForm.Handle);

                using var dialog = new FolderBrowserDialog
                {
                    Description = "Zielordner fuer GyazoDumper auswaehlen",
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };

                if (!string.IsNullOrEmpty(_config.SaveDirectory) && Directory.Exists(_config.SaveDirectory))
                {
                    dialog.InitialDirectory = _config.SaveDirectory;
                }

                if (dialog.ShowDialog(ownerForm) == DialogResult.OK)
                {
                    selectedPath = dialog.SelectedPath;
                }

                ownerForm.Close();
                ownerForm.Dispose();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (selectedPath != null)
            {
                _config.UpdateSaveDirectory(selectedPath);
                return new NativeResponse { Success = true, Message = selectedPath };
            }
            return new NativeResponse { Success = false, Error = "Abgebrochen" };
        });
    }

    // ========================================================================
    //  Win32 API: Fenster in den Vordergrund zwingen
    // ========================================================================

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>
    /// Zwingt ein Fenster in den Vordergrund, auch aus einem Hintergrund-Prozess.
    /// Haengt den eigenen Input-Thread an den Thread des aktuellen Vordergrund-Fensters,
    /// ruft SetForegroundWindow auf, und trennt die Threads wieder.
    /// </summary>
    private static void ForceForeground(IntPtr hWnd)
    {
        var foregroundWnd = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foregroundWnd, out _);
        var currentThread = GetCurrentThreadId();

        if (foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, true);
            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
            AttachThreadInput(currentThread, foregroundThread, false);
        }
        else
        {
            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
        }
    }
}

/// <summary>
/// Eingehende Nachricht von der Chrome Extension
/// </summary>
public class NativeMessage
{
    public string? Action { get; set; }
    public string? ImageUrl { get; set; }
    public string? GyazoId { get; set; }
    public string? SourceUrl { get; set; }
    public string? Timestamp { get; set; }
    public string? SaveDirectory { get; set; }
}

/// <summary>
/// Ausgehende Antwort an die Chrome Extension
/// </summary>
public class NativeResponse
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
