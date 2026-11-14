// Baseline-aware, diff-scoped analyzer gate (issue #1714).
//
// Only the C# files changed against a base ref (default: origin/main) are
// analyzed, and the gate fails only on diagnostics that are NEW relative to the
// merge-base. Pre-existing debt is reported as "baselined" instead of failing,
// which is what AGENTS.md promises and what the plain
// `dotnet format analyzers --include <changed files>` invocation could not do
// (it reports every finding in a touched file).
//
// How it works: the exact same `dotnet format analyzers` command is run twice -
// once in the working tree and once in a source snapshot of the merge-base
// (created with `git archive`, so no git metadata and no other checkout is
// touched). Diagnostics are keyed by (file, rule id, message) rather than by
// line number, so moving code around does not resurrect old findings, while a
// genuinely new finding - including a finding whose measured complexity or
// coupling got worse - has a different message and therefore counts as new.
// Pure renames are compared against the file's merge-base path, so renaming a
// file does not resurrect its pre-existing findings.
//
// This is a .NET file-based app so the gate behaves identically in CI (Ubuntu)
// and on Windows, without depending on bash, jq or a specific shell.
//
// Usage:
//   mise run analyzer-gate
//   ANALYZER_BASE=origin/develop mise run analyzer-gate
//   mise run analyzer-gate -- src/path/File.cs
//
// Environment:
//   ANALYZER_BASE            git ref to diff against (default: origin/main)
//   ANALYZER_GATE_KEEP_TEMP  set to 1 to keep the snapshot/report directories

using System.Diagnostics;
using System.Formats.Tar;
using System.Text.Json;

namespace AnalyzerGate;

internal static class Program
{
    private const string DefaultBase = "origin/main";

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

        var mergeBase = Capture("git", ["merge-base", baseRef, "HEAD"], repoRoot).Trim();
        if (mergeBase.Length == 0)
        {
            return Fail($"could not determine the merge-base of '{baseRef}' and HEAD");
        }

        Console.WriteLine($"Analyzer baseline: {baseRef} ({mergeBase})");

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
        Dictionary<string, string> renamedFrom = new(StringComparer.Ordinal);
        if (explicitFiles.Count > 0)
        {
            changedFiles = explicitFiles;
        }
        else
        {
            configChanged = ChangedFiles(repoRoot, baseRef, "CodeMetricsConfig.txt").Count > 0;
            changedFiles = ChangedFiles(repoRoot, baseRef, "*.cs");
            renamedFrom = RenamedFiles(repoRoot, baseRef, "*.cs");
        }

        // CA1509 (invalid CodeMetricsConfig.txt entry) is reported on the config
        // file, so it cannot be filtered with `--include`; promote it to an error
        // while validating.
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

        var workDirectory = CreateWorkDirectory();
        if (workDirectory is null)
        {
            return 1;
        }

        try
        {
            var snapshot = Path.Combine(workDirectory, "baseline-src");
            if (!CreateSnapshot(repoRoot, mergeBase, snapshot))
            {
                return 1;
            }

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
                        Path.Combine(workDirectory, $"current-{index}"),
                        "the working tree", out var current))
                {
                    return 1;
                }

                // Files that do not exist at the merge-base are new: every
                // finding on them is new by definition, so they are omitted
                // from the baseline run instead of being compared. Renamed
                // files are compared against their old path so a pure rename
                // keeps the pre-existing findings baselined.
                var baselinePaths = new List<string>();
                var baselinePathMap = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var file in files)
                {
                    var baselinePath = renamedFrom.GetValueOrDefault(file, file);
                    if (!File.Exists(Path.Combine(snapshot, baselinePath)))
                    {
                        continue;
                    }
                    baselinePaths.Add(baselinePath);
                    baselinePathMap[baselinePath] = file;
                }

                var baseline = new List<Finding>();
                if (File.Exists(Path.Combine(snapshot, project)) && baselinePaths.Count > 0)
                {
                    if (!AnalyzeProject(
                            snapshot, project, baselinePaths,
                            Path.Combine(workDirectory, $"baseline-{index}"),
                            mergeBase, out baseline, baselinePathMap))
                    {
                        return 1;
                    }
                }

                var (newFindings, baselined) = Subtract(current, baseline);
                if (newFindings.Count == 0)
                {
                    Console.WriteLine($"No new analyzer findings in {project} ({baselined} pre-existing baselined).");
                    continue;
                }

                Console.WriteLine($"New analyzer findings in {project} ({baselined} pre-existing baselined):");
                foreach (var finding in newFindings)
                {
                    Console.WriteLine($"  {finding.File}({finding.Line}): {finding.Rule} {finding.Message}");
                }
                status = 1;
            }
        }
        finally
        {
            Cleanup(workDirectory);
        }

        return status;
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

    // Maps a renamed file's current path to its path at the merge-base.
    private static Dictionary<string, string> RenamedFiles(string repoRoot, string baseRef, string pathSpec)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var arguments in new[]
                 {
                     new[] { "diff", "-M", "--name-status", $"{baseRef}...HEAD", "--", pathSpec },
                     new[] { "diff", "-M", "--name-status", "HEAD", "--", pathSpec }
                 })
        {
            foreach (var line in Lines(Capture("git", arguments, repoRoot)))
            {
                var columns = line.Split('\t');
                if (columns.Length == 3 && columns[0].StartsWith('R'))
                {
                    renames[NormalizePath(columns[2])] = NormalizePath(columns[1]);
                }
            }
        }
        return renames;
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

    private static bool CreateSnapshot(string repoRoot, string mergeBase, string destination)
    {
        Directory.CreateDirectory(destination);
        var archive = Path.Combine(Path.GetDirectoryName(destination)!, "baseline.tar");
        var git = Run("git", ["archive", "--format=tar", "-o", archive, mergeBase], repoRoot);
        if (git.ExitCode != 0)
        {
            Fail("could not archive the merge-base revision");
            return false;
        }

        try
        {
            TarFile.ExtractToDirectory(archive, destination, overwriteFiles: true);
            return true;
        }
        catch (Exception exception)
        {
            Fail($"could not extract the merge-base snapshot: {exception.Message}");
            return false;
        }
    }

    private static bool AnalyzeProject(
        string runRoot,
        string project,
        List<string> files,
        string reportDirectory,
        string revisionLabel,
        out List<Finding> findings,
        IReadOnlyDictionary<string, string>? pathMap = null)
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
        var result = Run("dotnet", arguments, runRoot, log);
        if (result.ExitCode != 0 && result.ExitCode != 2)
        {
            Console.Error.WriteLine($"{result.StandardOutput}{result.StandardError}");
            Fail($"analyzer run failed for {project} ({revisionLabel})");
            return false;
        }

        findings = ReadReport(Path.Combine(reportDirectory, "format-report.json"), pathMap);
        return true;
    }

    private static List<Finding> ReadReport(string reportPath, IReadOnlyDictionary<string, string>? pathMap)
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
            if (pathMap is not null && pathMap.TryGetValue(path, out var mappedPath))
            {
                path = mappedPath;
            }
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
                // FormatDescription is prefixed with "warning <rule>: "; drop that
                // so the key and the printed line contain only the message.
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

    private static (List<Finding> New, int Baselined) Subtract(List<Finding> current, List<Finding> baseline)
    {
        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var finding in baseline)
        {
            remaining[finding.Key] = remaining.GetValueOrDefault(finding.Key) + 1;
        }

        var newFindings = new List<Finding>();
        foreach (var finding in current)
        {
            if (remaining.GetValueOrDefault(finding.Key) > 0)
            {
                remaining[finding.Key]--;
            }
            else
            {
                newFindings.Add(finding);
            }
        }

        return (newFindings, current.Count - newFindings.Count);
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
        if (Environment.GetEnvironmentVariable("ANALYZER_GATE_KEEP_TEMP") == "1")
        {
            Console.WriteLine($"Artifacts kept in {workDirectory}");
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

    private sealed record Finding(string File, int Line, string Rule, string Message)
    {
        public string Key => $"{File}\u0001{Rule}\u0001{Message}";
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
