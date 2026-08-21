namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// Per-execution transcript persistence sink.
/// </summary>
internal interface ITranscriptSink : IDisposable
{
    string FilePath { get; }

    void Append(string content);

    void Complete();
}
