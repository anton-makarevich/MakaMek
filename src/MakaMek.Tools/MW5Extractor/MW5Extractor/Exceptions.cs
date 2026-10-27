namespace MakaMek.Tools.MW5Extractor;

/// <summary>The pak directory does not exist.</summary>
public sealed class PakDirectoryNotFoundException(string path)
    : Exception($"Pak directory '{path}' does not exist. Pass --paks or set {MountOptions.PaksDirectoryVariable}.")
{
    public string Path { get; } = path;
}

/// <summary>No asset in the mounted pak set matches the requested path or name.</summary>
public sealed class AssetNotFoundException(string path)
    : Exception($"No asset matches '{path}' in the mounted pak set.")
{
    public string Path { get; } = path;
}

/// <summary>A short asset name matched more than one asset.</summary>
public sealed class AmbiguousAssetException(string path, IReadOnlyList<string> candidates)
    : Exception($"'{path}' is ambiguous, {candidates.Count} assets match: {string.Join(", ", candidates.Take(5))}")
{
    public string Path { get; } = path;
    public IReadOnlyList<string> Candidates { get; } = candidates;
}

/// <summary>An asset was found but could not be exported (unsupported type, missing native codec, ...).</summary>
public sealed class ExtractionFailedException(string path, IReadOnlyList<string> errors)
    : Exception($"Export of '{path}' failed: {string.Join("; ", errors)}")
{
    public string Path { get; } = path;
    public IReadOnlyList<string> Errors { get; } = errors;
}
