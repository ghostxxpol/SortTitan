namespace SortTitan.Core;

public interface ITestFileGenerator
{
    Task GenerateAsync(GeneratorOptions options, CancellationToken cancellationToken = default);
}
