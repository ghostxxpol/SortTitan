using System.Text;

namespace SortTitan.Core;

public sealed record class GeneratorOptions
{
    public required string OutputPath { get; init; }
    public required long TargetSizeBytes { get; init; }

    public Encoding Encoding { get; init; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    public string NewLine { get; init; } = "\n";

    public long NumberMin { get; init; } = 1;
    public long NumberMax { get; init; } = 1_000_000;

    public int TextPoolSize { get; init; } = 1_000;
    public double RepeatProbability { get; init; } = 0.90;

    public int? Seed { get; init; }
}
