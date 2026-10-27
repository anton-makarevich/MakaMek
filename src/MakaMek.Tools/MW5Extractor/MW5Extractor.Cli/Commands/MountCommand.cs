using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Json;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class MountCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Where to write the report (default: artifacts/mw5/mount-report.json)."
        };

        var command = new Command("mount", "Mount the pak set and report archives, engine version, file counts and content roots.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(outOption);
        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("mount", json, Console.Out, Console.Error, async () =>
            {
                var stopwatch = Stopwatch.StartNew();
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));
                stopwatch.Stop();

                var report = MountReportBuilder.Build(archive, stopwatch.ElapsedMilliseconds);
                var path = ArtifactPaths.Resolve(parseResult.GetValue(outOption), "mount-report.json");
                WriteReport(path, report);

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "mount", report, report.Warnings);
                    return;
                }

                WriteHuman(report, path);
                VerbRunner.WriteWarnings(Console.Error, report.Warnings);
            });
        });

        return command;
    }

    private static void WriteReport(string path, MountReport report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonOutput.Serialize(report));
    }

    private static void WriteHuman(MountReport report, string reportPath)
    {
        Console.WriteLine($"paks      {report.PaksDirectory} ({report.Game})");
        Console.WriteLine($"archives  {report.Archives.Count}");
        foreach (var archive in report.Archives)
        {
            Console.WriteLine(
                $"  {archive.Name}  {Bytes(archive.LengthBytes)}  files={Number(archive.FileCount)}" +
                $"  encrypted={archive.Encrypted}" +
                (archive.CompressionMethods.Count == 0 ? string.Empty : $"  compression={string.Join(",", archive.CompressionMethods)}"));
        }

        Console.WriteLine($"files     {Number(report.FileCount)}  ({Bytes(report.TotalBytes)})");
        Console.WriteLine($"natives   oodle={report.Natives.Oodle}  zlib={report.Natives.Zlib}");
        Console.WriteLine("roots");
        foreach (var root in report.ContentRoots.Take(10))
        {
            Console.WriteLine($"  {root.Root}  files={Number(root.FileCount)}");
        }

        if (report.PendingKeys.Count > 0)
        {
            Console.WriteLine($"keys      {string.Join(", ", report.PendingKeys)}");
        }

        Console.WriteLine($"elapsed   {report.ElapsedMilliseconds} ms");
        Console.WriteLine($"report    {reportPath}");
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Bytes(long value) => value switch
    {
        >= 1024L * 1024 * 1024 => (value / (1024d * 1024 * 1024)).ToString("N2", CultureInfo.InvariantCulture) + " GiB",
        >= 1024L * 1024 => (value / (1024d * 1024)).ToString("N2", CultureInfo.InvariantCulture) + " MiB",
        >= 1024 => (value / 1024d).ToString("N2", CultureInfo.InvariantCulture) + " KiB",
        _ => value.ToString(CultureInfo.InvariantCulture) + " B"
    };
}
