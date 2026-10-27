using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Inspection;
using MakaMek.Tools.MW5Extractor.Json;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class InspectCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var assetArgument = new Argument<string>("asset")
        {
            Description = "Asset path, path without extension, or a unique asset name, e.g. Atlas_SKM."
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Where to write the inspect report (default: artifacts/mw5/inspect/<asset>.json)."
        };

        var command = new Command("inspect", "Read one asset: exports, bones, sockets with transforms, LODs, material slots, animations.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(outOption);
        command.Arguments.Add(assetArgument);

        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("inspect", json, Console.Out, Console.Error, () =>
            {
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var report = AssetInspector.Inspect(archive, parseResult.GetValue(assetArgument)!);
                var path = ArtifactPaths.Resolve(parseResult.GetValue(outOption), Path.Combine("inspect", $"{report.Name}.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonOutput.Serialize(report));

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "inspect", report, report.Warnings);
                    return Task.CompletedTask;
                }

                WriteHuman(report, path);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);
                VerbRunner.WriteWarnings(Console.Error, archive.Warnings);
                return Task.CompletedTask;
            });
        });

        return command;
    }

    private static void WriteHuman(Reporting.InspectReport report, string path)
    {
        Console.WriteLine(report.Path);
        Console.WriteLine(
            $"  exports   {report.Exports.Count}: " +
            string.Join(", ", report.Exports.GroupBy(export => export.Type)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}×{group.Count()}")));

        if (report.Exports.Count <= 4)
        {
            foreach (var export in report.Exports)
            {
                Console.WriteLine(
                    $"              {export.Type} ({export.Name}) props={export.PropertyCount}" +
                    (export.ParsedType == export.Type ? string.Empty : $" parsed-as={export.ParsedType}"));

                foreach (var property in export.Properties)
                {
                    Console.WriteLine(
                        $"                {property.Name} : {property.Type}" +
                        (property.Value is null ? string.Empty : $" = {property.Value}"));
                }
            }
        }

        if (report.Mesh is { } mesh)
        {
            Console.WriteLine(
                $"  lods      {mesh.Lods.Count}: " +
                string.Join(", ", mesh.Lods.Select(lod => $"{Number(lod.Vertices)}v/{Number(lod.Triangles)}t")));
            Console.WriteLine($"  bones     {mesh.Bones.Count}" +
                              (mesh.Bones.Count == 0 ? string.Empty : $": {string.Join(", ", mesh.Bones.Take(12))}…"));
            Console.WriteLine($"  sockets   {mesh.Sockets.Count}");
            foreach (var socket in mesh.Sockets)
            {
                Console.WriteLine($"              {socket.Name} bone={socket.Bone} loc=({socket.Translation.X:0.##},{socket.Translation.Y:0.##},{socket.Translation.Z:0.##})");
            }

            Console.WriteLine($"  materials {string.Join(", ", mesh.MaterialSlots.Select(slot => slot.Slot))}");
            Console.WriteLine($"  skeleton  {mesh.SkeletonAsset ?? "-"}");

            if (mesh.Bounds is { } bounds)
            {
                Console.WriteLine(
                    $"  bounds    origin=({bounds.Origin.X:0.##},{bounds.Origin.Y:0.##},{bounds.Origin.Z:0.##}) " +
                    $"extent=({bounds.Extent.X:0.##},{bounds.Extent.Y:0.##},{bounds.Extent.Z:0.##})");
            }
        }

        if (report.Skeleton is { } skeleton)
        {
            Console.WriteLine($"  skeleton  {skeleton.BoneCount} bones, {skeleton.Sockets.Count} sockets");
            foreach (var socket in skeleton.Sockets)
            {
                Console.WriteLine($"              {socket.Name} bone={socket.Bone}");
            }
        }

        if (report.Animation is { } animation)
        {
            Console.WriteLine(
                $"  animation {animation.LengthSeconds:0.###}s, {animation.Frames} frames, " +
                $"{animation.BoneTracks} bone tracks, skeleton={animation.SkeletonAsset ?? "-"}");
        }

        Console.WriteLine($"  report    {path}");
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
