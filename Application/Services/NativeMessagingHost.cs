using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

    public NativeMessagingHost()
    {
        _config = new ConfigurationService();
        _downloader = new ImageDownloader(_config);
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
        return JsonSerializer.Deserialize(json, GyazoDumperJsonContext.Default.NativeMessage);
    }

    /// <summary>
    /// Schreibt eine Antwort in den stdout-Stream
    /// </summary>
    private async Task WriteMessageAsync(Stream stdout, NativeResponse response)
    {
        var json = JsonSerializer.Serialize(response, GyazoDumperJsonContext.Default.NativeResponse);
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

            case "openfolder":
                return HandleOpenFolder();

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
    /// Oeffnet einen Windows-Ordnerauswahl-Dialog via COM IFileOpenDialog
    /// </summary>
    private Task<NativeResponse> HandleSelectFolderAsync()
    {
        return Task.Run(() =>
        {
            var selectedPath = FolderPicker.ShowDialog(_config.SaveDirectory);

            if (selectedPath != null)
            {
                _config.UpdateSaveDirectory(selectedPath);
                return new NativeResponse { Success = true, Message = selectedPath };
            }
            return new NativeResponse { Success = false, Error = "Abgebrochen" };
        });
    }

    /// <summary>
    /// Oeffnet den aktuellen Speicherordner im Windows Explorer
    /// </summary>
    private NativeResponse HandleOpenFolder()
    {
        var path = _config.SaveDirectory;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return new NativeResponse { Success = false, Error = "Ordner existiert nicht" };
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path
            });
            return new NativeResponse { Success = true, Message = path };
        }
        catch (Exception ex)
        {
            return new NativeResponse { Success = false, Error = ex.Message };
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
