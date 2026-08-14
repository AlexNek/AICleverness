using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiCleverness.Runtime;

/// <summary>
/// Shared, cached <see cref="JsonSerializerOptions"/> used across the runtime.
/// Using a single static instance avoids per-instance allocation and enables
/// source-generated serialization for Native AOT compatibility.
/// </summary>
internal static class AiClevernessJson
{
    /// <summary>
    /// Shared options with camelCase naming policy. Thread-safe for read operations.
    /// </summary>
    public static JsonSerializerOptions CamelCase { get; } = CreateCamelCase();

    /// <summary>
    /// Source-generated JSON serializer context for AOT and trimming compatibility.
    /// </summary>
    public static AiClevernessJsonContext Context { get; } = AiClevernessJsonContext.Default;

    /// <summary>
    /// Shared options with defaults. Thread-safe for read operations.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    private static JsonSerializerOptions CreateCamelCase()
    {
        var options = new JsonSerializerOptions
                          {
                              PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                          };
        return options;
    }

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions();
        return options;
    }
}

/// <summary>
/// Source-generated JSON serializer context. Provides reflection-free serialization
/// for Native AOT and trimming support.
/// </summary>
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(bool))]
internal partial class AiClevernessJsonContext : JsonSerializerContext
{
}
