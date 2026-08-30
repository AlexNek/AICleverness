using System.Text.RegularExpressions;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

/// <summary>
/// Simple prompt renderer that substitutes {{variable}} placeholders with provided values.
/// </summary>
public sealed partial class SimplePromptRenderer : IPromptRenderer
{
    private static readonly Regex PlaceholderRegex = CreatePlaceholderRegex();

    /// <inheritdoc/>
    public string Render(IPromptTemplate template, IReadOnlyDictionary<string, object> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        return PlaceholderRegex.Replace(
            template.Template,
            match =>
                {
                    var name = match.Groups[1].Value;
                    return variables.TryGetValue(name, out var value)
                               ? value?.ToString() ?? string.Empty
                               : match.Value; // Leave unresolved placeholders as-is
                });
    }

    /// <inheritdoc/>
    public IReadOnlyList<LlmMessage> RenderMessages(
        IPromptTemplate template,
        IReadOnlyDictionary<string, object> variables)
    {
        var rendered = Render(template, variables);
        return [new LlmMessage(LlmMessageRoles.User, rendered)];
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex CreatePlaceholderRegex();
}
