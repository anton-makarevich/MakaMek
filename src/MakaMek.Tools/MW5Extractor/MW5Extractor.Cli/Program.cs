using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor;
using MakaMek.Tools.MW5Extractor.Cli.Commands;
using MakaMek.Tools.MW5Extractor.Json;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var rootCommand = new RootCommand(
    "MechWarrior 5 asset discovery and extraction — headless, read-only on the game, writes only under artifacts/mw5.\n" +
    $"Set {MountOptions.PaksDirectoryVariable} or pass --paks to point at MW5Mercs\\Content\\Paks.");

rootCommand.Add(MountCommand.Create());
rootCommand.Add(LsCommand.Create());
rootCommand.Add(FindCommand.Create());
rootCommand.Add(InspectCommand.Create());
rootCommand.Add(ExtractCommand.Create());
rootCommand.Add(DiscoveryReportCommand.Create());
rootCommand.Add(AssembleCommand.Create());
rootCommand.Add(ClipsCommand.Create());

var parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count > 0)
{
    // System.CommandLine would print prose and exit 1; the CLI promises --json on every path.
    var errors = parseResult.Errors.Select(error => error.Message).ToList();
    if (args.Contains("--json", StringComparer.Ordinal))
    {
        Console.Out.WriteLine(JsonOutput.Failure("cli", errors));
    }
    else
    {
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"error: {error}");
        }
    }

    return 2;
}

return await parseResult.InvokeAsync();
