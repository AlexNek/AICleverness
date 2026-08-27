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
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var openingLineEnd = trimmed.IndexOf('\n');
        var closingFenceStart = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (openingLineEnd < 0
            || closingFenceStart <= openingLineEnd
            || closingFenceStart != trimmed.Length - 3
            || trimmed[closingFenceStart - 1] != '\n')
            return trimmed;

        var bodyEnd = closingFenceStart;
        if (bodyEnd > openingLineEnd + 1 && trimmed[bodyEnd - 1] == '\n')
            bodyEnd--;

        return trimmed[(openingLineEnd + 1)..bodyEnd].Trim();
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
