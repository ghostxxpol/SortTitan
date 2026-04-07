﻿﻿﻿using System.Globalization;
using SortTitan.Core;

static int PrintUsage(string? error = null)
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
    }

    Console.WriteLine("SortTitan.Sorter");
    Console.WriteLine("Sorts a file with lines in format: <Number>. <String>");
    Console.WriteLine("Order: Text (Ordinal), then Number (ascending).");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  SortTitan.Sorter --input <path> --output <path> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --input <path>           Input file path (required)");
    Console.WriteLine("  --output <path>          Output file path (required)");
    Console.WriteLine("  --temp <dir>             Temp directory (default: %TEMP%\\SortTitan)");
    Console.WriteLine("  --mem-frac <double>      Memory budget fraction 0..1 (default: 0.25)");
    Console.WriteLine("  --mem-bytes <long>       Total memory budget override in bytes (optional)");
    Console.WriteLine("  --max-inflight <int>     Max in-flight chunks (default: 1)");
    Console.WriteLine("  --max-entries <int>      Max entries per chunk (optional)");
    Console.WriteLine("  --spill-parallelism <int>  Max concurrent temp writes (default: 1)");
    Console.WriteLine("  --merge-fanin <int>      Max run files to merge at once (default: 128)");
    Console.WriteLine("  --keep-temp-on-error <true|false>   Keep temp files on error (default: true)");
    Console.WriteLine("  --delete-temp-on-error             Delete temp files on error");
    Console.WriteLine("  -h | --help              Show help");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine(@"  dotnet run --project SortTitan.Sorter -- --input ""input.txt"" --output ""sorted.txt"" --temp ""%TEMP%\SortTitan"" --mem-frac 0.25");

    return 2;
}

static bool TryGetOption(string[] args, string name, out string? value)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[i + 1];
        return true;
    }

    value = null;
    return false;
}

static string? GetOptionOrNull(string[] args, string name)
{
    return TryGetOption(args, name, out var value) ? value : null;
}

static bool HasFlag(string[] args, string name)
{
    return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}

if (args.Length == 0 || HasFlag(args, "-h") || HasFlag(args, "--help"))
{
    Environment.ExitCode = PrintUsage();
    return;
}

var inputPath = GetOptionOrNull(args, "--input");
var outputPath = GetOptionOrNull(args, "--output");
if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputPath))
{
    Environment.ExitCode = PrintUsage("Missing required arguments: --input and/or --output.");
    return;
}

var tempDir = GetOptionOrNull(args, "--temp");
if (string.IsNullOrWhiteSpace(tempDir))
{
    tempDir = Path.Combine(System.IO.Path.GetTempPath(), "SortTitan");
}

double memFrac = 0.25;
var memFracText = GetOptionOrNull(args, "--mem-frac");
if (!string.IsNullOrWhiteSpace(memFracText))
{
    if (!double.TryParse(memFracText, NumberStyles.Float, CultureInfo.InvariantCulture, out memFrac) || memFrac is < 0 or > 1)
    {
        Environment.ExitCode = PrintUsage("Invalid --mem-frac. Must be a number in range 0..1.");
        return;
    }
}

long? memBytesOverride = null;
var memBytesText = GetOptionOrNull(args, "--mem-bytes");
if (!string.IsNullOrWhiteSpace(memBytesText))
{
    if (!long.TryParse(memBytesText, out var memBytesValue) || memBytesValue <= 0)
    {
        Environment.ExitCode = PrintUsage("Invalid --mem-bytes. Must be a positive integer.");
        return;
    }

    memBytesOverride = memBytesValue;
}

var maxInflight = 1;
var maxInflightText = GetOptionOrNull(args, "--max-inflight");
if (!string.IsNullOrWhiteSpace(maxInflightText))
{
    if (!int.TryParse(maxInflightText, out maxInflight) || maxInflight <= 0)
    {
        Environment.ExitCode = PrintUsage("Invalid --max-inflight. Must be a positive integer.");
        return;
    }
}

var maxEntries = int.MaxValue;
var maxEntriesText = GetOptionOrNull(args, "--max-entries");
if (!string.IsNullOrWhiteSpace(maxEntriesText))
{
    if (!int.TryParse(maxEntriesText, out maxEntries) || maxEntries <= 0)
    {
        Environment.ExitCode = PrintUsage("Invalid --max-entries. Must be a positive integer.");
        return;
    }
}

var spillParallelism = 1;
var spillParallelismText = GetOptionOrNull(args, "--spill-parallelism");
if (!string.IsNullOrWhiteSpace(spillParallelismText))
{
    if (!int.TryParse(spillParallelismText, out spillParallelism) || spillParallelism <= 0)
    {
        Environment.ExitCode = PrintUsage("Invalid --spill-parallelism. Must be a positive integer.");
        return;
    }
}

var mergeFanIn = 128;
var mergeFanInText = GetOptionOrNull(args, "--merge-fanin");
if (!string.IsNullOrWhiteSpace(mergeFanInText))
{
    if (!int.TryParse(mergeFanInText, out mergeFanIn) || mergeFanIn <= 1)
    {
        Environment.ExitCode = PrintUsage("Invalid --merge-fanin. Must be an integer > 1.");
        return;
    }
}

var keepTempOnError = true;
var keepTempOnErrorText = GetOptionOrNull(args, "--keep-temp-on-error");
if (!string.IsNullOrWhiteSpace(keepTempOnErrorText))
{
    if (!bool.TryParse(keepTempOnErrorText, out keepTempOnError))
    {
        Environment.ExitCode = PrintUsage("Invalid --keep-temp-on-error. Use true or false.");
        return;
    }
}
else if (HasFlag(args, "--delete-temp-on-error"))
{
    keepTempOnError = false;
}

var options = new ExternalFileSorterOptions
{
    InputPath = inputPath,
    OutputPath = outputPath,
    TempDirectory = tempDir,
    MergeFanIn = mergeFanIn,
    MaxConcurrentSpills = spillParallelism,
    MemoryBudgetFraction = memFrac,
    TotalMemoryBudgetBytesOverride = memBytesOverride,
    MaxInFlightChunks = maxInflight,
    KeepTempFilesOnError = keepTempOnError,
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var sorter = new ExternalFileSorter();

try
{
    Console.WriteLine($"Input:  {options.InputPath}");
    Console.WriteLine($"Output: {options.OutputPath}");
    Console.WriteLine($"Temp:   {options.TempDirectory}");
    Console.WriteLine("Sorting...");

    var metrics = await sorter.SortWithMetricsAsync(options, cts.Token);

    Console.WriteLine($"Done. Wrote {metrics.OutputBytes} bytes.");
    Console.WriteLine($"Total time: {metrics.TotalTime}");
    Console.WriteLine($"Split time: {metrics.SplitPhaseTime}");
    Console.WriteLine($"Merge time: {metrics.MergePhaseTime}");
    Console.WriteLine($"Merge passes: {metrics.MergePasses}");
    Console.WriteLine($"Temp files: {metrics.TempFilesCount}");
    Console.WriteLine($"Input bytes: {metrics.InputBytes}");
    Console.WriteLine($"Temp bytes:  {metrics.TempBytes}");
    Console.WriteLine($"Approx I/O bytes: {metrics.ApproxTotalBytesTouched}");
    Environment.ExitCode = 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canceled.");
    Environment.ExitCode = 130;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed: {ex.Message}");
    Environment.ExitCode = 1;
}
