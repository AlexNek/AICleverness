namespace AiCleverness.Abstractions;

/// <summary>
/// Semantic vector-based memory for similarity search.
/// Used for embedding-based retrieval of relevant context.
/// </summary>
/// <remarks>
/// Implementations may use vector databases (e.g., FAISS, Pinecone, Qdrant)
/// or in-memory cosine similarity. The in-memory implementation is suitable
/// for testing or small datasets.
/// </remarks>
public interface IVectorMemory
{
    /// <summary>Removes all entries.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the number of entries stored.</summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes an entry by id. Returns true if it existed.</summary>
    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for entries similar to the given query vector.
    /// Returns entries ordered by descending similarity.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        int topK = 5,
        double minScore = 0.0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a text entry with its embedding vector.
    /// If the entry already exists (by id), it is overwritten.
    /// </summary>
    Task UpsertAsync(VectorMemoryEntry entry, CancellationToken cancellationToken = default);
}
