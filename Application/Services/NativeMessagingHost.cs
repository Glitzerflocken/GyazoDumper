using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace GyazoDumper.Services;

/// <summary>
/// Native Messaging Host — communicates with the Chrome Extension.
/// 
/// Protocol:
///   - Receives JSON messages from Chrome via stdin
///   - Sends JSON responses via stdout
///   - Format: 4-byte length (little endian) + JSON
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
    /// Starts the main loop for receiving and processing messages.
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
                    // If this also fails, exit
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Reads a message from the stdin stream.
    /// </summary>
    private async Task<NativeMessage?> ReadMessageAsync(Stream stdin)
    {
        // Native Messaging protocol: 4-byte length (little endian) + JSON
        var lengthBytes = new byte[4];
        var bytesRead = await stdin.ReadAsync(lengthBytes.AsMemory(0, 4));

        if (bytesRead == 0) return null;
        if (bytesRead < 4) return null;

        var length = BitConverter.ToInt32(lengthBytes, 0);
        
        // Safety check
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
    /// Writes a response to the stdout stream.
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
    /// Processes an incoming message.
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
                        Message = "Configuration updated"
                    };
                }
                return new NativeResponse
                {
                    Success = false,
                    Error = "No path specified"
                };

            case "selectfolder":
                return await HandleSelectFolderAsync();

            case "openfolder":
                return HandleOpenFolder();

            default:
                return new NativeResponse
                {
                    Success = false,
                    Error = $"Unknown action: {message.Action}"
                };
        }
    }

    /// <summary>
    /// Handles the saveImage action.
    /// </summary>
    private async Task<NativeResponse> HandleSaveImageAsync(NativeMessage message)
    {
        if (string.IsNullOrEmpty(message.ImageUrl))
        {
            return new NativeResponse
            {
                Success = false,
                Error = "No image URL specified"
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
                Message = "Image saved successfully"
            };
        }
        catch (Exception ex)
        {
            return new NativeResponse
            {
                Success = false,
                Error = $"Download failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Opens a Windows folder picker dialog via COM IFileOpenDialog.
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
            return new NativeResponse { Success = false, Error = "Cancelled" };
        });
    }

    /// <summary>
    /// Opens the current save folder in Windows Explorer.
    /// </summary>
    private NativeResponse HandleOpenFolder()
    {
        var path = _config.SaveDirectory;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return new NativeResponse { Success = false, Error = "Folder does not exist" };
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
/// Incoming message from the Chrome Extension.
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
/// Outgoing response to the Chrome Extension.
/// </summary>
public class NativeResponse
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
