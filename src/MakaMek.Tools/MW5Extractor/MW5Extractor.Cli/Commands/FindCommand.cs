using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Discovery;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class FindCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var chassisArgument = new Argument<string>("chassis")
        {
            Description = "Chassis folder or variant id, e.g. Atlas or AS7-D."
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Write the chassis report to this path instead of only printing it."
        };

        var command = new Command("find", "Classify one chassis' assets into body, cockpit, glass, weapons, animations and data assets.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(outOption);
        command.Arguments.Add(chassisArgument);

        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("find", json, Console.Out, Console.Error, () =>
            {
                var chassis = parseResult.GetValue(chassisArgument);
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var report = ChassisDiscovery.Build(archive, chassis);
                var entry = SelectEntry(report.Chassis, chassis);

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "find", entry, report.Warnings);
                    return Task.CompletedTask;
                }

                WriteHuman(entry);
                VerbRunner.WriteWarnings(Console.Error, entry.Warnings);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);

                var outPath = parseResult.GetValue(outOption);
                if (!string.IsNullOrWhiteSpace(outPath))
                {
                    var path = Path.GetFullPath(outPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, Json.JsonOutput.Serialize(entry));
                    Console.WriteLine($"report    {path}");
                }

                return Task.CompletedTask;
            });
        });

        return command;
    }

    private static ChassisEntry SelectEntry(IReadOnlyList<ChassisEntry> chassis, string filter)
    {
        var byFolder = chassis.FirstOrDefault(entry =>
            entry.Folder.Equals(filter, StringComparison.OrdinalIgnoreCase));

        if (byFolder is not null)
        {
            return byFolder;
        }

        var byVariant = chassis.Where(entry => entry.Variants.Any(variant =>
            variant.Id.Equals(filter, StringComparison.OrdinalIgnoreCase))).ToList();

        if (byVariant.Count == 1)
        {
            return byVariant[0];
        }

        if (byVariant.Count > 1)
        {
            throw new AmbiguousAssetException($"variant '{filter}'", byVariant.Select(entry => entry.Folder).ToList());
        }

        if (chassis.Count == 1)
        {
            return chassis[0];
        }

        throw chassis.Count == 0
            ? new AssetNotFoundException($"chassis '{filter}'")
            : new AmbiguousAssetException($"chassis '{filter}'", chassis.Select(entry => entry.Folder).ToList());
    }

    private static void WriteHuman(ChassisEntry entry)
    {
        Console.WriteLine($"{entry.Folder}  roots={string.Join(", ", entry.ContentRoots)}");
        Console.WriteLine($"  layout      {entry.BodyLayout}");
        Console.WriteLine($"  body        {entry.Body ?? "-"}");
        if (entry.BodyParts.Count > 0)
        {
            Console.WriteLine($"  body parts  {entry.BodyParts.Count}");
            foreach (var part in entry.BodyParts.Take(10))
            {
                Console.WriteLine($"              {part}");
            }
        }

        if (entry.Counts.BodyDamage > 0)
        {
            Console.WriteLine($"  damage      {Number(entry.Counts.BodyDamage)} destroyed-state meshes");
        }

        Console.WriteLine($"  cockpit     {entry.Cockpit ?? "-"}");
        Console.WriteLine($"  glass       {entry.CockpitGlass ?? "-"}");
        Console.WriteLine($"  hardpoints  {entry.HardpointSet ?? "-"}");
        Console.WriteLine($"  model       {entry.ModelDefinition ?? "-"}");
        Console.WriteLine(
            $"  weapons     {Number(entry.Counts.Weapons)} assets, {Number(entry.Counts.WeaponMeshes)} meshes");
        foreach (var group in entry.Weapons
                     .Where(weapon => weapon.Suffix.Equals("SKM", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(weapon => weapon.Location ?? "?")
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"              {group.Key}: {string.Join(", ", group.Select(Hardpoint).Where(h => h.Length > 0))}");
        }

        Console.WriteLine($"  variants    {entry.Variants.Count}: {string.Join(", ", entry.Variants.Select(v => v.Id))}");
        Console.WriteLine($"  animations  {Number(entry.Counts.Animations)}");
        Console.WriteLine($"  skins       {Number(entry.Counts.Skins)}");
    }

    private static string Hardpoint(WeaponEntry weapon) =>
        weapon.Hardpoint is null ? weapon.Weapon ?? "?" : $"{weapon.Hardpoint}:{weapon.Weapon}";

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
