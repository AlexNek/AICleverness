using System.Text;
using AiCleverness.Runtime.Transcript;

namespace AiClevernessLib.Tests.Testing;

internal sealed class RecordingTranscriptSink : ITranscriptSink
{
    private readonly object _gate = new();
    private readonly StringBuilder _content = new();
    private bool _completed;

    public RecordingTranscriptSink(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public bool IsCompleted
    {
        get
        {
            lock (_gate)
                return _completed;
        }
    }

    public string Content
    {
        get
        {
            lock (_gate)
                return _content.ToString();
        }
    }

    public void Append(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        lock (_gate)
        {
            if (_completed)
                return;

            _content.Append(content);
        }
    }

    public void Complete()
    {
        lock (_gate)
            _completed = true;
    }

    public void Dispose() => Complete();
}
