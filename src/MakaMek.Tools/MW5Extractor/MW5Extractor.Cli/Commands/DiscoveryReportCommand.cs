using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Discovery;
using MakaMek.Tools.MW5Extractor.Json;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class DiscoveryReportCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var chassisOption = new Option<string?>("--chassis", "-c")
        {
            Description = "Limit the report to one chassis folder or variant id (default: every chassis)."
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Where to write the report (default: artifacts/mw5/discovery-report.json)."
        };

        var command = new Command("discovery-report", "Classify every mech asset in the pak set into one reviewable JSON file.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(chassisOption);
        command.Options.Add(outOption);

        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("discovery-report", json, Console.Out, Console.Error, () =>
            {
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var report = ChassisDiscovery.Build(archive, parseResult.GetValue(chassisOption));
                var path = ArtifactPaths.Resolve(parseResult.GetValue(outOption), "discovery-report.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonOutput.Serialize(report));

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "discovery-report", report, report.Warnings);
                    return Task.CompletedTask;
                }

                WriteHuman(report, path);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);
                return Task.CompletedTask;
            });
        });

        return command;
    }

    private static void WriteHuman(DiscoveryReport report, string path)
    {
        var summary = report.Summary;
        Console.WriteLine($"report    {path}");
        Console.WriteLine(
            $"chassis   {Number(summary.ChassisCount)}  cockpit={Number(summary.WithCockpit)}  " +
            $"glass={Number(summary.WithCockpitGlass)}  weapons={Number(summary.WithWeapons)}  " +
            $"hardpoint sets={Number(summary.WithHardpointSet)}  modular={Number(summary.ModularBody)}");
        Console.WriteLine(
            $"assets    variants={Number(summary.VariantCount)}  weapon meshes={Number(summary.WeaponMeshCount)}  " +
            $"animations={Number(summary.AnimationCount)}");

        if (summary.ChassisWithoutCockpit.Count > 0)
        {
            Console.WriteLine($"no cockpit      {string.Join(", ", summary.ChassisWithoutCockpit)}");
        }

        if (summary.ChassisWithoutCockpitGlass.Count > 0)
        {
            Console.WriteLine($"no glass        {string.Join(", ", summary.ChassisWithoutCockpitGlass)}");
        }

        if (summary.ChassisWithoutHardpointSet.Count > 0)
        {
            Console.WriteLine($"no hardpoints   {string.Join(", ", summary.ChassisWithoutHardpointSet)}");
        }

        if (summary.ChassisWithoutVariants.Count > 0)
        {
            Console.WriteLine($"no variants     {string.Join(", ", summary.ChassisWithoutVariants)}");
        }

        if (report.OtherFolders.Count > 0)
        {
            Console.WriteLine($"other folders   {string.Join(", ", report.OtherFolders.Select(folder => folder.Folder))}");
        }

        foreach (var check in report.AssumptionChecks)
        {
            Console.WriteLine($"assumption [{check.Status}] {check.Assumption}");
            Console.WriteLine($"           {check.Evidence}");
        }
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
