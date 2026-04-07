namespace SortTitan.Core;

using System.Diagnostics;

public sealed class ExternalFileSorter
{
    private readonly ExternalSortSplitPhase _splitPhase;
    private readonly ExternalSortMergePhase _mergePhase;

    public ExternalFileSorter() : this(new ExternalSortSplitPhase(), new ExternalSortMergePhase())
    {
    }

    public ExternalFileSorter(ExternalSortSplitPhase splitPhase, ExternalSortMergePhase mergePhase)
    {
        _splitPhase = splitPhase ?? throw new ArgumentNullException(nameof(splitPhase));
        _mergePhase = mergePhase ?? throw new ArgumentNullException(nameof(mergePhase));
    }

    public async Task SortAsync(ExternalFileSorterOptions options, CancellationToken cancellationToken = default)
    {
        await SortWithMetricsAsync(options, cancellationToken);
    }

    public async Task<ExternalFileSorterMetrics> SortWithMetricsAsync(
        ExternalFileSorterOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("InputPath is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("OutputPath is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.TempDirectory))
        {
            throw new ArgumentException("TempDirectory is required.", nameof(options));
        }

        var sessionTempDirectory = Path.Combine(options.TempDirectory, $"SortTitan_{Guid.NewGuid():N}");
        IReadOnlyList<string>? runFiles = null;
        var success = false;
        var splitTime = TimeSpan.Zero;
        var mergeTime = TimeSpan.Zero;
        var tempBytes = 0L;
        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            var chunkReaderOptions = new ChunkReaderOptions
            {
                InputPath = options.InputPath,
                MemoryBudgetFraction = options.MemoryBudgetFraction,
                TotalMemoryBudgetBytesOverride = options.TotalMemoryBudgetBytesOverride,
                MaxInFlightChunks = options.MaxInFlightChunks,
                InvalidLineHandling = InvalidLineHandling.Throw,
            };

            var splitOptions = new ExternalSortSplitPhaseOptions
            {
                ChunkReader = chunkReaderOptions,
                TempDirectory = sessionTempDirectory,
                RunFilePrefix = options.RunFilePrefix,
                MaxConcurrentSpills = options.MaxConcurrentSpills,
            };

            var splitStopwatch = Stopwatch.StartNew();
            runFiles = await _splitPhase.ExecuteAsync(splitOptions, cancellationToken);
            splitStopwatch.Stop();
            splitTime = splitStopwatch.Elapsed;

            tempBytes = SumFileSizes(runFiles);

            if (runFiles.Count == 0)
            {
                var outputDir = Path.GetDirectoryName(options.OutputPath);
                if (!string.IsNullOrWhiteSpace(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                await using var outputStream = new FileStream(
                    options.OutputPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    useAsync: true);

                await outputStream.FlushAsync(cancellationToken);
                success = true;
                totalStopwatch.Stop();

                return new ExternalFileSorterMetrics
                {
                    TotalTime = totalStopwatch.Elapsed,
                    SplitPhaseTime = splitTime,
                    MergePhaseTime = TimeSpan.Zero,
                    MergePasses = 0,
                    TempFilesCount = 0,
                    InputBytes = GetFileSize(options.InputPath),
                    TempBytes = 0,
                    OutputBytes = GetFileSize(options.OutputPath),
                };
            }

            var mergeOptions = new ExternalSortMergePhaseOptions
            {
                InputRunFiles = runFiles,
                OutputPath = options.OutputPath,
                TempDirectory = sessionTempDirectory,
                RunFilePrefix = options.RunFilePrefix,
                FanIn = options.MergeFanIn,
                KeepTempFilesOnError = options.KeepTempFilesOnError,
            };

            var mergeStopwatch = Stopwatch.StartNew();
            var mergeMetrics = await _mergePhase.ExecuteAsync(mergeOptions, cancellationToken);
            mergeStopwatch.Stop();
            mergeTime = mergeStopwatch.Elapsed;
            success = true;

            totalStopwatch.Stop();
            return new ExternalFileSorterMetrics
            {
                TotalTime = totalStopwatch.Elapsed,
                SplitPhaseTime = splitTime,
                MergePhaseTime = mergeTime,
                MergePasses = mergeMetrics.Passes,
                TempFilesCount = runFiles.Count + mergeMetrics.IntermediateFilesCreated,
                InputBytes = GetFileSize(options.InputPath),
                TempBytes = tempBytes + mergeMetrics.IntermediateBytesWritten,
                OutputBytes = GetFileSize(options.OutputPath),
            };
        }
        finally
        {
            if (success || !options.KeepTempFilesOnError)
            {
                TryCleanupRunFiles(runFiles, sessionTempDirectory);
            }
        }
    }

    private static void TryCleanupRunFiles(IReadOnlyList<string>? runFiles, string sessionTempDirectory)
    {
        if (runFiles is not null)
        {
            foreach (var file in runFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                }
            }
        }

        try
        {
            if (Directory.Exists(sessionTempDirectory))
            {
                Directory.Delete(sessionTempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static long GetFileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static long SumFileSizes(IReadOnlyList<string> paths)
    {
        long sum = 0;
        foreach (var path in paths)
        {
            sum += GetFileSize(path);
        }

        return sum;
    }
}
