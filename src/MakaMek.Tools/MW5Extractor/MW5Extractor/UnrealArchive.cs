using CUE4Parse.Encryption.Aes;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using CUE4Parse.UE4.VirtualFileSystem;

namespace MakaMek.Tools.MW5Extractor;

/// <summary>
/// A mounted unreal pak set: the cheapest possible read-only view of the game's cooked assets.
/// </summary>
/// <remarks>
/// Enumerating <see cref="Files"/> only walks pak indexes; nothing is deserialised until
/// <see cref="LoadObject"/> is called. Batch tooling depends on that difference.
/// </remarks>
public sealed class UnrealArchive : IDisposable
{
    private const string PackageExtension = ".uasset";

    private readonly DefaultFileProvider _provider;

    private UnrealArchive(DefaultFileProvider provider, MountOptions options, NativeSupport natives, IReadOnlyList<string> warnings)
    {
        _provider = provider;
        Options = options;
        Natives = natives;
        Warnings = warnings;
    }

    public MountOptions Options { get; }
    public NativeSupport Natives { get; }
    public IReadOnlyList<string> Warnings { get; }
    public int FileCount => _provider.Files.Count;
    public IEnumerable<GameFile> Files => _provider.Files.Values;
    public IReadOnlyCollection<IAesVfsReader> Archives => _provider.MountedVfs;

    /// <summary>Key GUIDs still required by encrypted archives that could not be mounted.</summary>
    public IReadOnlyList<string> PendingKeys => _provider.RequiredKeys
        .Select(key => key.ToString())
        .OrderBy(key => key, StringComparer.Ordinal)
        .ToList();

    /// <summary>Mounts every pak in the configured directories. Fails when the pak directory does not exist.</summary>
    public static UnrealArchive Open(MountOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PaksDirectory))
        {
            throw new ArgumentException(
                $"The pak directory is required. Pass --paks or set {MountOptions.PaksDirectoryVariable}.",
                nameof(options));
        }

        var paksDirectory = Path.GetFullPath(options.PaksDirectory);
        if (!Directory.Exists(paksDirectory))
        {
            throw new PakDirectoryNotFoundException(paksDirectory);
        }

        var warnings = new List<string>();
        var natives = NativeSupport.Initialize(options, warnings);

        var extraDirectories = options.ExtraDirectories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => new DirectoryInfo(Path.GetFullPath(d)))
            .ToArray();

        foreach (var extra in extraDirectories.Where(d => !d.Exists))
        {
            warnings.Add($"Extra pak directory '{extra.FullName}' does not exist and was skipped.");
        }

        var provider = new DefaultFileProvider(
            new DirectoryInfo(paksDirectory),
            extraDirectories.Where(d => d.Exists).ToArray(),
            SearchOption.TopDirectoryOnly,
            new VersionContainer(options.Game),
            StringComparer.OrdinalIgnoreCase);

        provider.Initialize();
        SubmitKey(provider, options.AesKey, warnings);
        provider.Mount();

        if (provider.RequiredKeys.Count > 0)
        {
            warnings.Add(
                $"{provider.RequiredKeys.Count} archive(s) are encrypted and were not mounted. " +
                $"--aes or {MountOptions.AesKeyVariable} is required to read them.");
        }

        return new UnrealArchive(provider, options with { PaksDirectory = paksDirectory }, natives, warnings);
    }

    /// <summary>Returns the single asset matching a virtual path, a path without extension, or a bare asset name.</summary>
    public GameFile? FindFile(string pathOrName)
    {
        var needle = NormalizeInput(pathOrName);
        if (needle.Length == 0)
        {
            throw new ArgumentException("An asset path or name is required.", nameof(pathOrName));
        }

        if (_provider.TryGetGameFile(needle, out var file) ||
            _provider.TryGetGameFile(needle + PackageExtension, out file))
        {
            return file;
        }

        var candidates = _provider.Files.Values
            .Where(file => Matches(file, needle))
            .Select(file => file.Path)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return candidates.Count switch
        {
            0 => null,
            1 => _provider.Files[candidates[0]],
            _ => throw new AmbiguousAssetException(pathOrName, candidates)
        };
    }

    public IPackage LoadPackage(string pathOrName)
    {
        var file = FindFile(pathOrName) ?? throw new AssetNotFoundException(pathOrName);
        return _provider.LoadPackage(file);
    }

    public UObject LoadObject(string pathOrName)
    {
        var file = FindFile(pathOrName) ?? throw new AssetNotFoundException(pathOrName);

        // Blueprints and other multi-export packages do not always resolve by the generic UObject overload.
        var direct = _provider.SafeLoadPackageObject(file.PathWithoutExtension);
        if (direct is not null)
        {
            return direct;
        }

        var exports = _provider.LoadPackage(file.PathWithoutExtension).GetExports().ToList();
        return exports.FirstOrDefault(export => export.Name == file.NameWithoutExtension)
               ?? exports.FirstOrDefault()
               ?? throw new AssetNotFoundException(pathOrName);
    }

    public T LoadObject<T>(string pathOrName) where T : UObject
    {
        var file = FindFile(pathOrName) ?? throw new AssetNotFoundException(pathOrName);
        return _provider.LoadPackageObject<T>(file.PathWithoutExtension)
               ?? throw new AssetNotFoundException(pathOrName);
    }

    /// <summary>All assets below a virtual folder, e.g. <c>MW5Mercs/Content/Objects/Mechs/Atlas/</c>.</summary>
    public IEnumerable<GameFile> Enumerate(string folderPrefix)
    {
        var prefix = NormalizeInput(folderPrefix) + "/";
        return _provider.Files.Values
            .Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.Ordinal);
    }

    public void Dispose() => _provider.Dispose();

    private static bool Matches(GameFile file, string needle)
    {
        // Every asset ships .uasset + .uexp (+ .ubulk) sidecars that share a name; only the package itself counts.
        if (!file.IsUePackage)
        {
            return false;
        }

        return file.PathWithoutExtension.Equals(needle, StringComparison.OrdinalIgnoreCase) ||
               file.NameWithoutExtension.Equals(needle, StringComparison.OrdinalIgnoreCase) ||
               file.PathWithoutExtension.EndsWith("/" + needle, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeInput(string value)
    {
        var normalized = value.Replace('\\', '/').Trim();
        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        return normalized.EndsWith(PackageExtension, StringComparison.OrdinalIgnoreCase)
            ? normalized[..^PackageExtension.Length]
            : normalized;
    }

    private static void SubmitKey(DefaultFileProvider provider, string? aesKey, IList<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(aesKey))
        {
            return;
        }

        try
        {
            var hex = aesKey.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[2..];
            }

            provider.SubmitKey(new FGuid(), new FAesKey(hex));
        }
        catch (Exception exception)
        {
            warnings.Add($"The AES key could not be applied: {exception.Message}");
        }
    }
}
