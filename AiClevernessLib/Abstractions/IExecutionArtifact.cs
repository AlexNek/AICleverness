namespace AiCleverness.Abstractions;

/// <summary>
/// Represents a non-text output artifact produced during execution.
/// Artifacts carry typed data alongside an agent result.
/// </summary>
public interface IExecutionArtifact
{
    /// <summary>MIME type of the artifact content.</summary>
    string ContentType { get; }

    /// <summary>Artifact name or identifier.</summary>
    string Name { get; }

    /// <summary>Gets the artifact data as a byte array.</summary>
    Task<byte[]> GetBytesAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the artifact data as a stream.</summary>
    Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}
