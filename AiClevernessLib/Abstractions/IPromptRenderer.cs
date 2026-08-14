using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Renders a prompt template into final LLM messages by substituting variables.
/// </summary>
public interface IPromptRenderer
{
    /// <summary>
    /// Renders a template with the given variable bindings.
    /// </summary>
    /// <param name="template">The prompt template to render.</param>
    /// <param name="variables">Variable values keyed by name.</param>
    /// <returns>The rendered prompt text.</returns>
    string Render(IPromptTemplate template, IReadOnlyDictionary<string, object> variables);

    /// <summary>
    /// Renders a template into LLM messages.
    /// </summary>
    IReadOnlyList<LlmMessage> RenderMessages(
        IPromptTemplate template,
        IReadOnlyDictionary<string, object> variables);
}
