using System.Text.Json.Serialization;

namespace GyazoDumper.Bootstrapper;

[JsonSerializable(typeof(ManifestModel))]
[JsonSerializable(typeof(ConfigModel))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class BootstrapperJsonContext : JsonSerializerContext { }

public class ManifestModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("allowed_origins")]
    public string[] AllowedOrigins { get; set; } = [];
}

public class ConfigModel
{
    [JsonPropertyName("saveDirectory")]
    public string SaveDirectory { get; set; } = string.Empty;

    [JsonPropertyName("fileNamePattern")]
    public string FileNamePattern { get; set; } = string.Empty;
}
