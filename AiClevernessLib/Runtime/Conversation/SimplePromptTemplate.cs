using System.Text.RegularExpressions;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Conversation;

/// <summary>
/// Simple prompt template using {{variable}} placeholder syntax.
/// </summary>
public sealed partial class SimplePromptTemplate : IPromptTemplate
{
    private static readonly Regex PlaceholderRegex = CreatePlaceholderRegex();

    public string Name { get; }

    public string Template { get; }

    public IReadOnlyList<string> Variables { get; }

    public PromptVersionMetadata Version { get; }

    public SimplePromptTemplate(string name, string template, PromptVersionMetadata? version = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(template);

        Name = name;
        Template = template;
        Version = version ?? new PromptVersionMetadata { VersionString = "1.0.0" };
        Variables = PlaceholderRegex.Matches(template)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex CreatePlaceholderRegex();
}
