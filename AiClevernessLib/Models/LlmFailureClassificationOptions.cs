namespace AiCleverness.Models;

/// <summary>
/// Application-owned provider failure classification policies.
/// </summary>
/// <remarks>
/// The core runtime does not contain provider-specific error vocabulary. Add
/// mappings here when an adapter supplies provider and error metadata without
/// setting <see cref="LlmProviderException.IsTransient"/>.
/// </remarks>
public sealed class LlmFailureClassificationOptions
{
    /// <summary>
    /// Maps a provider and error code to a failure classification.
    /// </summary>
    public IDictionary<LlmProviderErrorKey, EFailureClassification> ProviderErrorMappings { get; } =
        new Dictionary<LlmProviderErrorKey, EFailureClassification>();

    /// <summary>
    /// Maps a provider and HTTP status to a failure classification.
    /// </summary>
    public IDictionary<LlmProviderStatusKey, EFailureClassification> ProviderStatusMappings { get; } =
        new Dictionary<LlmProviderStatusKey, EFailureClassification>();
}
