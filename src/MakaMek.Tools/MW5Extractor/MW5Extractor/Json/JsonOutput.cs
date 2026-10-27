using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakaMek.Tools.MW5Extractor.Json;

/// <summary>The machine-readable envelope every verb can emit (<c>--json</c>).</summary>
public sealed record JsonEnvelope<T>
{
    public required bool Ok { get; init; }
    public required string Command { get; init; }
    public T? Data { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public static class JsonOutput
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static string Envelope<T>(string command, T data, IReadOnlyList<string>? warnings = null) =>
        Serialize(new JsonEnvelope<T>
        {
            Ok = true,
            Command = command,
            Data = data,
            Warnings = warnings ?? []
        });

    public static string Failure(string command, IReadOnlyList<string> errors, IReadOnlyList<string>? warnings = null) =>
        Serialize(new JsonEnvelope<object>
        {
            Ok = false,
            Command = command,
            Warnings = warnings ?? [],
            Errors = errors
        });
}
