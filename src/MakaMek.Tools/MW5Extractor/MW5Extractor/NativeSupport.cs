using CUE4Parse.Compression;

namespace MakaMek.Tools.MW5Extractor;

/// <summary>
/// Availability of the native decompression libraries some pak entries need.
/// </summary>
/// <remarks>
/// CUE4Parse ships managed readers only; Oodle and zlib-ng are native. Its helpers will happily download them,
/// which the extractor deliberately never does — a read-only discovery tool must not reach the network.
/// Pass <c>--oodle</c>/<c>--zlib</c> (or the environment variables) to point at a library the machine already has.
/// </remarks>
public sealed record NativeSupport
{
    public required bool Oodle { get; init; }
    public required bool Zlib { get; init; }
    public string? OodlePath { get; init; }
    public string? ZlibPath { get; init; }

    internal static NativeSupport Initialize(MountOptions options, IList<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(options.OodleLibraryPath))
        {
            TryLoad(() => OodleHelper.Initialize(options.OodleLibraryPath), "Oodle", options.OodleLibraryPath, warnings);
        }

        if (!string.IsNullOrWhiteSpace(options.ZlibLibraryPath))
        {
            TryLoad(() => ZlibHelper.Initialize(options.ZlibLibraryPath), "zlib-ng", options.ZlibLibraryPath, warnings);
        }

        var oodle = OodleHelper.Instance is not null;
        var zlib = ZlibHelper.Instance is not null;

        if (!oodle)
        {
            warnings.Add(
                "Oodle native library is not loaded. Pak entries compressed with Oodle (some textures/bulk data) will fail; " +
                $"pass --oodle <path> or set {MountOptions.OodleLibraryVariable}.");
        }

        if (!zlib)
        {
            warnings.Add(
                "zlib-ng native library is not loaded. Zlib-compressed entries may fail; " +
                $"pass --zlib <path> or set {MountOptions.ZlibLibraryVariable}.");
        }

        return new NativeSupport
        {
            Oodle = oodle,
            Zlib = zlib,
            OodlePath = options.OodleLibraryPath,
            ZlibPath = options.ZlibLibraryPath
        };
    }

    private static void TryLoad(Action load, string name, string? path, IList<string> warnings)
    {
        try
        {
            load();
        }
        catch (Exception exception)
        {
            warnings.Add($"Could not load the {name} native library from '{path}': {exception.Message}");
        }
    }
}
