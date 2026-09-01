namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Per-execution transcript persistence sink.
/// </summary>
public interface ITranscriptSink : IDisposable
{
    string FilePath { get; }

    void Append(string content);

    void Complete();
}
