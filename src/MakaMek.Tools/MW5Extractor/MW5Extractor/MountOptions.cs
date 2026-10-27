using CUE4Parse.UE4.Versions;

namespace MakaMek.Tools.MW5Extractor;

/// <summary>
/// Everything needed to open a MechWarrior 5 pak set.
/// </summary>
/// <remarks>
/// The game path is never hard-coded or committed: it comes from an explicit argument or an environment
/// variable. MW5-derived data (extracted meshes, sprites) must stay in gitignored <c>artifacts/</c>.
/// </remarks>
public sealed record MountOptions
{
    public const string PaksDirectoryVariable = "MW5_PAKS_PATH";
    public const string AesKeyVariable = "MW5_AES_KEY";
    public const string OodleLibraryVariable = "MW5_OODLE_DLL";
    public const string ZlibLibraryVariable = "MW5_ZLIB_DLL";

    /// <summary>Directory holding the game paks (base game, DLC and mod paks can all live here).</summary>
    public required string PaksDirectory { get; init; }

    /// <summary>Additional pak directories scanned alongside <see cref="PaksDirectory"/> (e.g. <c>Saved\Paks</c> mods).</summary>
    public IReadOnlyList<string> ExtraDirectories { get; init; } = [];

    /// <summary>Hex AES key for encrypted archives; only mod paks normally need one.</summary>
    public string? AesKey { get; init; }

    /// <summary>Native Oodle library used for Oodle-compressed entries. Never downloaded implicitly.</summary>
    public string? OodleLibraryPath { get; init; }

    /// <summary>Native zlib-ng library used for zlib-compressed entries.</summary>
    public string? ZlibLibraryPath { get; init; }

    /// <summary>Engine version the pak set was cooked with. MW5 is UE 4.26.</summary>
    public EGame Game { get; init; } = EGame.GAME_UE4_26;

    /// <summary>Fills the unset values from the documented environment variables.</summary>
    public static MountOptions FromEnvironment()
    {
        return new MountOptions
        {
            PaksDirectory = Environment.GetEnvironmentVariable(PaksDirectoryVariable) ?? string.Empty,
            AesKey = NullIfBlank(Environment.GetEnvironmentVariable(AesKeyVariable)),
            OodleLibraryPath = NullIfBlank(Environment.GetEnvironmentVariable(OodleLibraryVariable)),
            ZlibLibraryPath = NullIfBlank(Environment.GetEnvironmentVariable(ZlibLibraryVariable))
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
