using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>
/// In-memory artifact carrying a byte array.
/// </summary>
public sealed class ExecutionArtifact : IExecutionArtifact
{
    private readonly byte[] _data;

    public string ContentType { get; }

    public string Name { get; }

    public ExecutionArtifact(
        string name,
        byte[] data,
        string contentType = "application/octet-stream")
    {
        Name = name;
        _data = data;
        ContentType = contentType;
    }

    public ExecutionArtifact(string name, string text, string contentType = "text/plain")
    {
        Name = name;
        _data = System.Text.Encoding.UTF8.GetBytes(text);
        ContentType = contentType;
    }

    public Task<byte[]> GetBytesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_data);
    }

    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream(_data));
    }
}
