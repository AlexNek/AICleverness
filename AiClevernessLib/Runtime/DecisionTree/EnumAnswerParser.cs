using System.Text.Json;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Parses the bounded JSON response used by question nodes.</summary>
public sealed class EnumAnswerParser
{
    public EnumAnswer? Parse(string? json, IReadOnlyList<string> allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);
        if (string.IsNullOrWhiteSpace(json) || allowedValues.Count == 0)
            return null;

        var trimmed = StripCodeFences(json);

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("answer", out var answerElement)
                || answerElement.ValueKind != JsonValueKind.String)
                return null;

            var answer = answerElement.GetString()?.Trim();
            if (string.IsNullOrEmpty(answer))
                return null;

            var canonical = allowedValues.FirstOrDefault(
                value => string.Equals(value?.Trim(), answer, StringComparison.OrdinalIgnoreCase));
            if (canonical is null)
                return null;

            var observation = ReadOptionalString(document.RootElement, "observation");
            var confidence = ReadOptionalString(document.RootElement, "confidence");
            return new EnumAnswer(canonical.Trim(), observation, confidence);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```"))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```");
        if (firstNewline >= 0 && lastFence > firstNewline)
            return trimmed[(firstNewline + 1)..lastFence].Trim();

        return trimmed;
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
