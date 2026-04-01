namespace SortTitan.Core;

public sealed class TestFileGenerator : ITestFileGenerator
{
    public Task GenerateAsync(GeneratorOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        throw new NotImplementedException();
    }
}
