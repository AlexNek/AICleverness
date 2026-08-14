using System.Collections;
using System.Collections.Concurrent;

using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IExecutionArtifactCollection"/>.
/// </summary>
public sealed class DefaultExecutionArtifactCollection : IExecutionArtifactCollection
{
    private readonly ConcurrentDictionary<string, IExecutionArtifact> _artifacts = new();

    /// <inheritdoc/>
    public int Count => _artifacts.Count;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Names => _artifacts.Keys.ToArray();

    /// <inheritdoc/>
    public void Add(IExecutionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!_artifacts.TryAdd(artifact.Name, artifact))
        {
            // Overwrite on duplicate name (last-write-wins)
            _artifacts[artifact.Name] = artifact;
        }
    }

    /// <inheritdoc/>
    public bool Contains(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _artifacts.ContainsKey(name);
    }

    /// <inheritdoc/>
    public IExecutionArtifact? Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _artifacts.TryGetValue(name, out var artifact) ? artifact : null;
    }

    /// <inheritdoc/>
    public IEnumerator<IExecutionArtifact> GetEnumerator() => _artifacts.Values.GetEnumerator();

    /// <inheritdoc/>
    public IReadOnlyList<IExecutionArtifact> ToList() => _artifacts.Values.ToArray();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
