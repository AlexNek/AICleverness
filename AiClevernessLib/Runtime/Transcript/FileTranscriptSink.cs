using System.Text;

namespace AiCleverness.Runtime.Transcript;

/// <summary>
/// UTF-8 append-and-flush sink for one transcript file.
/// </summary>
public sealed class FileTranscriptSink : ITranscriptSink
{
    private const int FileBufferSizeBytes = 4096;

    private readonly object _gate = new();

    private readonly StreamWriter _writer;

    private bool _completed;

    public FileTranscriptSink(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new IOException("Transcript path has no directory.");

        Directory.CreateDirectory(directory);
        var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: FileBufferSizeBytes,
            options: FileOptions.SequentialScan);

        try
        {
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public string FilePath { get; }

    public void Append(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        lock (_gate)
        {
            if (_completed)
                return;

            _writer.Write(content);
            _writer.Flush();
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
                return;

            try
            {
                _writer.Flush();
            }
            finally
            {
                try
                {
                    _writer.Dispose();
                }
                finally
                {
                    _completed = true;
                }
            }
        }
    }

    public void Dispose() => Complete();
}
