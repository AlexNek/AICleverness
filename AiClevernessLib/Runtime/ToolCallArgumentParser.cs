using System.Text.Json;

namespace AiCleverness.Runtime;

/// <summary>
/// Parses LLM tool call argument payloads (JSON strings) into .NET dictionaries.
/// Malformed or empty payloads yield an empty dictionary instead of failing the run.
/// </summary>
internal static class ToolCallArgumentParser
{
    /// <summary>
    /// Parses a JSON object string into an argument dictionary.
    /// Returns an empty dictionary for null/whitespace input or invalid JSON.
    /// </summary>
    public static Dictionary<string, object> Parse(string? argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                result[property.Name] = ConvertJsonElement(property.Value);
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => ConvertJsonElement(p.Value)),
                _ => element.ToString()
            };
    }
}
