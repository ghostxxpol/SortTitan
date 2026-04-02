namespace SortTitan.Core;

public sealed class ExternalSortSplitPhase
{
    private readonly LineEntryChunkReader _chunkReader;
    private readonly LineEntryChunkSpiller _spiller;

    public ExternalSortSplitPhase() : this(new LineEntryChunkReader(), new LineEntryChunkSpiller())
    {
    }

    public ExternalSortSplitPhase(LineEntryChunkReader chunkReader, LineEntryChunkSpiller spiller)
    {
        _chunkReader = chunkReader ?? throw new ArgumentNullException(nameof(chunkReader));
        _spiller = spiller ?? throw new ArgumentNullException(nameof(spiller));
    }

    public async Task<IReadOnlyList<string>> ExecuteAsync(
        ExternalSortSplitPhaseOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.ChunkReader is null)
        {
            throw new ArgumentException("ChunkReader options are required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            throw new ArgumentException("TempDirectory is required.", nameof(options));
        }

        var runFileProvider = new TempRunFileProvider(options.TempDirectory, options.RunFilePrefix);
        var runFiles = new List<string>();

        var runIndex = 0;
        await foreach (var chunk in _chunkReader.ReadChunksAsync(options.ChunkReader, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            runIndex++;
            var tempFilePath = runFileProvider.GetRunFilePath(runIndex);

            await _spiller.SpillSortedAsync(chunk, tempFilePath, cancellationToken);
            runFiles.Add(tempFilePath);
        }

        return runFiles;
    }
}
