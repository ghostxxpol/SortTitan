namespace SortTitan.Core;

public sealed class TempRunFileProvider
{
    private readonly string _directory;
    private readonly string _prefix;

    public TempRunFileProvider(string directory, string prefix)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Temp directory is required.", nameof(directory));
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Run file prefix is required.", nameof(prefix));
        }

        _directory = directory;
        _prefix = prefix;
    }

    public string GetRunFilePath(int index)
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, $"{_prefix}_{index:D6}.txt");
    }
}
