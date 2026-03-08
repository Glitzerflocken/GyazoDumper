using System.Text.Json;
using System.Text.Json.Serialization;

namespace GyazoDumper.Services;

/// <summary>
/// JSON Source Generator Context — produces trim-safe serialization
/// for all model types. Avoids reflection-based JSON processing
/// which would be problematic with trimming.
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
/// Separate context for snake_case serialization (Native Messaging Manifest).
/// </summary>
[JsonSerializable(typeof(NativeMessagingManifest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true)]
internal partial class ManifestJsonContext : JsonSerializerContext
{
}
