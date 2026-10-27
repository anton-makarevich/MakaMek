using CUE4Parse.UE4.VirtualFileSystem;

namespace MakaMek.Tools.MW5Extractor.Reporting;

/// <summary>Result of <c>mount</c>: what the pak set contains before anything is loaded.</summary>
public sealed record MountReport
{
    public required string PaksDirectory { get; init; }
    public required string Game { get; init; }
    public required int FileCount { get; init; }
    public required long TotalBytes { get; init; }
    public required IReadOnlyList<ArchiveInfo> Archives { get; init; }
    public required IReadOnlyList<string> PendingKeys { get; init; }
    public required NativeSupport Natives { get; init; }
    public required IReadOnlyList<ContentRootInfo> ContentRoots { get; init; }
    public required long ElapsedMilliseconds { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record ArchiveInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required long LengthBytes { get; init; }
    public required int FileCount { get; init; }
    public required string MountPoint { get; init; }
    public required bool Encrypted { get; init; }
    public required string EncryptionKeyGuid { get; init; }
    public required IReadOnlyList<string> CompressionMethods { get; init; }
}

public sealed record ContentRootInfo
{
    public required string Root { get; init; }
    public required int FileCount { get; init; }
}

public static class MountReportBuilder
{
    public static MountReport Build(UnrealArchive archive, long elapsedMilliseconds)
    {
        var archives = archive.Archives
            .Select(archiveReader => Describe(archiveReader))
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ToList();

        var contentRoots = archive.Files
            .GroupBy(file => ContentRootOf(file.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ContentRootInfo { Root = group.Key, FileCount = group.Count() })
            .OrderByDescending(root => root.FileCount)
            .ThenBy(root => root.Root, StringComparer.Ordinal)
            .ToList();

        return new MountReport
        {
            PaksDirectory = archive.Options.PaksDirectory,
            Game = archive.Options.Game.ToString(),
            FileCount = archive.FileCount,
            TotalBytes = archive.Files.Sum(file => file.Size),
            Archives = archives,
            PendingKeys = archive.PendingKeys,
            Natives = archive.Natives,
            ContentRoots = contentRoots,
            ElapsedMilliseconds = elapsedMilliseconds,
            Warnings = archive.Warnings
        };
    }

    /// <summary>The content root of a virtual path: <c>MW5Mercs/Content</c>, <c>DLC3/Content</c>, …</summary>
    internal static string ContentRootOf(string virtualPath)
    {
        const string contentSegment = "/Content";
        var index = virtualPath.IndexOf(contentSegment + "/", StringComparison.OrdinalIgnoreCase);
        if (index > 0)
        {
            return virtualPath[..(index + contentSegment.Length)];
        }

        var slash = virtualPath.IndexOf('/');
        return slash > 0 ? virtualPath[..slash] : virtualPath;
    }

    private static ArchiveInfo Describe(IAesVfsReader reader)
    {
        return new ArchiveInfo
        {
            Name = reader.Name,
            Path = reader.Path,
            LengthBytes = reader.Length,
            FileCount = reader.FileCount,
            MountPoint = reader.MountPoint,
            Encrypted = reader.IsEncrypted,
            EncryptionKeyGuid = reader.EncryptionKeyGuid.ToString(),
            CompressionMethods = reader.CompressionMethods
                .Where(method => method != CUE4Parse.Compression.CompressionMethod.None)
                .Select(method => method.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList()
        };
    }
}
