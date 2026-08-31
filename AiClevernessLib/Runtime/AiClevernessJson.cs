using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiCleverness.Runtime;

/// <summary>Shared, cached JSON serializer options used across the runtime.</summary>
internal static class AiClevernessJson
{
    public static JsonSerializerOptions CamelCase { get; } = CreateCamelCase();
    public static AiClevernessJsonContext Context { get; } = AiClevernessJsonContext.Default;
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    private static JsonSerializerOptions CreateCamelCase() =>
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static JsonSerializerOptions CreateDefault() => new();
}
