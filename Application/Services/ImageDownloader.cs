namespace GyazoDumper.Services;

/// <summary>
/// Downloads images from URLs and saves them locally.
/// </summary>
public class ImageDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ConfigurationService _config;

    public ImageDownloader(ConfigurationService config)
    {
        _config = config;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "GyazoDumper/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Downloads an image from the specified URL and saves it.
    /// </summary>
    /// <param name="imageUrl">URL of the image</param>
    /// <param name="gyazoId">Gyazo ID for the filename</param>
    /// <returns>Full path to the saved file</returns>
    public async Task<string> DownloadImageAsync(string imageUrl, string gyazoId)
    {
        var response = await _httpClient.GetAsync(imageUrl);
        response.EnsureSuccessStatusCode();

        var fileName = GenerateFileName(imageUrl, gyazoId);
        var savePath = Path.Combine(_config.SaveDirectory, fileName);

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Save image
        await using var fileStream = File.Create(savePath);
        await response.Content.CopyToAsync(fileStream);

        return savePath;
    }

    /// <summary>
    /// Generates a filename based on the configured pattern.
    /// </summary>
    private string GenerateFileName(string imageUrl, string gyazoId)
    {
        // Extract file extension from URL
        var uri = new Uri(imageUrl);
        var extension = Path.GetExtension(uri.AbsolutePath);

        if (string.IsNullOrEmpty(extension))
        {
            extension = ".png";
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        return _config.FileNamePattern
            .Replace("{hash}", gyazoId)
            .Replace("{timestamp}", timestamp)
            .Replace("{ext}", extension);
    }
}
