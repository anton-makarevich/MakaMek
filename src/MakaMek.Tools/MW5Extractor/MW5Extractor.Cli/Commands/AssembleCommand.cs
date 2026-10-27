using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Assembly;
using MakaMek.Tools.MW5Extractor.Json;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class AssembleCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var chassisArgument = new Argument<string>("chassis")
        {
            Description = "Chassis folder or variant id, e.g. Atlas or AS7-D."
        };

        var variantOption = new Option<string?>("--variant", "-v")
        {
            Description = "Variant whose loadout defines the weapons (default: the first variant with a loadout)."
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Output directory (default: artifacts/mw5/assemble/<variant>)."
        };

        var skipCockpitOption = new Option<bool>("--skip-cockpit")
        {
            Description = "Leave out the cockpit mesh and cockpit glass."
        };

        var skipWeaponsOption = new Option<bool>("--skip-weapons")
        {
            Description = "Assemble the bare chassis only."
        };

        var clipsOption = new Option<string[]>("--clips")
        {
            Description = "Canonical clips to bake in (e.g. --clips Walk). Produces a skinned <variant>.animated.glb.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        var clipMapOption = new Option<string?>("--clip-map")
        {
            Description = "Clip alias map (default: clip-map.json next to the tool)."
        };

        var command = new Command("assemble", "Merge body + cockpit + glass + loadout weapons into one renderable glTF model.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(variantOption);
        command.Options.Add(outOption);
        command.Options.Add(skipCockpitOption);
        command.Options.Add(skipWeaponsOption);
        command.Options.Add(clipsOption);
        command.Options.Add(clipMapOption);
        command.Arguments.Add(chassisArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("assemble", json, Console.Out, Console.Error, async () =>
            {
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var chassis = parseResult.GetValue(chassisArgument)!;
                var variant = parseResult.GetValue(variantOption);
                var output = ArtifactPaths.Resolve(parseResult.GetValue(outOption), Path.Combine("assemble", variant ?? chassis));

                var report = await MechAssembler.AssembleAsync(
                    archive,
                    new AssemblyRequest
                    {
                        Chassis = chassis,
                        Variant = variant,
                        OutputDirectory = output,
                        SkipCockpit = parseResult.GetValue(skipCockpitOption),
                        SkipWeapons = parseResult.GetValue(skipWeaponsOption),
                        Clips = (parseResult.GetValue(clipsOption) ?? [])
                            .SelectMany(value => value.Split(
                                ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            .ToList(),
                        ClipMapPath = parseResult.GetValue(clipMapOption)
                    },
                    cancellationToken);

                var reportPath = Path.Combine(report.ModelPath[..report.ModelPath.LastIndexOf('.')] + ".assembly.json");
                File.WriteAllText(reportPath, JsonOutput.Serialize(report));

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "assemble", report, report.Warnings);
                    return;
                }

                WriteHuman(report, reportPath);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);
                VerbRunner.WriteWarnings(Console.Error, archive.Warnings);
            });
        });

        return command;
    }

    private static void WriteHuman(AssemblyReport report, string reportPath)
    {
        Console.WriteLine($"{report.Chassis} {report.Variant} → {report.ModelPath}");
        Console.WriteLine(
            $"  {Number(report.VertexCount)} vertices, {Number(report.TriangleCount)} triangles, " +
            $"{report.PlanViewExtentMeters:0.00} m plan-view span");
        Console.WriteLine(
            $"  bounds {report.Bounds.Extent.X * 2:0.00} × {report.Bounds.Extent.Y * 2:0.00} × " +
            $"{report.Bounds.Extent.Z * 2:0.00} m");

        foreach (var part in report.Parts)
        {
            var detail = part.Kind == "weapon"
                ? $" {part.Slot} → {part.Bone} ({part.WeaponTag})"
                : part.Bone is null ? string.Empty : $" bone={part.Bone}";
            Console.WriteLine(
                $"  {part.Kind,-9} {Number(part.VertexCount),8} v {Number(part.TriangleCount),8} t" +
                $"  {Path.GetFileName(part.AssetPath)}{detail}");
        }

        foreach (var clip in report.Animations)
        {
            Console.WriteLine(
                $"  clip      {clip.Name,-9} {clip.SampledFrames,4} frames @ {clip.Fps:0.##} fps " +
                $"({clip.DurationSeconds:0.00} s, {clip.AnimatedBones} bones, root {clip.RootMotionMeters:0.000} m)" +
                $"  {Path.GetFileName(clip.AssetPath)}");
        }

        if (report.Unmapped.Count > 0)
        {
            Console.WriteLine("  unmapped");
            foreach (var entry in report.Unmapped)
            {
                Console.WriteLine($"    {entry}");
            }
        }

        Console.WriteLine($"  report {reportPath}");
        if (report.ModelSpace != "canonical")
        {
            Console.WriteLine(
                $"  note: animated model stays in {report.ModelSpace} space; " +
                $"render with --model-yaw {report.ModelYawDegrees:0}");
        }
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
