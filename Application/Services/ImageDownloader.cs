namespace GyazoDumper.Services;

/// <summary>
/// Laedt Bilder von URLs herunter und speichert sie lokal
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
    /// Laedt ein Bild von der angegebenen URL herunter und speichert es
    /// </summary>
    /// <param name="imageUrl">URL des Bildes</param>
    /// <param name="gyazoId">Gyazo ID fuer den Dateinamen</param>
    /// <returns>Vollstaendiger Pfad zur gespeicherten Datei</returns>
    public async Task<string> DownloadImageAsync(string imageUrl, string gyazoId)
    {
        var response = await _httpClient.GetAsync(imageUrl);
        response.EnsureSuccessStatusCode();

        var fileName = GenerateFileName(imageUrl, gyazoId);
        var savePath = Path.Combine(_config.SaveDirectory, fileName);

        // Verzeichnis erstellen falls nicht vorhanden
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Bild speichern
        await using var fileStream = File.Create(savePath);
        await response.Content.CopyToAsync(fileStream);

        return savePath;
    }

    /// <summary>
    /// Generiert einen Dateinamen basierend auf dem konfigurierten Muster
    /// </summary>
    private string GenerateFileName(string imageUrl, string gyazoId)
    {
        // Dateiendung aus URL extrahieren
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
