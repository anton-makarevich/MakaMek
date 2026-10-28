using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Animation;
using MakaMek.Tools.MW5Extractor.Json;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class ClipsCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var chassisArgument = new Argument<string>("chassis")
        {
            Description = "Chassis folder or variant id, e.g. Atlas or AS7-D."
        };

        var probeOption = new Option<bool>("--probe")
        {
            Description = "Decode every available clip (frames, fps, duration, animated bones, root travel, sampled foot contacts, loop closure)."
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Write the JSON report here (default: artifacts/mw5/clips/<chassis>.json)."
        };

        var clipMapOption = new Option<string?>("--clip-map")
        {
            Description = "Clip alias map (default: clip-map.json next to the tool)."
        };

        var shippedFramesOption = new Option<int>("--shipped-frames")
        {
            Description = "Frames the render preset ships per clip; used for the sampled foot probe.",
            DefaultValueFactory = _ => 10
        };

        var command = new Command(
            "clips",
            "Map a chassis' MW5 animation assets onto the canonical clip vocabulary (Idle, Walk, Run, …).");

        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(probeOption);
        command.Options.Add(outOption);
        command.Options.Add(clipMapOption);
        command.Options.Add(shippedFramesOption);
        command.Arguments.Add(chassisArgument);

        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("clips", json, Console.Out, Console.Error, () =>
            {
                var chassis = parseResult.GetValue(chassisArgument)!;
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var vocabulary = ClipVocabulary.Load(parseResult.GetValue(clipMapOption));
                var report = ClipInventory.Build(archive, chassis, vocabulary);
                if (parseResult.GetValue(probeOption))
                {
                    report = ClipInventory.Probe(archive, report, vocabulary,
                        parseResult.GetValue(shippedFramesOption));
                }

                var outPath = ArtifactPaths.Resolve(
                    parseResult.GetValue(outOption), Path.Combine("clips", $"{report.Variant ?? report.Chassis}.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllText(outPath, JsonOutput.Serialize(report));

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "clips", report, report.Warnings);
                    return Task.CompletedTask;
                }

                WriteHuman(report, outPath);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);
                VerbRunner.WriteWarnings(Console.Error, archive.Warnings);
                return Task.CompletedTask;
            });
        });

        return command;
    }

    private static void WriteHuman(ClipInventoryReport report, string reportPath)
    {
        Console.WriteLine($"{report.Chassis}  clips={report.Clips.Count(c => c.Status == "available")}");
        Console.WriteLine(
            $"  {"clip",-11} {"status",-9} {"frames",7} {"fps",6} {"secs",7} {"bones",6} {"root m",7}  asset");

        foreach (var clip in report.Clips)
        {
            var metrics = clip.Status is "available" or "error"
                ? $"{clip.SampledFrames,7} {clip.Fps,6:0.##} {clip.DurationSeconds,7:0.00} {clip.AnimatedBones,6} {clip.RootMotionMeters,7:0.000}"
                : $"{'-',7} {"-",6} {"-",7} {"-",6} {"-",7}";

            Console.WriteLine(
                $"  {clip.Name,-11} {clip.Status,-9} {metrics}  {Path.GetFileName(clip.AssetPath) ?? "-"}");

            foreach (var warning in clip.Warnings)
            {
                Console.WriteLine($"  {"",11} warn: {warning}");
            }

            if (clip.FootGait is { } gait)
            {
                Console.WriteLine(
                    $"  {"",11} feet({gait.Frames} shipped): corrected stride " +
                    $"{gait.CorrectedStrideMeters:0.000} m (root {clip.RootEndpointDisplacementMeters:0.000} m)");
                foreach (var contact in gait.Contacts)
                {
                    Console.WriteLine(
                        $"  {"",11}   {contact.Foot,-5} {contact.Bone} {contact.StartSeconds:0.000}s " +
                        $"root {contact.RootTravelMeters:0.000} m " +
                        $"foot {contact.FootTravelMeters:0.000} m ({contact.SampleCount} frames)");
                }
            }
        }

        foreach (var absent in report.Absent)
        {
            Console.WriteLine($"  {absent.Name,-11} {"absent",-9} {"",-37}  {absent.Reason}");
        }

        if (report.Ignored.Count > 0)
        {
            Console.WriteLine($"  ignored ({report.Ignored.Count}): {string.Join(", ", report.Ignored.Take(8))}" +
                              (report.Ignored.Count > 8 ? ", …" : string.Empty));
        }

        Console.WriteLine($"  report {reportPath}");
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
