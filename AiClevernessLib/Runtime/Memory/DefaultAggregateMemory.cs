using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Memory;

/// <summary>
/// Default implementation of <see cref="IAggregateMemory"/> that composes all memory tiers.
/// The <see cref="IAgentMemory"/> methods delegate to the long-term store for backward compatibility.
/// </summary>
public sealed class DefaultAggregateMemory : IAggregateMemory
{
    /// <inheritdoc/>
    public ILongTermMemory LongTerm { get; }

    /// <inheritdoc/>
    public IVectorMemory Vector { get; }

    /// <inheritdoc/>
    public IWorkingMemory Working { get; }

    public DefaultAggregateMemory()
        : this(
            new InMemoryWorkingMemory(),
            new InMemoryLongTermMemory(),
            new InMemoryVectorMemory())
    {
    }

    public DefaultAggregateMemory(
        IWorkingMemory working,
        ILongTermMemory longTerm,
        IVectorMemory vector)
    {
        Working = working ?? throw new ArgumentNullException(nameof(working));
        LongTerm = longTerm ?? throw new ArgumentNullException(nameof(longTerm));
        Vector = vector ?? throw new ArgumentNullException(nameof(vector));
    }

    /// <inheritdoc/>
    public Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default) =>
        LongTerm.ContainsAsync(key, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>>
        GetKeysAsync(CancellationToken cancellationToken = default) =>
        LongTerm.GetKeysAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default) =>
        LongTerm.LoadAsync<T>(key, cancellationToken);

    // IAgentMemory backward-compatible implementation delegates to LongTerm.

    /// <inheritdoc/>
    public Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
        LongTerm.SaveAsync(key, value, cancellationToken);
}
