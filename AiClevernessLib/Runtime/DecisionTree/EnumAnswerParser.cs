using System.Text.Json;
using AiCleverness.Models.DecisionTree;

namespace AiCleverness.Runtime.DecisionTree;

/// <summary>Parses the bounded JSON response used by classify nodes.</summary>
public sealed class EnumAnswerParser
{
    private const string CodeFenceMarker = "```";
    private const char CodeFenceCharacter = '`';
    private const char NewLineCharacter = '\n';
    private const int MinimumCodeFenceLength = 3;

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

    internal static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith(CodeFenceMarker, StringComparison.Ordinal))
            return trimmed;

        var openingFenceLength = CountFenceLength(trimmed, 0);
        var openingLineEnd = trimmed.IndexOf(NewLineCharacter);
        var closingFenceStart = trimmed.Length;
        while (closingFenceStart > 0 && trimmed[closingFenceStart - 1] == CodeFenceCharacter)
            closingFenceStart--;

        var closingFenceLength = trimmed.Length - closingFenceStart;
        if (openingFenceLength < MinimumCodeFenceLength
            || openingLineEnd < 0
            || closingFenceStart <= openingLineEnd
            || closingFenceStart == trimmed.Length
            || trimmed[closingFenceStart - 1] != NewLineCharacter
            || closingFenceLength < openingFenceLength)
            return trimmed;

        var bodyEnd = closingFenceStart;
        if (bodyEnd > openingLineEnd + 1 && trimmed[bodyEnd - 1] == NewLineCharacter)
            bodyEnd--;

        return trimmed[(openingLineEnd + 1)..bodyEnd].Trim();
    }

    private static int CountFenceLength(string content, int startIndex)
    {
        var length = 0;
        while (startIndex + length < content.Length
            && content[startIndex + length] == CodeFenceCharacter)
            length++;

        return length;
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
            return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
