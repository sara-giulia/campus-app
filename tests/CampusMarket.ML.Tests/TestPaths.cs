namespace CampusMarket.ML.Tests;

// Walks up from the test assembly's bin folder to the solution root and then
// resolves ml/. Lets the tests find ml/data/listings.csv and ml/models/*.zip
// without copying them into the output folder.
internal static class TestPaths
{
    public static string MlDirectory { get; } = ResolveMlDirectory();

    private static string ResolveMlDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "ml")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
            throw new InvalidOperationException(
                $"Could not locate the ml/ directory starting from {AppContext.BaseDirectory}");

        return Path.Combine(dir.FullName, "ml");
    }
}
