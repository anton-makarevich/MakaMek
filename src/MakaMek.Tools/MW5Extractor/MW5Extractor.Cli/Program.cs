using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor;
using MakaMek.Tools.MW5Extractor.Cli.Commands;

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

return await rootCommand.Parse(args).InvokeAsync();
