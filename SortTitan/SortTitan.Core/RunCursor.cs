using System.Text;

namespace SortTitan.Core;

public sealed class RunCursor : IAsyncDisposable
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly LineEntryParser _parser;
    private readonly FileStream _stream;
    private readonly StreamReader _reader;
    private long _lineNumber;
    private bool _disposed;

    public string Path { get; }
    public LineEntry Current { get; private set; }

    public RunCursor(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Run file path is required.", nameof(path));
        }

        Path = path;
        _parser = new LineEntryParser();
        _stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        _reader = new StreamReader(
            _stream,
            Utf8NoBom,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024 * 1024,
            leaveOpen: false);
    }

    public async ValueTask<bool> MoveNextAsync(CancellationToken cancellationToken)
    {
        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            return false;
        }

        _lineNumber++;

        if (!_parser.TryParse(line, out var entry))
        {
            throw new InvalidDataException($"Invalid line in run file '{Path}' at {_lineNumber}.");
        }

        Current = entry;
        return true;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return default;
        }

        _disposed = true;
        _reader.Dispose();
        return _stream.DisposeAsync();
    }
}
