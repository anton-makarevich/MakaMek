using System.CommandLine;
using System.Globalization;
using System.IO.Enumeration;

namespace MakaMek.Tools.MW5Extractor.Cli.Commands;

internal static class LsCommand
{
    public static Command Create()
    {
        var connection = new ConnectionOptions();
        var pathArgument = new Argument<string>("path")
        {
            Description = "Virtual folder or path prefix, e.g. MW5Mercs/Content/Objects/Mechs/Atlas"
        };

        var limitOption = new Option<int>("--limit", "-l")
        {
            Description = "Maximum number of paths printed (default: 200, 0 = no limit).",
            DefaultValueFactory = _ => 200
        };

        var patternOption = new Option<string?>("--pattern")
        {
            Description = "Glob filter applied to the file name, e.g. *_SKM.uasset."
        };

        var command = new Command("ls", "List virtual paths under a folder — the discovery index, without loading anything.");
        foreach (var option in connection.All)
        {
            command.Options.Add(option);
        }

        command.Options.Add(limitOption);
        command.Options.Add(patternOption);
        command.Arguments.Add(pathArgument);

        command.SetAction(async (parseResult, _) =>
        {
            var json = connection.IsJson(parseResult);
            return await VerbRunner.RunAsync("ls", json, Console.Out, Console.Error, () =>
            {
                using var archive = UnrealArchive.Open(connection.Resolve(parseResult));

                var prefix = NormalizePrefix(parseResult.GetValue(pathArgument)!);
                var pattern = parseResult.GetValue(patternOption);
                var limit = parseResult.GetValue(limitOption);

                var matches = archive.Files
                    .Select(file => file.Path)
                    .Where(path => MatchesPrefix(path, prefix))
                    .Where(path => pattern is null ||
                                   FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(path), ignoreCase: true))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();

                var returned = limit <= 0 ? matches : matches.Take(limit).ToList();

                if (json)
                {
                    VerbRunner.WriteJson(Console.Out, "ls", new
                    {
                        path = prefix,
                        pattern,
                        total = matches.Count,
                        returned = returned.Count,
                        files = returned
                    }, archive.Warnings);
                    return Task.CompletedTask;
                }

                foreach (var path in returned)
                {
                    Console.WriteLine(path);
                }

                Console.WriteLine(
                    $"-- {returned.Count.ToString("N0", CultureInfo.InvariantCulture)} of " +
                    $"{matches.Count.ToString("N0", CultureInfo.InvariantCulture)} path(s)");
                VerbRunner.WriteWarnings(Console.Error, archive.Warnings);
                return Task.CompletedTask;
            });
        });

        return command;
    }

    private static string NormalizePrefix(string value)
    {
        var normalized = value.Replace('\\', '/').Trim();
        while (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        return normalized.TrimEnd('/');
    }

    private static bool MatchesPrefix(string path, string prefix)
    {
        if (prefix.Length == 0)
        {
            return true;
        }

        return path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
               (!prefix.Contains('/') &&
                path.Contains("/" + prefix + "/", StringComparison.OrdinalIgnoreCase));
    }
}
