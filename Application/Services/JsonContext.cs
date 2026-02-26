using System.Text.Json;
using System.Text.Json.Serialization;

namespace GyazoDumper.Services;

/// <summary>
/// JSON Source Generator Context - erzeugt trim-sichere Serialisierung
/// fuer alle Modelltypen. Vermeidet Reflection-basierte JSON-Verarbeitung
/// die beim Trimming problematisch waere.
/// </summary>
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(NativeMessage))]
[JsonSerializable(typeof(NativeResponse))]
[JsonSerializable(typeof(NativeMessagingManifest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
internal partial class GyazoDumperJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Separater Context fuer snake_case Serialisierung (Native Messaging Manifest)
/// </summary>
[JsonSerializable(typeof(NativeMessagingManifest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
internal partial class ManifestJsonContext : JsonSerializerContext
{
}
