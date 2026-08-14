using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// A reusable prompt template with named placeholders.
/// Templates can be versioned, cached, and rendered with different variable bindings.
/// </summary>
public interface IPromptTemplate
{
    /// <summary>Unique name identifying this template.</summary>
    string Name { get; }

    /// <summary>The raw template text with placeholders (e.g., "{{variable}}").</summary>
    string Template { get; }

    /// <summary>Gets the placeholder variable names defined in this template.</summary>
    IReadOnlyList<string> Variables { get; }

    /// <summary>Version metadata for this template.</summary>
    PromptVersionMetadata Version { get; }
}
