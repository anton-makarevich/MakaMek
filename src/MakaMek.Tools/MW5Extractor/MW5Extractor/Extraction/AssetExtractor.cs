using System.Diagnostics;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Exporters;
using CUE4Parse_Conversion.Options;

namespace MakaMek.Tools.MW5Extractor.Extraction;

/// <summary>Output formats the extractor can write.</summary>
public enum ExtractFormat
{
    /// <summary>glTF 2.0 (one <c>.glb</c> per mesh) — the hand-off to <c>tools/blender/render.py</c>.</summary>
    Gltf,

    /// <summary>ActorX <c>.psk</c> — the format the umodel/Blender workflow already understands.</summary>
    Psk,

    /// <summary>Raw deserialised properties as JSON; empty for MW5's custom cooked classes.</summary>
    Json
}

public sealed record ExtractRequest
{
    public required string AssetPath { get; init; }
    public required ExtractFormat Format { get; init; }
    public required string OutputDirectory { get; init; }
}

public sealed record ExtractResult
{
    public required string AssetPath { get; init; }
    public required string Format { get; init; }
    public required string OutputDirectory { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
    public required long ElapsedMilliseconds { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>One asset → files on disk, through CUE4Parse's exporters.</summary>
public static class AssetExtractor
{
    public static async Task<ExtractResult> ExportAsync(
        UnrealArchive archive,
        ExtractRequest request,
        CancellationToken cancellationToken = default)
    {
        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var (exportOptions, asProperties) = OptionsFor(request.Format);

        var session = new ExportSession();
        if (asProperties)
        {
            // A package can hold several exports (blueprints especially); dump them all rather than guessing.
            foreach (var export in archive.LoadPackage(request.AssetPath).GetExports())
            {
                session.Add(new JsonPropertiesExporter(export));
            }
        }
        else
        {
            session.Add(archive.LoadObject(request.AssetPath));
        }

        var stopwatch = Stopwatch.StartNew();
        var results = await session.RunAsync(outputDirectory, exportOptions, null, cancellationToken);
        stopwatch.Stop();

        var failures = results
            .Where(result => !result.Success)
            .Select(result => $"{result.ObjectPath}: {result.Error?.Message ?? "unknown error"}")
            .ToList();

        if (failures.Count > 0)
        {
            throw new ExtractionFailedException(request.AssetPath, failures);
        }

        var files = results
            .SelectMany(result => result.DiskFilePaths ?? [])
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var warnings = new List<string>();
        if (files.Count == 0)
        {
            warnings.Add("The exporter finished without writing any file.");
        }

        return new ExtractResult
        {
            AssetPath = request.AssetPath,
            Format = request.Format.ToString(),
            OutputDirectory = outputDirectory,
            Files = files,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            Warnings = warnings
        };
    }

    private static (ExportOptions Options, bool AsProperties) OptionsFor(ExtractFormat format) => format switch
    {
        ExtractFormat.Gltf => (new ExportOptions(
            EMeshFormat.Gltf2,
            socketFormat: ESocketFormat.Socket,
            exportMaterials: true,
            textureFormat: ETextureFormat.Png), false),
        ExtractFormat.Psk => (new ExportOptions(
            EMeshFormat.ActorX,
            socketFormat: ESocketFormat.Socket,
            exportMaterials: false), false),
        ExtractFormat.Json => (new ExportOptions(EMeshFormat.Gltf2, exportMaterials: false), true),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported extract format.")
    };
}
