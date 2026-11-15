// Diff-scoped analyzer gate (issue #1714).
//
// Analyzes only the C# files changed against a base ref (default: origin/main) in the reusable
// library projects, and fails when any analyzer finding remains after applying the explicit
// allowlist in tools/analyzer-gate/exclusions.txt.
//
// The allowlist is deliberately tiny: it exists only for findings that cannot be fixed without
// changing a public contract that this repository's tests or views pin (for example the class
// coupling of a type that implements a large public interface). Every entry must name a reason.
// Everything else must be fixed or refactored - do not add #pragma warning disable or
// [SuppressMessage] to work around the gate.
//
// This is a .NET file-based app so the gate behaves identically in CI (Ubuntu) and on Windows,
// without depending on bash, jq or a specific shell.
//
// Usage:
//   mise run analyzer-gate
//   ANALYZER_BASE=origin/develop mise run analyzer-gate
//   mise run analyzer-gate -- src/path/File.cs
//
// Environment:
//   ANALYZER_BASE  git ref to diff against (default: origin/main)
//   ANALYZER_GATE_KEEP_REPORT  set to 1 to keep the report directories

using System.Diagnostics;
using System.Text.Json;

namespace AnalyzerGate;

internal static class Program
{
    private const string DefaultBase = "origin/main";
    private const string ExclusionsFile = "tools/analyzer-gate/exclusions.txt";

    private static readonly string[] AnalyzedProjectPrefixes =
    [
        "src/MakaMek.Core/",
        "src/MakaMek.Presentation/",
        "src/MakaMek.Map/",
        "src/MakaMek.Services/",
        "src/MakaMek.Assets/",
        "src/MakaMek.Localization/",
        "src/MakaMek.Bots/"
    ];

    private static int Main(string[] args)
    {
        var repoRoot = Capture("git", ["rev-parse", "--show-toplevel"], null).Trim();
        if (repoRoot.Length == 0)
        {
            return Fail("not inside a git repository");
        }

        var baseRef = ResolveBase(repoRoot);
        if (baseRef is null)
        {
            return 1;
        }

        Console.WriteLine($"Analyzer gate: diff against {baseRef}");

        var status = 0;
        var explicitFiles = new List<string>();
        var configChanged = false;
        foreach (var arg in args)
        {
            if (arg == "CodeMetricsConfig.txt")
            {
                configChanged = true;
            }
            else
            {
                explicitFiles.Add(NormalizePath(arg));
            }
        }

        List<string> changedFiles;
        if (explicitFiles.Count > 0)
        {
            changedFiles = explicitFiles;
        }
        else
        {
            configChanged = ChangedFiles(repoRoot, baseRef, "CodeMetricsConfig.txt").Count > 0;
            changedFiles = ChangedFiles(repoRoot, baseRef, "*.cs");
        }

        // CA1509 (invalid CodeMetricsConfig.txt entry) is reported on the config file, so it
        // cannot be filtered with `--include`; promote it to an error while validating.
        if (configChanged)
        {
            Console.WriteLine("CodeMetricsConfig.txt changed; validating CA1509...");
            var build = Run("dotnet", ["build", "src/MakaMek.Core/MakaMek.Core.csproj", "-warnaserror:CA1509", "-v:q"], repoRoot);
            if (build.ExitCode != 0)
            {
                Console.Error.WriteLine($"{build.StandardOutput}{build.StandardError}");
                status = 1;
            }
        }

        if (changedFiles.Count == 0)
        {
            Console.WriteLine($"No changed C# files to analyze (base: {baseRef}).");
            return status;
        }

        var projects = GroupByProject(repoRoot, changedFiles);
        if (projects is null)
        {
            return 1;
        }
        if (projects.Count == 0)
        {
            Console.WriteLine("No C# files to analyze.");
            return status;
        }

        var exclusions = ReadExclusions(repoRoot);
        var excluded = new List<Finding>();
        var failures = new List<Finding>();

        var workDirectory = CreateWorkDirectory();
        if (workDirectory is null)
        {
            return 1;
        }

        try
        {
            var index = 0;
            foreach (var (project, files) in projects)
            {
                index++;
                Console.WriteLine($"Analyzing changed file(s) in {project}:");
                foreach (var file in files)
                {
                    Console.WriteLine($"  {file}");
                }

                if (!AnalyzeProject(
                        repoRoot, project, files,
                        Path.Combine(workDirectory, $"report-{index}"),
                        out var findings))
                {
                    return 1;
                }

                foreach (var finding in findings)
                {
                    if (exclusions.IsExcluded(finding))
                    {
                        excluded.Add(finding);
                    }
                    else
                    {
                        failures.Add(finding);
                    }
                }
            }
        }
        finally
        {
            Cleanup(workDirectory);
        }

        if (excluded.Count > 0)
        {
            Console.WriteLine($"Excluded by {ExclusionsFile} ({excluded.Count}):");
            foreach (var finding in excluded)
            {
                Console.WriteLine($"  {finding.File}({finding.Line}): {finding.Rule} {finding.Message}");
            }
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("No analyzer findings outside the allowlist.");
            return status;
        }

        Console.WriteLine($"Analyzer findings outside the allowlist ({failures.Count}):");
        foreach (var finding in failures)
        {
            Console.WriteLine($"  {finding.File}({finding.Line}): {finding.Rule} {finding.Message}");
        }

        return 1;
    }

    private static string? ResolveBase(string repoRoot)
    {
        var requested = Environment.GetEnvironmentVariable("ANALYZER_BASE");
        var baseRef = string.IsNullOrWhiteSpace(requested) ? DefaultBase : requested;

        if (GitRefExists(repoRoot, baseRef))
        {
            return baseRef;
        }

        Console.Error.WriteLine($"Base '{baseRef}' not found, falling back to 'main'. Run 'git fetch' for an up-to-date diff.");
        baseRef = "main";
        if (GitRefExists(repoRoot, baseRef))
        {
            return baseRef;
        }

        Fail("neither the requested base nor 'main' exists. Run 'git fetch' or set ANALYZER_BASE.");
        return null;
    }

    private static bool GitRefExists(string repoRoot, string reference) =>
        Run("git", ["rev-parse", "--verify", "--quiet", reference], repoRoot).ExitCode == 0;

    private static List<string> ChangedFiles(string repoRoot, string baseRef, string pathSpec)
    {
        var output = new List<string>();
        output.AddRange(Lines(Capture("git", ["diff", "--name-only", "--diff-filter=ACMR", $"{baseRef}...HEAD", "--", pathSpec], repoRoot)));
        output.AddRange(Lines(Capture("git", ["diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--", pathSpec], repoRoot)));
        output.AddRange(Lines(Capture("git", ["ls-files", "--others", "--exclude-standard", "--", pathSpec], repoRoot)));
        return output.Select(NormalizePath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static SortedDictionary<string, List<string>>? GroupByProject(string repoRoot, List<string> files)
    {
        var projects = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!AnalyzedProjectPrefixes.Any(p => file.StartsWith(p, StringComparison.Ordinal)))
            {
                Console.Error.WriteLine($"Skipping non-library code: {file}");
                continue;
            }

            if (!File.Exists(Path.Combine(repoRoot, file)))
            {
                Console.Error.WriteLine($"Skipping missing file: {file}");
                continue;
            }

            var project = FindProject(repoRoot, file);
            if (project is null)
            {
                Fail($"could not determine a single project for {file}");
                return null;
            }

            if (!projects.TryGetValue(project, out var projectFiles))
            {
                projectFiles = [];
                projects[project] = projectFiles;
            }
            projectFiles.Add(file);
        }

        return projects;
    }

    private static string? FindProject(string repoRoot, string file)
    {
        var directory = Path.GetDirectoryName(file);
        while (!string.IsNullOrEmpty(directory))
        {
            var fullDirectory = Path.Combine(repoRoot, directory);
            var candidates = Directory.Exists(fullDirectory)
                ? Directory.GetFiles(fullDirectory, "*.csproj")
                : [];
            if (candidates.Length == 1)
            {
                return NormalizePath(Path.GetRelativePath(repoRoot, candidates[0]));
            }
            if (candidates.Length > 1)
            {
                return null;
            }
            directory = Path.GetDirectoryName(directory);
        }
        return null;
    }

    private static string? CreateWorkDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"makamek-analyzer-gate-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(path);
            return path;
        }
        catch (Exception exception)
        {
            Fail($"could not create a temporary directory: {exception.Message}");
            return null;
        }
    }

    private static bool AnalyzeProject(
        string repoRoot,
        string project,
        List<string> files,
        string reportDirectory,
        out List<Finding> findings)
    {
        findings = [];
        Directory.CreateDirectory(reportDirectory);

        List<string> arguments =
        [
            "format", "analyzers", project,
            "--verify-no-changes",
            "--severity", "warn",
            "--include"
        ];
        arguments.AddRange(files);
        arguments.AddRange(["--report", reportDirectory, "--verbosity", "quiet"]);

        var log = Path.Combine(reportDirectory, "analyzer.log");
        var result = Run("dotnet", arguments, repoRoot, log);
        if (result.ExitCode != 0 && result.ExitCode != 2)
        {
            Console.Error.WriteLine($"{result.StandardOutput}{result.StandardError}");
            Fail($"analyzer run failed for {project}");
            return false;
        }

        findings = ReadReport(Path.Combine(reportDirectory, "format-report.json"));
        return true;
    }

    private static List<Finding> ReadReport(string reportPath)
    {
        var findings = new List<Finding>();
        if (!File.Exists(reportPath))
        {
            return findings;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(reportPath));
        foreach (var sourceFile in document.RootElement.EnumerateArray())
        {
            var path = Relativize(sourceFile.GetProperty("FilePath").GetString() ?? string.Empty);
            if (!sourceFile.TryGetProperty("FileChanges", out var changes))
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                var rule = change.TryGetProperty("DiagnosticId", out var ruleElement)
                    ? ruleElement.GetString() ?? string.Empty
                    : string.Empty;
                var message = change.TryGetProperty("FormatDescription", out var messageElement)
                    ? messageElement.GetString() ?? string.Empty
                    : string.Empty;
                // FormatDescription is prefixed with "warning <rule>: "; drop that.
                var prefix = $"warning {rule}: ";
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                {
                    message = message[prefix.Length..];
                }

                findings.Add(new Finding(
                    path,
                    change.TryGetProperty("LineNumber", out var line) ? line.GetInt32() : 0,
                    rule,
                    message));
            }
        }
        return findings;
    }

    private static Exclusions ReadExclusions(string repoRoot)
    {
        var entries = new HashSet<(string File, string Rule)>();
        var path = Path.Combine(repoRoot, ExclusionsFile);
        if (!File.Exists(path))
        {
            return new Exclusions(entries);
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var columns = line.Split(':', 2);
            if (columns.Length != 2 || columns[0].Length == 0 || columns[1].Length == 0)
            {
                Console.Error.WriteLine($"WARNING: ignoring invalid exclusion line '{line}' in {ExclusionsFile}");
                continue;
            }

            entries.Add((NormalizePath(columns[0].Trim()), columns[1].Trim()));
        }

        return new Exclusions(entries);
    }

    private static string Relativize(string path)
    {
        var normalized = path.Replace('\\', '/');
        var marker = normalized.LastIndexOf("/src/", StringComparison.Ordinal);
        return marker >= 0 ? normalized[(marker + 1)..] : normalized;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Capture(string file, IReadOnlyList<string> arguments, string? workingDirectory) =>
        Run(file, arguments, workingDirectory).StandardOutput;

    private static ProcessResult Run(string file, IReadOnlyList<string> arguments, string? workingDirectory, string? logPath = null)
    {
        var startInfo = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (logPath is not null)
        {
            File.WriteAllText(logPath, stdout + stderr);
        }

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static void Cleanup(string workDirectory)
    {
        if (Environment.GetEnvironmentVariable("ANALYZER_GATE_KEEP_REPORT") == "1")
        {
            Console.WriteLine($"Reports kept in {workDirectory}");
            return;
        }

        try
        {
            Directory.Delete(workDirectory, recursive: true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"WARNING: could not remove {workDirectory}: {exception.Message}");
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"ERROR: {message}");
        return 1;
    }

    private sealed record Finding(string File, int Line, string Rule, string Message);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class Exclusions(HashSet<(string File, string Rule)> entries)
    {
        public bool IsExcluded(Finding finding) => entries.Contains((finding.File, finding.Rule));
    }
}

