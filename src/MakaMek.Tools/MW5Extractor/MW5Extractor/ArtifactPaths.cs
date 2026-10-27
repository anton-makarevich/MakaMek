namespace MakaMek.Tools.MW5Extractor;

/// <summary>
/// Where the extractor writes. Everything MW5-derived stays under gitignored <c>artifacts/mw5</c>;
/// nothing the tool produces may be committed or published.
/// </summary>
public static class ArtifactPaths
{
    private const string RepositoryMarker = "MakaMek.slnx";
    private const int MaxParentLevels = 8;

    /// <summary>Repository root, found by walking up from the current directory to the solution marker.</summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Default output root: <c>&lt;repo&gt;/artifacts/mw5</c>.</summary>
    public static string DefaultRoot => Path.Combine(RepositoryRoot, "artifacts", "mw5");

    /// <summary>Resolves an explicit output path, or the default root joined with <paramref name="relativePath"/>.</summary>
    public static string Resolve(string? path, string relativePath) =>
        string.IsNullOrWhiteSpace(path)
            ? Path.Combine(DefaultRoot, relativePath)
            : Path.GetFullPath(path);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var level = 0; level < MaxParentLevels && directory is not null; level++)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryMarker)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
