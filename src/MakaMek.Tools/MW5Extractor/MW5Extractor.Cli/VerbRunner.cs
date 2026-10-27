using MakaMek.Tools.MW5Extractor.Json;

namespace MakaMek.Tools.MW5Extractor.Cli;

/// <summary>Exit codes, mirroring the existing <c>MtfConverter</c>/<c>MmuxPackager</c> convention.</summary>
internal static class ExitCodes
{
    public const int Ok = 0;
    public const int GeneralError = 1;
    public const int BadArguments = 2;
    public const int InputMissing = 3;
    public const int ValidationFailed = 4;
}

/// <summary>Runs a verb and renders either the JSON envelope or human text, mapping failures to exit codes.</summary>
internal static class VerbRunner
{
    public static async Task<int> RunAsync(
        string command,
        bool json,
        TextWriter output,
        TextWriter error,
        Func<Task> action)
    {
        try
        {
            await action();
            return ExitCodes.Ok;
        }
        catch (Exception exception)
        {
            return RenderFailure(command, json, output, error, exception);
        }
    }

    public static void WriteJson<T>(TextWriter output, string command, T data, IReadOnlyList<string>? warnings = null)
    {
        output.WriteLine(JsonOutput.Envelope(command, data, warnings));
    }

    public static void WriteWarnings(TextWriter error, IReadOnlyList<string>? warnings)
    {
        foreach (var warning in warnings ?? [])
        {
            error.WriteLine($"warn: {warning}");
        }
    }

    private static int RenderFailure(string command, bool json, TextWriter output, TextWriter error, Exception exception)
    {
        var message = exception.Message;
        if (json)
        {
            output.WriteLine(JsonOutput.Envelope(command, [message]));
        }
        else
        {
            error.WriteLine($"error: {message}");
        }

        return exception switch
        {
            AmbiguousAssetException => ExitCodes.BadArguments,
            ArgumentException => ExitCodes.BadArguments,
            PakDirectoryNotFoundException => ExitCodes.InputMissing,
            AssetNotFoundException => ExitCodes.InputMissing,
            ExtractionFailedException => ExitCodes.ValidationFailed,
            _ => ExitCodes.GeneralError
        };
    }
}
