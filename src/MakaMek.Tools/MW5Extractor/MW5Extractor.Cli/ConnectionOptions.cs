using System.CommandLine;
using CUE4Parse.UE4.Versions;

namespace MakaMek.Tools.MW5Extractor.Cli;

/// <summary>Options shared by every verb: how to reach the pak set, and how to report.</summary>
internal sealed class ConnectionOptions
{
    public Option<string?> Paks { get; } = new("--paks", "-p")
    {
        Description = $"Directory holding the MW5 paks. Defaults to ${MountOptions.PaksDirectoryVariable}."
    };

    public Option<string[]?> Extra { get; } = new("--extra", "-e")
    {
        Description = "Additional pak directories (DLC, Saved\\Paks mods). Repeatable."
    };

    public Option<string?> Aes { get; } = new("--aes")
    {
        Description = $"Hex AES key for encrypted paks. Defaults to ${MountOptions.AesKeyVariable}."
    };

    public Option<string?> Oodle { get; } = new("--oodle")
    {
        Description = $"Path to the native Oodle library. Defaults to ${MountOptions.OodleLibraryVariable}."
    };

    public Option<string?> Zlib { get; } = new("--zlib")
    {
        Description = $"Path to the native zlib-ng library. Defaults to ${MountOptions.ZlibLibraryVariable}."
    };

    public Option<string?> Game { get; } = new("--game")
    {
        Description = "Unreal engine version the paks were cooked with (default: UE4_26)."
    };

    public Option<bool> Json { get; } = new("--json")
    {
        Description = "Emit the machine-readable envelope instead of human text."
    };

    public IEnumerable<Option> All => [Paks, Extra, Aes, Oodle, Zlib, Game, Json];

    public bool IsJson(ParseResult parseResult) => parseResult.GetValue(Json);

    public MountOptions Resolve(ParseResult parseResult)
    {
        var defaults = MountOptions.FromEnvironment();
        var paks = parseResult.GetValue(Paks);

        return defaults with
        {
            PaksDirectory = string.IsNullOrWhiteSpace(paks) ? defaults.PaksDirectory : paks,
            ExtraDirectories = parseResult.GetValue(Extra) ?? defaults.ExtraDirectories,
            AesKey = parseResult.GetValue(Aes) ?? defaults.AesKey,
            OodleLibraryPath = parseResult.GetValue(Oodle) ?? defaults.OodleLibraryPath,
            ZlibLibraryPath = parseResult.GetValue(Zlib) ?? defaults.ZlibLibraryPath,
            Game = ParseGame(parseResult.GetValue(Game))
        };
    }

    private static EGame ParseGame(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EGame.GAME_UE4_26;
        }

        if (Enum.TryParse<EGame>(value, ignoreCase: true, out var game))
        {
            return game;
        }

        throw new ArgumentException($"'{value}' is not a known Unreal version. Example: UE4_26.", nameof(value));
    }
}
