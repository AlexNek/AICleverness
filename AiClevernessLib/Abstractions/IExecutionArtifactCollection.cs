namespace AiCleverness.Abstractions;

/// <summary>
/// Thread-safe collection for artifacts produced during an execution.
/// Provides add, retrieval, and enumeration of <see cref="IExecutionArtifact"/> instances.
/// </summary>
public interface IExecutionArtifactCollection : IEnumerable<IExecutionArtifact>
{
    /// <summary>Gets the number of artifacts in the collection.</summary>
    int Count { get; }

    /// <summary>Gets all artifact names.</summary>
    IReadOnlyCollection<string> Names { get; }

    /// <summary>Adds an artifact to the collection.</summary>
    void Add(IExecutionArtifact artifact);

    /// <summary>Returns true if an artifact with the given name exists.</summary>
    bool Contains(string name);

    /// <summary>Gets an artifact by name. Returns null if not found.</summary>
    IExecutionArtifact? Get(string name);

    /// <summary>Gets all artifacts as a read-only list.</summary>
    IReadOnlyList<IExecutionArtifact> ToList();
}
