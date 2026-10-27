using System.CommandLine;
using System.Globalization;
using MakaMek.Tools.MW5Extractor.Extraction;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class ExtractCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var assetArgument = new Argument<string>("asset")
        {
            Description = "Asset path, path without extension, or a unique asset name."
        };

        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "Output format: gltf, psk or json (default: gltf).",
            DefaultValueFactory = _ => "gltf"
        };
        formatOption.AcceptOnlyFromAmong("gltf", "psk", "json");

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Output directory (default: artifacts/mw5/extract/<asset>)."
        };

        var command = new Command("extract", "Export one asset to disk (glTF/PSK/properties) for eyeballing or rendering.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(formatOption);
        command.Options.Add(outOption);
        command.Arguments.Add(assetArgument);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("extract", json, Console.Out, Console.Error, async () =>
            {
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var assetPath = parseResult.GetValue(assetArgument)!;
                var format = ParseFormat(parseResult.GetValue(formatOption));
                var assetName = Path.GetFileNameWithoutExtension(assetPath.Replace('\\', '/'));
                var output = ArtifactPaths.Resolve(parseResult.GetValue(outOption), Path.Combine("extract", assetName));

                var result = await AssetExtractor.ExportAsync(
                    archive,
                    new ExtractRequest { AssetPath = assetPath, Format = format, OutputDirectory = output },
                    cancellationToken);

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "extract", result, result.Warnings);
                    return;
                }

                Console.WriteLine($"{result.Format} → {result.OutputDirectory}");
                foreach (var file in result.Files)
                {
                    Console.WriteLine($"  {file}  ({new FileInfo(file).Length.ToString("N0", CultureInfo.InvariantCulture)} B)");
                }

                Console.WriteLine($"  elapsed {result.ElapsedMilliseconds} ms");
                VerbRunner.WriteWarnings(Console.Error, result.Warnings);
                VerbRunner.WriteWarnings(Console.Error, archive.Warnings);
            });
        });

        return command;
    }

    private static ExtractFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
    {
        "psk" => ExtractFormat.Psk,
        "json" => ExtractFormat.Json,
        _ => ExtractFormat.Gltf
    };
}
