using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Memory;

/// <summary>
/// In-memory implementation of <see cref="IVectorMemory"/> using cosine similarity.
/// Suitable for testing or small datasets where a full vector database is overkill.
/// </summary>
public sealed class InMemoryVectorMemory : IVectorMemory
{
    private readonly ConcurrentDictionary<string, VectorMemoryEntry> _entries = new();

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entries.Count);
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Task.FromResult(_entries.TryRemove(id, out _));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 5,
        double minScore = 0.0,
        CancellationToken cancellationToken = default)
    {
        var results = new List<VectorSearchResult>();
        var querySpan = queryVector.Span;

        foreach (var entry in _entries.Values)
        {
            var score = CosineSimilarity(querySpan, entry.Vector.Span);
            if (score >= minScore)
            {
                results.Add(new VectorSearchResult(entry, score));
            }
        }

        results.Sort((a, b) => b.Score.CompareTo(a.Score));
        var topResults = results.Count > topK ? results.GetRange(0, topK) : results;

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(topResults);
    }

    /// <inheritdoc/>
    public Task UpsertAsync(VectorMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
        return Task.CompletedTask;
    }

    private static double CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0.0;

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * (double)b[i];
            normA += a[i] * (double)a[i];
            normB += b[i] * (double)b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0.0 ? 0.0 : dotProduct / denominator;
    }
}
