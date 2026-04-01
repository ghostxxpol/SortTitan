using System.Diagnostics;
using SortTitan.Core;

static int PrintUsage(string? error = null)
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine();
    }

    Console.WriteLine("SortTitan.Generator");
    Console.WriteLine("Generates a test file with lines in format: <Number>. <String>");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  SortTitan.Generator --output <path> --size <bytes> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --output <path>          Output file path (required)");
    Console.WriteLine("  --size <bytes>           Approx target size in bytes (required)");
    Console.WriteLine("  --seed <int>             Random seed (optional)");
    Console.WriteLine("  --min <long>             Min number (default: 1)");
    Console.WriteLine("  --max <long>             Max number (default: 1000000)");
    Console.WriteLine("  --text-pool <int>        Unique text pool size (default: 1000)");
    Console.WriteLine("  --repeat <double>        Repeat probability 0..1 (default: 0.90)");
    Console.WriteLine("  --newline <lf|crlf>      New line (default: lf)");
    Console.WriteLine("  -h | --help              Show help");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine(@"  dotnet run --project SortTitan.Generator -- --output ""data.txt"" --size 104857600 --seed 123 --text-pool 5000 --repeat 0.95");

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

var outputPath = GetOptionOrNull(args, "--output");
var sizeText = GetOptionOrNull(args, "--size");
if (string.IsNullOrWhiteSpace(outputPath) || string.IsNullOrWhiteSpace(sizeText))
{
    Environment.ExitCode = PrintUsage("Missing required arguments: --output and/or --size.");
    return;
}

if (!long.TryParse(sizeText, out var targetSizeBytes) || targetSizeBytes <= 0)
{
    Environment.ExitCode = PrintUsage("Invalid --size. Must be a positive integer (bytes).");
    return;
}

int? seed = null;
var seedText = GetOptionOrNull(args, "--seed");
if (!string.IsNullOrWhiteSpace(seedText))
{
    if (!int.TryParse(seedText, out var seedValue))
    {
        Environment.ExitCode = PrintUsage("Invalid --seed. Must be an integer.");
        return;
    }

    seed = seedValue;
}

var options = new GeneratorOptions
{
    OutputPath = outputPath,
    TargetSizeBytes = targetSizeBytes,
    Seed = seed,
};

var minText = GetOptionOrNull(args, "--min");
if (!string.IsNullOrWhiteSpace(minText))
{
    if (!long.TryParse(minText, out var minValue))
    {
        Environment.ExitCode = PrintUsage("Invalid --min. Must be a long integer.");
        return;
    }

    options = options with { NumberMin = minValue };
}

var maxText = GetOptionOrNull(args, "--max");
if (!string.IsNullOrWhiteSpace(maxText))
{
    if (!long.TryParse(maxText, out var maxValue))
    {
        Environment.ExitCode = PrintUsage("Invalid --max. Must be a long integer.");
        return;
    }

    options = options with { NumberMax = maxValue };
}

var textPoolText = GetOptionOrNull(args, "--text-pool");
if (!string.IsNullOrWhiteSpace(textPoolText))
{
    if (!int.TryParse(textPoolText, out var poolSizeValue) || poolSizeValue <= 0)
    {
        Environment.ExitCode = PrintUsage("Invalid --text-pool. Must be a positive integer.");
        return;
    }

    options = options with { TextPoolSize = poolSizeValue };
}

var repeatText = GetOptionOrNull(args, "--repeat");
if (!string.IsNullOrWhiteSpace(repeatText))
{
    if (!double.TryParse(repeatText, out var repeatValue))
    {
        Environment.ExitCode = PrintUsage("Invalid --repeat. Must be a number in range 0..1.");
        return;
    }

    options = options with { RepeatProbability = repeatValue };
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var generator = new TestFileGenerator();
var stopwatch = Stopwatch.StartNew();

try
{
    Console.WriteLine($"Output: {options.OutputPath}");
    Console.WriteLine($"Target size: {options.TargetSizeBytes} bytes");
    Console.WriteLine("Generating...");

    await generator.GenerateAsync(options, cts.Token);

    stopwatch.Stop();
    var actualBytes = new FileInfo(options.OutputPath).Length;
    Console.WriteLine($"Done. Wrote {actualBytes} bytes in {stopwatch.Elapsed}.");
    Environment.ExitCode = 0;
}
catch (OperationCanceledException)
{
    stopwatch.Stop();
    Console.Error.WriteLine($"Canceled after {stopwatch.Elapsed}.");
    Environment.ExitCode = 130;
}
catch (Exception ex)
{
    stopwatch.Stop();
    Console.Error.WriteLine($"Failed after {stopwatch.Elapsed}: {ex.Message}");
    Environment.ExitCode = 1;
}
