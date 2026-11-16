// Diff-scoped analyzer gate (issue #1714).
//
// Analyzes only the C# files changed against a base ref (default: origin/main) in the reusable
// library projects, and fails when any analyzer finding remains after applying the explicit
// allowlist in tools/analyzer-gate/exclusions.txt.
//
// The allowlist is deliberately tiny: it exists only for findings that cannot be fixed without
// changing a public contract that this repository's tests or views pin (for example the class
// coupling of a type that implements a large public interface). Each entry is
// `path:RULE[:symbol]`: the optional symbol limits the entry to the declaration the analyzer
// names in the message, so an entry cannot blanket a whole file. Every entry must carry a comment
// naming the reason, and an entry that matches no finding fails the gate so the list cannot rot.
//
// The gate also rejects suppressions introduced by the diff: a changed file may keep the
// `#pragma warning disable` or [SuppressMessage] it already carried on the base ref, but it must
// not add one for a gated rule. Otherwise "zero tolerance" would only mean "tolerate everything
// the compiler was told to ignore".
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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnalyzerGate;

internal static class Program
{
    private const string DefaultBase = "origin/main";
    private const string ExclusionsFile = "tools/analyzer-gate/exclusions.txt";
    private const string AnySuppression = "*";
    private const string NoDeclaration = "<none>";

    /// <summary>
    /// The rules this repository's threshold configuration (`.editorconfig`, `CodeMetricsConfig.txt`)
    /// turns on. A suppression introduced for one of them would hide new debt from the gate.
    /// </summary>
    private static readonly string[] GatedRules = ["CA1501", "CA1502", "CA1505", "CA1506", "S3776"];

    /// <summary>
    /// A type declaration. The name is the stable anchor a suppression is attributed to.
    /// </summary>
    private static readonly Regex TypeDeclarationPattern = new(
        @"^(?:(?:public|private|protected|internal|static|sealed|abstract|partial|readonly|ref|file|unsafe|new)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:struct|class))?)\s+(?<name>\w+)",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// A member declaration (method, constructor return-type form, property or operator). Fields and
    /// local variables are deliberately not matched: a suppression only ever wraps an executable
    /// member, and the analyzer never reports CA1502/CA1505/S3776 on a field.
    /// </summary>
    private static readonly Regex MemberDeclarationPattern = new(
        @"^(?:(?:public|private|protected|internal|static|async|virtual|override|sealed|abstract|partial|unsafe|extern|new|required|readonly)\s+)*(?:[\w<>\[\],\.\?]+\s+)+(?<name>\w+)\s*(?:<[^>]*>)?\s*(?:\(|\{|=>)",
        RegexOptions.CultureInvariant);

    private static readonly Regex RuleIdInAttributePattern =
        new(@"^(?<id>(?:CA|S)\d{4})(?::|$)", RegexOptions.CultureInvariant);

    private static readonly Regex ConstantDeclarationPattern =
        new(@"(?<![\w.])const\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=", RegexOptions.CultureInvariant);

    private static readonly Regex IdentifierPathPattern =
        new("^[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant);

    private static readonly Regex FileScopedNamespacePattern = new(
        @"^\s*namespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*;",
        RegexOptions.CultureInvariant);

    private static readonly Regex BlockNamespacePattern = new(
        @"^\s*namespace\s+(?<name>[A-Za-z_][A-Za-z0-9_.]*)\s*\{?\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SuppressMessageAliasPattern = new(
        @"\b(?:global\s+)?using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global::)?(?:[A-Za-z_][A-Za-z0-9_]*\.)*SuppressMessageAttribute\s*;",
        RegexOptions.CultureInvariant);

    private static readonly Regex GlobalSuppressMessageAliasPattern = new(
        @"\bglobal\s+using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global::)?(?:[A-Za-z_][A-Za-z0-9_]*\.)*SuppressMessageAttribute\s*;",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The <c>[assembly: </c>-style usage target that precedes an attribute name in an attribute list.
    /// </summary>
    private static readonly Regex AttributeUsageTargetPattern = new(
        @"^\[\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*$",
        RegexOptions.CultureInvariant);

    private const int MaxConstantEvaluationDepth = 8;

    /// <summary>
    /// A <c>#pragma warning disable|restore</c> directive. The <c>#</c> and the directive name may be
    /// separated by whitespace, so the spelling is parsed rather than searched for verbatim.
    /// </summary>
    private static readonly Regex PragmaDirectivePattern = new(
        @"^\s*#\s*pragma\s+warning\s+(?<action>disable|restore)\b(?<rules>.*)$",
        RegexOptions.CultureInvariant);

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
        if (!TryCaptureGit(null, ["rev-parse", "--show-toplevel"], out var repoRootOutput))
        {
            return Fail("not inside a git repository");
        }

        var repoRoot = repoRootOutput.Trim();
        if (repoRoot.Length == 0)
        {
            return Fail("git could not report the repository root");
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
                explicitFiles.Add(CanonicalizePath(repoRoot, arg));
            }
        }

        var explicitMode = explicitFiles.Count > 0;
        List<string> changedFiles;
        if (explicitMode)
        {
            changedFiles = explicitFiles;
        }
        else
        {
            var configFiles = ChangedFiles(repoRoot, baseRef, "CodeMetricsConfig.txt");
            if (configFiles is null) return 1;
            configChanged = configFiles.Count > 0;

            var sourceFiles = ChangedFiles(repoRoot, baseRef, "*.cs");
            if (sourceFiles is null) return 1;
            changedFiles = sourceFiles;
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

        // A diff must not hide new debt behind a suppression the base ref did not have. A global
        // alias lives in another file of the same project, so the suppression scan needs the files
        // grouped by project rather than as one flat list.
        var suppressionFailures = FindNewSuppressions(repoRoot, baseRef, projects);
        foreach (var failure in suppressionFailures)
        {
            Console.Error.WriteLine($"ERROR: {failure}");
        }
        if (suppressionFailures.Count > 0) status = 1;

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

        // An entry can only be judged when its file is part of this diff: an allowlisted file the
        // PR does not touch is not analyzed, and its finding is expected to be absent. Explicit-file
        // mode analyzes just a slice of the diff, so it never checks for unused entries at all.
        if (!explicitMode)
        {
            var analyzedFiles = projects.Values
                .SelectMany(files => files)
                .ToHashSet(StringComparer.Ordinal);
            var unused = exclusions.Unmatched(analyzedFiles).ToList();
            if (unused.Count > 0)
            {
                Console.Error.WriteLine($"ERROR: {ExclusionsFile} has entries that matched no finding:");
                foreach (var entry in unused)
                {
                    Console.Error.WriteLine($"  {entry}");
                }
                status = 1;
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

    /// <summary>
    /// The changed files the gate must analyze: the diff against the merge base, the uncommitted
    /// changes and the untracked files. Returns <c>null</c> when git could not answer (for example
    /// a shallow checkout without a merge base), because an empty list would silently pass the gate.
    /// </summary>
    private static List<string>? ChangedFiles(string repoRoot, string baseRef, string pathSpec)
    {
        var output = new List<string>();
        if (!TryCaptureGit(repoRoot, ["diff", "--name-only", "-z", "--diff-filter=ACMR", $"{baseRef}...HEAD", "--", pathSpec], out var committed)
            || !TryCaptureGit(repoRoot, ["diff", "--name-only", "-z", "--diff-filter=ACMR", "HEAD", "--", pathSpec], out var uncommitted)
            || !TryCaptureGit(repoRoot, ["ls-files", "-z", "--others", "--exclude-standard", "--", pathSpec], out var untracked))
        {
            return null;
        }

        output.AddRange(NullSeparated(committed));
        output.AddRange(NullSeparated(uncommitted));
        output.AddRange(NullSeparated(untracked));
        return output.Select(NormalizePath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Suppressions a changed file added relative to the base ref. Project-wide aliases are collected
    /// first, because a <c>global using</c> alias declared in one file applies to every file of the
    /// project that references it.
    /// </summary>
    private static List<string> FindNewSuppressions(
        string repoRoot,
        string baseRef,
        SortedDictionary<string, List<string>> projects)
    {
        var failures = new List<string>();
        foreach (var (project, files) in projects)
        {
            var projectAliases = FindProjectGlobalAliases(repoRoot, project);
            var baselineAliases = FindBaselineProjectGlobalAliases(repoRoot, baseRef, project);
            foreach (var file in files)
            {
                var path = Path.Combine(repoRoot, file);
                if (!File.Exists(path)) continue;

                failures.AddRange(FindNewSuppressionsIn(
                    file,
                    ReadAtBase(repoRoot, baseRef, file),
                    File.ReadAllText(path),
                    projectAliases,
                    baselineAliases));
            }
        }

        return failures;
    }

    /// <summary>
    /// The aliases that name <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/> in
    /// a project's <c>global using</c> directives. The scan is project-wide because the alias is
    /// declared in one file and used in another; generated folders are ignored.
    /// </summary>
    internal static List<string> FindProjectGlobalAliases(string repoRoot, string projectFile)
    {
        var projectDirectory = Path.GetDirectoryName(Path.Combine(repoRoot, projectFile));
        if (string.IsNullOrEmpty(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return [];
        }

        var aliases = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var segments = Path.GetRelativePath(projectDirectory, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment is "bin" or "obj"))
            {
                continue;
            }

            aliases.UnionWith(
                FindSuppressMessageAliases(MaskLiteralsAndComments(File.ReadAllText(path)), globalOnly: true));
        }

        return [.. aliases];
    }

    /// <summary>
    /// The project-wide aliases as they were at the base ref. An alias that changes meaning between
    /// revisions changes whether an attribute suppresses anything, so the baseline scan must not use
    /// the current alias set. Only files that mention a global using are read from git.
    /// </summary>
    internal static List<string> FindBaselineProjectGlobalAliases(
        string repoRoot,
        string baseRef,
        string projectFile)
    {
        var projectDirectory = Path.GetDirectoryName(NormalizePath(projectFile));
        if (string.IsNullOrEmpty(projectDirectory))
        {
            return [];
        }

        var result = Run(
            "git",
            ["grep", "-l", "-E", "global[[:space:]]+using", baseRef, "--", "*.cs"],
            repoRoot);
        if (result.ExitCode != 0)
        {
            return [];
        }

        var prefix = projectDirectory + "/";
        var pathPrefix = baseRef + ":";
        var aliases = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in result.StandardOutput.Split(
                     ['\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith(pathPrefix, StringComparison.Ordinal)) continue;

            var file = NormalizePath(line[pathPrefix.Length..]);
            if (!file.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var source = ReadAtBase(repoRoot, baseRef, file);
            if (source is null) continue;

            aliases.UnionWith(FindSuppressMessageAliases(MaskLiteralsAndComments(source), globalOnly: true));
        }

        return [.. aliases];
    }

    /// <summary>
    /// The suppressions a file added relative to the base ref. Each suppression is identified by its
    /// rule and by the declaration it covers, plus the attribute usage target, Scope and Target when
    /// the attribute names them, so a pre-existing suppression authorizes neither a second
    /// occurrence, nor the same rule moved to another member or another assembly-level Target, nor a
    /// wider region created by a removed restore. Renaming a suppressed declaration therefore also
    /// fails the gate: the member is expected to lose its suppression in the change that moves it. A
    /// suppression whose rule, Scope or Target argument cannot be resolved to a literal value always
    /// fails, because the gate cannot tell what it hides.
    /// </summary>
    internal static List<string> FindNewSuppressionsIn(
        string file,
        string? baselineSource,
        string currentSource,
        IReadOnlyCollection<string>? projectAliases = null,
        IReadOnlyCollection<string>? baselineProjectAliases = null)
    {
        var failures = new List<string>();
        var current = ScanSuppressions(currentSource, projectAliases);

        foreach (var unresolvable in current.Unresolvable)
        {
            failures.Add(
                $"{file}({unresolvable.Line}): the suppression {unresolvable.Kind} argument `{unresolvable.Expression}` " +
                $"cannot be resolved (covering {unresolvable.Scope}), so the gate cannot verify what it hides. " +
                "Use a single string literal or constants the gate can read.");
        }

        if (current.Scopes.Count == 0) return failures;

        var baseline = ScanSuppressions(baselineSource, baselineProjectAliases ?? projectAliases).Scopes;
        foreach (var ((rule, scope), count) in current.Scopes)
        {
            var baselineCount = baseline.TryGetValue((rule, scope), out var known) ? known : 0;
            if (count <= baselineCount) continue;

            var line = ScopeLine(currentSource, scope);
            var location = line is null ? file : $"{file}({line})";
            failures.Add(baselineCount == 0
                ? $"{location}: adds a new {rule} suppression covering {scope}. Fix the finding instead of suppressing it."
                : $"{location}: adds another {rule} suppression covering {scope} (base: {baselineCount}, now: {count}). Fix the finding instead of suppressing it.");
        }

        return failures;
    }

    private static string? ReadAtBase(string repoRoot, string baseRef, string file)
    {
        var result = Run("git", ["show", $"{baseRef}:{file}"], repoRoot);
        return result.ExitCode == 0 ? result.StandardOutput : null;
    }

    private sealed record SuppressionScan(
        SortedDictionary<(string Rule, string Scope), int> Scopes,
        IReadOnlyList<UnresolvableSuppression> Unresolvable);

    private sealed record UnresolvableSuppression(int Line, string Scope, string Kind, string Expression);

    /// <summary>
    /// Every gated-rule suppression in the source, keyed by rule and by the declaration it covers.
    /// Comments and literals are masked first, so text that only mentions a directive or an attribute
    /// cannot count, and a suppression is attributed to the declaration it applies to rather than to
    /// a line number that every edit would move.
    /// </summary>
    internal static SortedDictionary<(string Rule, string Scope), int> SuppressionScopes(string? source) =>
        ScanSuppressions(source).Scopes;

    private static SuppressionScan ScanSuppressions(string? source, IReadOnlyCollection<string>? projectAliases = null)
    {
        var scopes = new SortedDictionary<(string Rule, string Scope), int>();
        var unresolvable = new List<UnresolvableSuppression>();
        if (string.IsNullOrEmpty(source)) return new SuppressionScan(scopes, unresolvable);

        var code = MaskLiteralsAndComments(source);
        // Attribute arguments hold the rule id as a string literal, so they are read from a variant
        // that keeps literals and only removes comments.
        var argumentsCode = MaskComments(source);
        var lines = ReadLines(code);
        var types = FindTypeDeclarations(lines);
        var declarations = FindDeclarations(lines, types, code);
        var (fileNamespace, namespaces) = FindNamespaces(lines);
        var constants = FindConstantDeclarations(lines, types, fileNamespace, namespaces);
        var conditionalStates = FindConditionalStates(lines);

        foreach (var (rule, start, end, depth, context) in FindPragmaRegions(lines, code.Length, conditionalStates))
        {
            var covered = declarations
                .Where(declaration =>
                    declaration.Offset > start
                    && declaration.Offset < end
                    && declaration.Depth == depth)
                .Select(declaration => declaration.Key)
                .ToList();
            if (covered.Count == 0) covered.Add(NoDeclaration);

            foreach (var suppressedRule in ExpandRule(rule))
            {
                foreach (var scope in covered)
                {
                    Increment(scopes, suppressedRule, AppendContext(scope, context));
                }
            }
        }

        var aliases = FindSuppressMessageAliases(code);
        if (projectAliases is not null) aliases.AddRange(projectAliases);
        aliases = [.. aliases.Distinct(StringComparer.Ordinal)];
        foreach (var attribute in FindSuppressMessageAttributes(code, argumentsCode, aliases))
        {
            var conditional = ConditionalStateAt(lines, conditionalStates, attribute.Start);
            if (!conditional.IsActive) continue;

            var scope = ScopeAfter(declarations, attribute.End);
            var context = conditional.Chain;
            var referenceNamespace = NamespaceAt(lines, fileNamespace, namespaces, attribute.Start);
            var referenceTypePath = TypePathAt(types, attribute.Start, DepthAt(lines, attribute.Start));

            // The declaration a suppression is attached to is not enough on its own: an
            // assembly-level suppression names its target method in Target, and Scope narrows the
            // kind of API. Both belong to the identity, otherwise changing Target moves the
            // suppression from one method to another without changing anything the gate sees.
            var identity = scope;
            if (attribute.AttributeTarget.Length > 0)
            {
                identity += $" attribute:{attribute.AttributeTarget}";
            }

            var scopeArgument = ReadNamedArgument(attribute.Arguments, "Scope");
            if (scopeArgument is not null)
            {
                if (!TryEvaluateRuleArgument(
                        scopeArgument, argumentsCode, constants, referenceNamespace, referenceTypePath,
                        out var scopeValue))
                {
                    unresolvable.Add(new UnresolvableSuppression(
                        LineAt(code, attribute.Start), scope, "Scope", scopeArgument));
                    continue;
                }

                identity += $" scope:{Compact(scopeValue)}";
            }

            var targetArgument = ReadNamedArgument(attribute.Arguments, "Target");
            if (targetArgument is not null)
            {
                if (!TryEvaluateRuleArgument(
                        targetArgument, argumentsCode, constants, referenceNamespace, referenceTypePath,
                        out var targetValue))
                {
                    unresolvable.Add(new UnresolvableSuppression(
                        LineAt(code, attribute.Start), scope, "Target", targetArgument));
                    continue;
                }

                identity += $" target:{Compact(targetValue)}";
            }

            identity = AppendContext(identity, context);

            var ruleArgument = ReadRuleArgument(attribute.Arguments);
            if (ruleArgument is null) continue;

            if (!TryEvaluateRuleArgument(
                    ruleArgument,
                    argumentsCode,
                    constants,
                    referenceNamespace,
                    referenceTypePath,
                    out var ruleText))
            {
                unresolvable.Add(new UnresolvableSuppression(LineAt(code, attribute.Start), scope, "rule", ruleArgument));
                continue;
            }

            foreach (Match id in RuleIdInAttributePattern.Matches(ruleText))
            {
                if (IsGatedRule(id.Groups["id"].Value))
                {
                    Increment(scopes, id.Groups["id"].Value.ToUpperInvariant(), identity);
                }
            }
        }

        return new SuppressionScan(scopes, unresolvable);
    }

    /// <summary>
    /// Evaluates a suppression rule argument to the string the compiler sees: a single literal, a
    /// concatenation of literals, or constants (including constants whose initializer is itself an
    /// expression). Returns <c>false</c> when any part cannot be resolved, so the caller can reject
    /// the suppression instead of assuming it hides nothing.
    /// </summary>
    private static bool TryEvaluateRuleArgument(
        string expression,
        string source,
        IReadOnlyList<ConstantDeclaration> constants,
        string referenceNamespace,
        string referenceTypePath,
        out string value) =>
        TryEvaluateExpression(expression, source, constants, referenceNamespace, referenceTypePath, 0, out value);

    private static bool TryEvaluateExpression(
        string expression,
        string source,
        IReadOnlyList<ConstantDeclaration> constants,
        string referenceNamespace,
        string referenceTypePath,
        int depth,
        out string value)
    {
        value = string.Empty;
        if (depth > MaxConstantEvaluationDepth) return false;

        var parts = SplitTopLevel(expression, '+');
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            var operand = part.Trim();
            if (operand.Length == 0) return false;

            if (TryDecodeStringLiteral(operand, out var literal))
            {
                builder.Append(literal);
                continue;
            }

            if (!IdentifierPathPattern.IsMatch(operand)) return false;
            if (!TryResolveStringConstant(
                    operand, source, constants, referenceNamespace, referenceTypePath, depth, out var constant))
            {
                return false;
            }
            builder.Append(constant);
        }

        value = builder.ToString();
        return true;
    }

    /// <summary>
    /// Resolves a constant reference the way the compiler binds it. A qualified path must match the
    /// constant's full type path, or its type path inside the reference's namespace. An unqualified
    /// name is only bound to a constant declared by the type that encloses the reference — the case
    /// member attributes rely on — because a member of another type is not in scope for an
    /// unqualified reference (`using static` can bind an external one instead). Anything ambiguous or
    /// not provable from this file is rejected.
    /// </summary>
    private static bool TryResolveStringConstant(
        string path,
        string source,
        IReadOnlyList<ConstantDeclaration> constants,
        string referenceNamespace,
        string referenceTypePath,
        int depth,
        out string value)
    {
        value = string.Empty;
        var segments = path.Split('.');
        var name = segments[^1];
        var qualifier = segments.Length > 1 ? string.Join('.', segments[..^1]) : string.Empty;

        var candidates = constants.Where(candidate => candidate.Name == name).ToList();
        if (qualifier.Length > 0)
        {
            candidates = candidates
                .Where(candidate =>
                    string.Equals(qualifier, candidate.FullPath, StringComparison.Ordinal)
                    || (string.Equals(qualifier, candidate.TypePath, StringComparison.Ordinal)
                        && string.Equals(candidate.Namespace, referenceNamespace, StringComparison.Ordinal)))
                .ToList();
        }
        else
        {
            if (referenceTypePath.Length == 0) return false;

            candidates = candidates
                .Where(candidate =>
                    string.Equals(candidate.TypePath, referenceTypePath, StringComparison.Ordinal)
                    && string.Equals(candidate.Namespace, referenceNamespace, StringComparison.Ordinal))
                .ToList();
        }

        if (candidates.Count != 1) return false;

        var declaration = candidates[0];
        var end = FindStatementEnd(source, declaration.InitializerStart);
        if (end < 0) return false;

        return TryEvaluateExpression(
            source[declaration.InitializerStart..end],
            source,
            constants,
            declaration.Namespace,
            declaration.TypePath,
            depth + 1,
            out value);
    }

    /// <summary>
    /// The file's type declarations with the offset where each scope ends, outermost first when
    /// combined with <see cref="TypePathAt"/>.
    /// </summary>
    private static List<TypeDeclaration> FindTypeDeclarations(IReadOnlyList<SourceLine> lines)
    {
        var types = new List<TypeDeclaration>();
        for (var index = 0; index < lines.Count; index++)
        {
            var typeMatch = TypeDeclarationPattern.Match(lines[index].Text.TrimStart());
            if (!typeMatch.Success) continue;

            types.Add(new TypeDeclaration(
                lines[index].Offset,
                lines[index].Depth,
                FindScopeEnd(lines, index),
                typeMatch.Groups["name"].Value));
        }

        return types;
    }

    /// <summary>
    /// The chain of types whose scope contains <paramref name="offset"/>, outermost first. Empty for
    /// a declaration that sits at file scope (for example an attribute above a class).
    /// </summary>
    private static string TypePathAt(IReadOnlyList<TypeDeclaration> types, int offset, int depth) =>
        string.Join('.',
            types
                .Where(type => type.Offset < offset && offset < type.End && type.Depth < depth)
                .GroupBy(type => type.Depth)
                .Select(group => group.OrderByDescending(type => type.Offset).First())
                .OrderBy(type => type.Depth)
                .Select(type => type.Name));

    /// <summary>
    /// Every <c>const string</c> declared directly as a type member, with the namespace and the type
    /// path that declare it. Local constants inside methods are not members and cannot be reached by
    /// an unqualified attribute reference, so they are not indexed.
    /// </summary>
    private static List<ConstantDeclaration> FindConstantDeclarations(
        IReadOnlyList<SourceLine> lines,
        IReadOnlyList<TypeDeclaration> types,
        string fileNamespace,
        IReadOnlyList<NamespaceDeclaration> namespaces)
    {
        var declarations = new List<ConstantDeclaration>();
        foreach (var line in lines)
        {
            var constant = ConstantDeclarationPattern.Match(line.Text);
            if (!constant.Success) continue;
            if (!IsDirectTypeMember(line, types)) continue;

            var typePath = TypePathAt(types, line.Offset, line.Depth);
            if (typePath.Length == 0) continue;

            declarations.Add(new ConstantDeclaration(
                NamespaceAt(lines, fileNamespace, namespaces, line.Offset),
                typePath,
                constant.Groups["name"].Value,
                line.Offset + constant.Index + constant.Length));
        }

        return declarations;
    }

    /// <summary>
    /// The file-scoped namespace (if any) and every block namespace with the offset where its scope
    /// ends, so a reference can be resolved against the namespace it sits in.
    /// </summary>
    private static (string FileNamespace, List<NamespaceDeclaration> Blocks) FindNamespaces(
        IReadOnlyList<SourceLine> lines)
    {
        var fileNamespace = string.Empty;
        var blocks = new List<NamespaceDeclaration>();

        for (var index = 0; index < lines.Count; index++)
        {
            var fileScoped = FileScopedNamespacePattern.Match(lines[index].Text);
            if (fileScoped.Success)
            {
                fileNamespace = fileScoped.Groups["name"].Value;
                continue;
            }

            var block = BlockNamespacePattern.Match(lines[index].Text);
            if (!block.Success) continue;

            blocks.Add(new NamespaceDeclaration(
                lines[index].Offset,
                lines[index].Depth,
                FindScopeEnd(lines, index),
                block.Groups["name"].Value));
        }

        return (fileNamespace, blocks);
    }

    /// <summary>
    /// The offset where the scope of the declaration at <paramref name="declarationIndex"/> ends: the
    /// first line that is back at the declaration's depth after its body has opened.
    /// </summary>
    private static int FindScopeEnd(IReadOnlyList<SourceLine> lines, int declarationIndex)
    {
        var depth = lines[declarationIndex].Depth;
        var opened = false;
        for (var index = declarationIndex + 1; index < lines.Count; index++)
        {
            var lineDepth = lines[index].Depth;
            if (!opened)
            {
                if (lineDepth > depth) opened = true;
                continue;
            }

            if (lineDepth <= depth) return lines[index].Offset;
        }

        return int.MaxValue;
    }

    private static string NamespaceAt(
        IReadOnlyList<SourceLine> lines,
        string fileNamespace,
        IReadOnlyList<NamespaceDeclaration> namespaces,
        int offset)
    {
        var depth = DepthAt(lines, offset);
        var active = namespaces
            .Where(declaration => declaration.Offset < offset && offset < declaration.End && declaration.Depth < depth)
            .OrderBy(declaration => declaration.Offset)
            .Select(declaration => declaration.Name)
            .ToList();

        return active.Count > 0 ? string.Join('.', active) : fileNamespace;
    }

    private static int DepthAt(IReadOnlyList<SourceLine> lines, int offset)
    {
        var depth = 0;
        foreach (var line in lines)
        {
            if (line.Offset > offset) break;
            depth = line.Depth;
        }

        return depth;
    }

    /// <summary>
    /// The index of the <c>;</c> that ends the statement starting at <paramref name="start"/>, or -1.
    /// String literals are skipped so a semicolon inside one does not end the statement.
    /// </summary>
    private static int FindStatementEnd(string source, int start)
    {
        for (var index = start; index < source.Length; index++)
        {
            if (IsLiteralStart(source, index))
            {
                var end = MaskLiteral(source, index, new StringBuilder(), maskContent: true);
                if (end > index)
                {
                    index = end - 1;
                    continue;
                }
            }

            if (source[index] == ';') return index;
        }

        return -1;
    }

    private static int LineAt(string source, int offset)
    {
        var line = 1;
        for (var index = 0; index < offset && index < source.Length; index++)
        {
            if (source[index] == '\n') line++;
        }

        return line;
    }

    private static int? ScopeLine(string source, string scope)
    {
        scope = BaseScope(scope);
        if (scope == NoDeclaration) return null;

        var code = MaskLiteralsAndComments(source);
        var lines = ReadLines(code);
        var types = FindTypeDeclarations(lines);
        for (var index = 0; index < lines.Count; index++)
        {
            if (DeclarationKey(lines[index], types, code) == scope) return index + 1;
        }

        return null;
    }

    private static string BaseScope(string scope)
    {
        var marker = scope.IndexOf(" [", StringComparison.Ordinal);
        return marker < 0 ? scope : scope[..marker];
    }

    private static string AppendContext(string scope, string context) =>
        context.Length == 0 ? scope : $"{scope} [{context}]";

    private enum ConditionTruth
    {
        False,
        True,
        Unknown
    }

    private sealed record ConditionalLine(string Chain, bool IsActive, bool IsCertain);

    private sealed class ConditionalFrame
    {
        public ConditionalFrame(string chain, ConditionTruth branchTruth)
        {
            Chain = chain;
            BranchTruth = branchTruth;
            AllPreviousFalse = branchTruth == ConditionTruth.False;
        }

        public string Chain { get; set; }

        public ConditionTruth BranchTruth { get; set; }

        public bool EarlierBranchTrue { get; set; }

        public bool AllPreviousFalse { get; set; }
    }

    /// <summary>
    /// The conditional-compilation state of each line: the full <c>#if/#elif/#else</c> chain, with
    /// the truth the preprocessor assigns to every branch (in-file <c>#define</c>/<c>#undef</c>
    /// included), and whether the line is provably inactive. Both are needed: an inactive suppression
    /// is not recorded at all, while a condition change that leaves a suppression potentially active
    /// still changes the chain, so a newly effective suppression cannot be mistaken for an unchanged
    /// one. A symbol the file does not define is unknown, never false, because it may come from the
    /// project file.
    /// </summary>
    private static List<ConditionalLine> FindConditionalStates(IReadOnlyList<SourceLine> lines)
    {
        var states = new List<ConditionalLine>(lines.Count);
        var stack = new List<ConditionalFrame>();
        var symbols = new Dictionary<string, ConditionTruth>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            var trimmed = line.Text.Trim();

            if (TryDirective(trimmed, "#if", out var expression))
            {
                var truth = EvaluateCondition(expression, symbols);
                stack.Add(new ConditionalFrame($"if:{Compact(expression)}:{TruthName(truth)}", truth));
            }
            else if (TryDirective(trimmed, "#elif", out expression) && stack.Count > 0)
            {
                var frame = stack[^1];
                frame.EarlierBranchTrue |= frame.BranchTruth == ConditionTruth.True;
                frame.AllPreviousFalse &= frame.BranchTruth == ConditionTruth.False;
                var truth = frame.EarlierBranchTrue
                    ? ConditionTruth.False
                    : EvaluateCondition(expression, symbols);
                frame.BranchTruth = truth;
                frame.Chain += $"/elif:{Compact(expression)}:{TruthName(truth)}";
            }
            else if (TryDirective(trimmed, "#else", out _) && stack.Count > 0)
            {
                var frame = stack[^1];
                frame.EarlierBranchTrue |= frame.BranchTruth == ConditionTruth.True;
                frame.AllPreviousFalse &= frame.BranchTruth == ConditionTruth.False;
                var truth = frame.EarlierBranchTrue
                    ? ConditionTruth.False
                    : frame.AllPreviousFalse ? ConditionTruth.True : ConditionTruth.Unknown;
                frame.BranchTruth = truth;
                frame.Chain += $"/else:{TruthName(truth)}";
            }

            var active = stack.All(frame =>
                !frame.EarlierBranchTrue && frame.BranchTruth != ConditionTruth.False);
            var certain = stack.All(frame =>
                !frame.EarlierBranchTrue && frame.BranchTruth == ConditionTruth.True);
            states.Add(new ConditionalLine(string.Join('&', stack.Select(frame => frame.Chain)), active, certain));

            if (TryDirective(trimmed, "#endif", out _))
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }

            if (!active) continue;

            // A definition inside a branch that may or may not be compiled cannot be recorded as
            // unconditional: a later `#if !SYMBOL` must stay potentially active when the enclosing
            // condition is unknown.
            if (TryDirective(trimmed, "#define", out var defined))
            {
                var symbol = FirstWord(defined);
                if (symbol.Length > 0)
                {
                    symbols[symbol] = certain ? ConditionTruth.True : ConditionTruth.Unknown;
                }
            }
            else if (TryDirective(trimmed, "#undef", out var undefined))
            {
                var symbol = FirstWord(undefined);
                if (symbol.Length > 0)
                {
                    symbols[symbol] = certain ? ConditionTruth.False : ConditionTruth.Unknown;
                }
            }
        }

        return states;
    }

    private static bool TryDirective(string trimmed, string directive, out string arguments)
    {
        if (trimmed.StartsWith(directive, StringComparison.Ordinal)
            && (trimmed.Length == directive.Length || char.IsWhiteSpace(trimmed[directive.Length])))
        {
            arguments = trimmed[directive.Length..].Trim();
            return true;
        }

        arguments = string.Empty;
        return false;
    }

    private static string FirstWord(string value)
    {
        var end = 0;
        while (end < value.Length && !char.IsWhiteSpace(value[end])) end++;
        return value[..end];
    }

    private static string TruthName(ConditionTruth truth) => truth switch
    {
        ConditionTruth.True => "true",
        ConditionTruth.False => "false",
        _ => "unknown"
    };

    /// <summary>
    /// Evaluates a C# preprocessor condition. <c>true</c>, <c>false</c>, <c>!</c>, <c>&amp;&amp;</c>,
    /// <c>||</c>, <c>==</c>, <c>!=</c> and parentheses are supported; anything else makes the
    /// expression unknown, which keeps the chain (and therefore the suppression identity) sensitive
    /// to the expression text.
    /// </summary>
    private static ConditionTruth EvaluateCondition(
        string expression,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        var tokens = TokenizeCondition(expression);
        var index = 0;
        var truth = ParseOr(tokens, ref index, symbols);
        return truth is not null && index == tokens.Count ? truth.Value : ConditionTruth.Unknown;
    }

    private static ConditionTruth? ParseOr(
        IReadOnlyList<string> tokens,
        ref int index,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        var left = ParseAnd(tokens, ref index, symbols);
        while (left is not null && Peek(tokens, index) == "||")
        {
            index++;
            var right = ParseAnd(tokens, ref index, symbols);
            if (right is null) return null;
            left = Or(left.Value, right.Value);
        }

        return left;
    }

    private static ConditionTruth? ParseAnd(
        IReadOnlyList<string> tokens,
        ref int index,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        var left = ParseEquality(tokens, ref index, symbols);
        while (left is not null && Peek(tokens, index) == "&&")
        {
            index++;
            var right = ParseEquality(tokens, ref index, symbols);
            if (right is null) return null;
            left = And(left.Value, right.Value);
        }

        return left;
    }

    private static ConditionTruth? ParseEquality(
        IReadOnlyList<string> tokens,
        ref int index,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        var left = ParseUnary(tokens, ref index, symbols);
        while (left is not null && Peek(tokens, index) is "==" or "!=")
        {
            var equal = tokens[index] == "==";
            index++;
            var right = ParseUnary(tokens, ref index, symbols);
            if (right is null) return null;
            left = Equality(left.Value, right.Value, equal);
        }

        return left;
    }

    private static ConditionTruth? ParseUnary(
        IReadOnlyList<string> tokens,
        ref int index,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        if (Peek(tokens, index) != "!") return ParsePrimary(tokens, ref index, symbols);

        index++;
        var inner = ParseUnary(tokens, ref index, symbols);
        return inner is null ? null : Not(inner.Value);
    }

    private static ConditionTruth? ParsePrimary(
        IReadOnlyList<string> tokens,
        ref int index,
        IReadOnlyDictionary<string, ConditionTruth> symbols)
    {
        var token = Peek(tokens, index);
        if (token is null) return null;

        index++;
        if (token == "(")
        {
            var inner = ParseOr(tokens, ref index, symbols);
            if (inner is null || Peek(tokens, index) != ")") return null;
            index++;
            return inner;
        }

        if (token == "true") return ConditionTruth.True;
        if (token == "false") return ConditionTruth.False;
        if (!IsIdentifier(token)) return null;

        return symbols.TryGetValue(token, out var value) ? value : ConditionTruth.Unknown;
    }

    private static string? Peek(IReadOnlyList<string> tokens, int index) =>
        index < tokens.Count ? tokens[index] : null;

    private static bool IsIdentifier(string token) =>
        token.Length > 0
        && (char.IsLetter(token[0]) || token[0] == '_')
        && token.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static ConditionTruth And(ConditionTruth left, ConditionTruth right) =>
        left == ConditionTruth.False || right == ConditionTruth.False
            ? ConditionTruth.False
            : left == ConditionTruth.True && right == ConditionTruth.True
                ? ConditionTruth.True
                : ConditionTruth.Unknown;

    private static ConditionTruth Or(ConditionTruth left, ConditionTruth right) =>
        left == ConditionTruth.True || right == ConditionTruth.True
            ? ConditionTruth.True
            : left == ConditionTruth.False && right == ConditionTruth.False
                ? ConditionTruth.False
                : ConditionTruth.Unknown;

    private static ConditionTruth Not(ConditionTruth value) => value switch
    {
        ConditionTruth.True => ConditionTruth.False,
        ConditionTruth.False => ConditionTruth.True,
        _ => ConditionTruth.Unknown
    };

    private static ConditionTruth Equality(ConditionTruth left, ConditionTruth right, bool equal)
    {
        if (left == ConditionTruth.Unknown || right == ConditionTruth.Unknown) return ConditionTruth.Unknown;
        return left == right == equal ? ConditionTruth.True : ConditionTruth.False;
    }

    private static List<string> TokenizeCondition(string expression)
    {
        var tokens = new List<string>();
        for (var index = 0; index < expression.Length;)
        {
            var current = expression[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current is '(' or ')')
            {
                tokens.Add(current.ToString());
                index++;
                continue;
            }

            if (index + 1 < expression.Length)
            {
                var pair = expression.Substring(index, 2);
                if (pair is "&&" or "||" or "==" or "!=")
                {
                    tokens.Add(pair);
                    index += 2;
                    continue;
                }
            }

            if (current == '!')
            {
                tokens.Add("!");
                index++;
                continue;
            }

            if (char.IsLetterOrDigit(current) || current == '_')
            {
                var start = index;
                while (index < expression.Length
                       && (char.IsLetterOrDigit(expression[index]) || expression[index] == '_'))
                {
                    index++;
                }

                tokens.Add(expression[start..index]);
                continue;
            }

            // A token the grammar does not understand makes the whole expression unknown.
            tokens.Add("\0");
            index++;
        }

        return tokens;
    }

    private static ConditionalLine ConditionalStateAt(
        IReadOnlyList<SourceLine> lines,
        IReadOnlyList<ConditionalLine> states,
        int offset)
    {
        var index = 0;
        for (var line = 0; line < lines.Count && lines[line].Offset <= offset; line++)
        {
            index = line;
        }

        return index < states.Count ? states[index] : new ConditionalLine(string.Empty, true, true);
    }

    private static IEnumerable<string> ExpandRule(string rule) =>
        rule == AnySuppression ? GatedRules : [rule];

    private static string ScopeAfter(IReadOnlyList<Declaration> declarations, int offset)
    {
        foreach (var declaration in declarations)
        {
            if (declaration.Offset >= offset) return declaration.Key;
        }
        return NoDeclaration;
    }

    /// <summary>
    /// Replaces the contents of comments and string/char literals with spaces so the scan reads only
    /// code. Line breaks are preserved so declaration offsets and pragma scopes stay meaningful.
    /// </summary>
    internal static string MaskLiteralsAndComments(string source) => Mask(source, maskLiterals: true);

    /// <summary>
    /// Removes comments but keeps string literals, for syntax whose payload is a string (the rule id
    /// of a <c>[SuppressMessage]</c> attribute).
    /// </summary>
    internal static string MaskComments(string source) => Mask(source, maskLiterals: false);

    private static string Mask(string source, bool maskLiterals)
    {
        var masked = new StringBuilder(source.Length);
        var index = 0;
        while (index < source.Length)
        {
            var current = source[index];
            if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                index = MaskLineComment(source, index, masked);
            }
            else if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                index = MaskBlockComment(source, index, masked);
            }
            else if (IsLiteralStart(source, index))
            {
                index = MaskLiteral(source, index, masked, maskLiterals);
            }
            else
            {
                masked.Append(current);
                index++;
            }
        }

        return masked.ToString();
    }

    private static bool IsLiteralStart(string source, int index)
    {
        if (source[index] is '"' or '\'') return true;
        if (source[index] is not ('$' or '@')) return false;

        for (var probe = index; probe < source.Length && probe < index + 3; probe++)
        {
            if (source[probe] == '"') return true;
            if (source[probe] is not ('$' or '@')) return false;
        }

        return false;
    }

    private static int MaskLiteral(string source, int start, StringBuilder masked, bool maskContent)
    {
        var index = start;
        var verbatim = false;
        while (index < source.Length && source[index] is '$' or '@')
        {
            if (source[index] == '@') verbatim = true;
            masked.Append(maskContent ? ' ' : source[index]);
            index++;
        }

        if (index < source.Length && source[index] == '\'')
        {
            return MaskQuotedRun(source, index, '\'', masked, escapes: true, maskContent);
        }

        var quoteRun = 0;
        while (index + quoteRun < source.Length && source[index + quoteRun] == '"') quoteRun++;
        if (quoteRun >= 3)
        {
            return MaskRawString(source, index, quoteRun, masked, maskContent);
        }

        return MaskQuotedRun(source, index, '"', masked, escapes: !verbatim, maskContent);
    }

    private static int MaskQuotedRun(
        string source,
        int start,
        char quote,
        StringBuilder masked,
        bool escapes,
        bool maskContent)
    {
        masked.Append(quote);
        var index = start + 1;
        while (index < source.Length)
        {
            var current = source[index];
            if (escapes && current == '\\' && index + 1 < source.Length)
            {
                AppendLiteral(masked, source, index, 2, maskContent);
                index += 2;
                continue;
            }

            if (current == quote)
            {
                // In a verbatim string a doubled quote is an escaped quote, not the terminator.
                if (!escapes && index + 1 < source.Length && source[index + 1] == quote)
                {
                    AppendLiteral(masked, source, index, 2, maskContent);
                    index += 2;
                    continue;
                }

                masked.Append(quote);
                return index + 1;
            }

            AppendLiteral(masked, source, index, 1, maskContent);
            index++;
        }

        return index;
    }

    private static int MaskRawString(
        string source,
        int start,
        int quoteRun,
        StringBuilder masked,
        bool maskContent)
    {
        masked.Append(maskContent ? new string(' ', quoteRun) : source[start..(start + quoteRun)]);
        var index = start + quoteRun;
        while (index < source.Length)
        {
            if (source[index] == '"')
            {
                var run = 0;
                while (index + run < source.Length && source[index + run] == '"') run++;
                masked.Append(maskContent ? new string(' ', run) : source[index..(index + run)]);
                index += run;
                if (run >= quoteRun) return index;
                continue;
            }

            AppendLiteral(masked, source, index, 1, maskContent);
            index++;
        }

        return index;
    }

    private static void AppendLiteral(
        StringBuilder masked,
        string source,
        int start,
        int length,
        bool maskContent)
    {
        for (var index = start; index < start + length; index++)
        {
            if (maskContent) AppendMaskedCharacter(masked, source[index]);
            else masked.Append(source[index]);
        }
    }

    private static int MaskLineComment(string source, int start, StringBuilder masked)
    {
        var index = start;
        while (index < source.Length && source[index] is not ('\n' or '\r'))
        {
            masked.Append(' ');
            index++;
        }

        return index;
    }

    private static int MaskBlockComment(string source, int start, StringBuilder masked)
    {
        masked.Append(' ', 2);
        var index = start + 2;
        while (index < source.Length)
        {
            if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
            {
                masked.Append(' ', 2);
                return index + 2;
            }

            AppendMaskedCharacter(masked, source[index]);
            index++;
        }

        return index;
    }

    private static void AppendMaskedCharacter(StringBuilder masked, char character) =>
        masked.Append(character is '\n' or '\r' ? character : ' ');

    private sealed record SourceLine(int Offset, int Depth, string Text);

    private sealed record Declaration(int Offset, int Depth, string Key);

    private sealed record NamespaceDeclaration(int Offset, int Depth, int End, string Name);

    private sealed record TypeDeclaration(int Offset, int Depth, int End, string Name);

    private sealed record ConstantDeclaration(string Namespace, string TypePath, string Name, int InitializerStart)
    {
        public string FullPath => Namespace.Length == 0 ? TypePath : $"{Namespace}.{TypePath}";
    }

    private sealed record PragmaStatement(int Offset, int Depth, string Context, bool IsCertain, bool IsDisable, IReadOnlyList<string> Rules);

    private static List<SourceLine> ReadLines(string code)
    {
        var lines = new List<SourceLine>();
        var depth = 0;
        var start = 0;
        for (var position = 0; position <= code.Length; position++)
        {
            var atEnd = position == code.Length;
            if (!atEnd && code[position] is not ('\n' or '\r')) continue;

            var text = code[start..position];
            lines.Add(new SourceLine(start, depth, text));
            foreach (var character in text)
            {
                if (character == '{') depth++;
                else if (character == '}') depth--;
            }

            start = position + 1;
        }

        return lines;
    }

    private static List<Declaration> FindDeclarations(
        IReadOnlyList<SourceLine> lines,
        IReadOnlyList<TypeDeclaration> types,
        string code)
    {
        var declarations = new List<Declaration>();
        foreach (var line in lines)
        {
            var key = DeclarationKey(line, types, code);
            if (key is not null) declarations.Add(new Declaration(line.Offset, line.Depth, key));
        }

        return declarations;
    }

    /// <summary>
    /// The stable identity of a declaration: its namespace-qualified type path, the member name and
    /// the member's parameter list. The type path and the signature keep a suppression attributable
    /// to one member, so moving it between same-named members or between overloads is detected.
    /// Locals (including local constants) are not declarations a suppression can cover.
    /// </summary>
    private static string? DeclarationKey(
        SourceLine line,
        IReadOnlyList<TypeDeclaration> types,
        string code)
    {
        var trimmed = line.Text.TrimStart();
        if (trimmed.Length == 0) return null;

        var typeMatch = TypeDeclarationPattern.Match(trimmed);
        if (typeMatch.Success)
        {
            var path = JoinPath(TypePathAt(types, line.Offset, line.Depth), typeMatch.Groups["name"].Value);
            return $"type:{path}";
        }

        var memberMatch = MemberDeclarationPattern.Match(trimmed);
        if (!memberMatch.Success) return null;
        if (!IsDirectTypeMember(line, types)) return null;

        var typePath = TypePathAt(types, line.Offset, line.Depth);
        if (typePath.Length == 0) return null;

        var name = memberMatch.Groups["name"].Value;
        return $"member:{typePath}.{name}{MemberSignature(code, line, trimmed, memberMatch)}";
    }

    /// <summary>
    /// Whether the line is declared directly in a type body (a field, method or property) rather than
    /// inside a method or a nested scope, where a suppression cannot target it.
    /// </summary>
    private static bool IsDirectTypeMember(SourceLine line, IReadOnlyList<TypeDeclaration> types)
    {
        var enclosingDepth = types
            .Where(type => type.Offset < line.Offset && line.Offset < type.End && type.Depth < line.Depth)
            .Select(type => type.Depth)
            .DefaultIfEmpty(-1)
            .Max();

        return enclosingDepth >= 0 && line.Depth == enclosingDepth + 1;
    }

    /// <summary>
    /// The member's parameter list, whitespace-free, so overloads get distinct identities. A list that
    /// starts on the declaration line but continues on later lines is read to its matching
    /// parenthesis; a member without parentheses contributes an empty signature.
    /// </summary>
    private static string MemberSignature(string code, SourceLine line, string trimmed, Match memberMatch)
    {
        var nameEnd = memberMatch.Groups["name"].Index + memberMatch.Groups["name"].Length;
        if (nameEnd >= trimmed.Length) return string.Empty;

        var rest = trimmed[nameEnd..];
        var open = rest.IndexOf('(');
        if (open < 0) return string.Empty;

        var close = rest.IndexOf(')', open);
        if (close >= 0) return Compact(rest[open..(close + 1)]);

        var absoluteOpen = line.Offset + (line.Text.Length - trimmed.Length) + nameEnd + open;
        var matching = FindMatchingParenthesis(code, absoluteOpen);
        return matching >= 0 ? Compact(code[absoluteOpen..(matching + 1)]) : Compact(rest[open..]);
    }

    private static string Compact(string text) =>
        new(text.Where(character => !char.IsWhiteSpace(character)).ToArray());

    private static string JoinPath(string prefix, string name) =>
        prefix.Length == 0 ? name : $"{prefix}.{name}";

    private static List<(string Rule, int Start, int End, int Depth, string Context)> FindPragmaRegions(
        IReadOnlyList<SourceLine> lines,
        int codeLength,
        IReadOnlyList<ConditionalLine> conditionalStates)
    {
        var directives = new List<PragmaStatement>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var match = PragmaDirectivePattern.Match(line.Text);
            if (!match.Success) continue;

            // A directive in a provably inactive #if branch is not compiled, so it neither suppresses
            // nor opens a region.
            if (!conditionalStates[index].IsActive) continue;

            directives.Add(new PragmaStatement(
                line.Offset,
                line.Depth,
                conditionalStates[index].Chain,
                conditionalStates[index].IsCertain,
                match.Groups["action"].Value == "disable",
                ParseRules(match.Groups["rules"].Value)));
        }

        var open = new Dictionary<string, Stack<(int Start, int Depth, string Context)>>(StringComparer.Ordinal);
        var regions = new List<(string Rule, int Start, int End, int Depth, string Context)>();
        foreach (var directive in directives.OrderBy(directive => directive.Offset))
        {
            if (directive.IsDisable)
            {
                foreach (var rule in directive.Rules)
                {
                    if (!open.TryGetValue(rule, out var stack))
                    {
                        stack = new Stack<(int Start, int Depth, string Context)>();
                        open[rule] = stack;
                    }

                    stack.Push((directive.Offset, directive.Depth, directive.Context));
                }

                continue;
            }

            // A restore inside a branch that may not be compiled cannot close every path: if the
            // branch is skipped the suppression keeps widening, so the region is left open.
            if (!directive.IsCertain) continue;

            var closingRules = directive.Rules.Contains(AnySuppression)
                ? open.Keys.ToList()
                : directive.Rules;
            foreach (var rule in closingRules)
            {
                if (!open.TryGetValue(rule, out var stack) || stack.Count == 0) continue;
                var (start, depth, context) = stack.Pop();
                regions.Add((rule, start, directive.Offset, depth, context));
            }
        }

        foreach (var (rule, stack) in open)
        {
            while (stack.Count > 0)
            {
                var (start, depth, context) = stack.Pop();
                regions.Add((rule, start, codeLength, depth, context));
            }
        }

        return regions;
    }

    private static List<string> ParseRules(string arguments)
    {
        var ids = arguments.Split(
            [',', ' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0)
        {
            // `#pragma warning disable` without ids disables every warning in the block.
            return [AnySuppression];
        }

        return ids.Where(IsGatedRule).Select(id => id.ToUpperInvariant()).ToList();
    }

    private static IEnumerable<(int Start, int End, string Arguments, string AttributeTarget)> FindSuppressMessageAttributes(
        string code,
        string argumentsCode,
        IReadOnlyCollection<string> aliases)
    {
        var names = new List<string> { "SuppressMessage", "SuppressMessageAttribute" };
        names.AddRange(aliases);

        var pattern = new Regex(
            $@"(?<![\w.])(?:[A-Za-z_][A-Za-z0-9_]*\.)*(?:{string.Join("|", names.Select(Regex.Escape))})\s*\(",
            RegexOptions.CultureInvariant);

        foreach (Match match in pattern.Matches(code))
        {
            var openParenthesis = match.Index + match.Length - 1;
            var closeParenthesis = FindMatchingParenthesis(code, openParenthesis);
            if (closeParenthesis < 0) continue;

            yield return (match.Index, closeParenthesis, argumentsCode[(openParenthesis + 1)..closeParenthesis],
                AttributeUsageTarget(code, match.Index));
        }
    }

    /// <summary>
    /// The usage target of an attribute (<c>assembly</c>, <c>module</c>, ...), or an empty string for
    /// an attribute attached to a declaration. The target is part of the suppression identity, so a
    /// suppression moved between usage targets is not treated as unchanged.
    /// </summary>
    private static string AttributeUsageTarget(string code, int nameIndex)
    {
        var openBracket = code.LastIndexOf('[', nameIndex);
        if (openBracket < 0) return string.Empty;

        var match = AttributeUsageTargetPattern.Match(code[openBracket..nameIndex]);
        return match.Success ? match.Groups["target"].Value : string.Empty;
    }

    /// <summary>
    /// Aliases that name <c>SuppressMessageAttribute</c>, so an attribute written through an alias is
    /// still recognized. With <paramref name="globalOnly"/>, only <c>global using</c> aliases are
    /// returned: those are the ones another file of the project can use.
    /// </summary>
    private static List<string> FindSuppressMessageAliases(string code, bool globalOnly = false)
    {
        var pattern = globalOnly ? GlobalSuppressMessageAliasPattern : SuppressMessageAliasPattern;
        var aliases = new List<string>();
        foreach (Match match in pattern.Matches(code))
        {
            aliases.Add(match.Groups["alias"].Value);
        }

        return aliases;
    }

    /// <summary>
    /// A named argument of an attribute (<c>Name = value</c> or <c>Name: value</c>), or <c>null</c>.
    /// </summary>
    private static string? ReadNamedArgument(string arguments, string name)
    {
        foreach (var argument in SplitArguments(arguments))
        {
            if (TryStripArgumentName(argument.Trim(), name, out var value)) return value;
        }

        return null;
    }

    private static string? ReadRuleArgument(string arguments)
    {
        var positional = new List<string>();
        string? namedCheckId = null;
        foreach (var argument in SplitArguments(arguments))
        {
            var trimmed = argument.Trim();
            if (trimmed.Length == 0) continue;

            if (TryStripArgumentName(trimmed, "checkId", out var checkId))
            {
                namedCheckId = checkId;
                continue;
            }

            if (TryStripArgumentName(trimmed, "category", out var category))
            {
                positional.Add(category);
                continue;
            }

            positional.Add(trimmed);
        }

        return namedCheckId ?? (positional.Count >= 2 ? positional[1] : null);
    }

    /// <summary>
    /// Decodes one plain or verbatim string literal into the value the compiler sees, including escape
    /// sequences such as <c>\u0043</c>. Returns <c>false</c> for anything else (a concatenation, a
    /// constant, an interpolated string, or an escape the scanner cannot resolve), so the caller can
    /// evaluate it as an expression or reject it.
    /// </summary>
    private static bool TryDecodeStringLiteral(string argument, out string value)
    {
        value = string.Empty;
        var trimmed = argument.Trim();
        if (trimmed.Length == 0) return false;

        var offset = trimmed.StartsWith('@') ? 1 : 0;
        if (offset >= trimmed.Length || trimmed[offset] != '"') return false;

        var end = MaskLiteral(trimmed, 0, new StringBuilder(), maskContent: true);
        if (end <= offset + 1 || trimmed[end..].Trim().Length != 0) return false;

        // Raw string literal ("""..."""), possibly with more delimiter quotes. A single-line raw
        // string decodes to its content verbatim; the indentation rules of a multi-line raw string are
        // not modelled, so it is rejected rather than decoded into a wrong value.
        var quoteRun = 0;
        while (offset + quoteRun < trimmed.Length && trimmed[offset + quoteRun] == '"') quoteRun++;
        if (quoteRun >= 3)
        {
            var contentEnd = end - quoteRun;
            if (contentEnd < offset + quoteRun) return false;

            var rawContent = trimmed[(offset + quoteRun)..contentEnd];
            if (rawContent.Contains('\n') || rawContent.Contains('\r')) return false;

            value = rawContent;
            return true;
        }

        var content = trimmed[(offset + 1)..(end - 1)];
        if (offset == 1)
        {
            // A verbatim string spells an embedded quote as two quotes.
            value = content.Replace("\"\"", "\"", StringComparison.Ordinal);
            return true;
        }

        var decoded = new StringBuilder(content.Length);
        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (current != '\\')
            {
                decoded.Append(current);
                continue;
            }

            if (index + 1 >= content.Length) return false;
            var escape = content[++index];
            switch (escape)
            {
                case '\\': decoded.Append('\\'); break;
                case '"': decoded.Append('"'); break;
                case '\'': decoded.Append('\''); break;
                case '0': decoded.Append('\0'); break;
                case 'a': decoded.Append('\a'); break;
                case 'b': decoded.Append('\b'); break;
                case 'f': decoded.Append('\f'); break;
                case 'n': decoded.Append('\n'); break;
                case 'r': decoded.Append('\r'); break;
                case 't': decoded.Append('\t'); break;
                case 'v': decoded.Append('\v'); break;
                case 'u':
                    if (!TryReadHex(content, ref index, 4, out var utf16)) return false;
                    decoded.Append((char)utf16);
                    break;
                case 'U':
                    if (!TryReadHex(content, ref index, 8, out var codePoint)) return false;
                    if (codePoint > 0x10FFFF || codePoint is >= 0xD800 and <= 0xDFFF) return false;
                    decoded.Append(char.ConvertFromUtf32(codePoint));
                    break;
                case 'x':
                    if (!TryReadHex(content, ref index, 4, out var hex, allowShorter: true)) return false;
                    decoded.Append((char)hex);
                    break;
                default:
                    return false;
            }
        }

        value = decoded.ToString();
        return true;
    }

    private static bool TryReadHex(string content, ref int index, int digits, out int value, bool allowShorter = false)
    {
        value = 0;
        var consumed = 0;
        while (consumed < digits
               && index + 1 < content.Length
               && Uri.IsHexDigit(content[index + 1]))
        {
            value = value * 16 + Convert.ToInt32(content[index + 1].ToString(), 16);
            index++;
            consumed++;
        }

        return consumed == digits || (allowShorter && consumed > 0);
    }

    private static bool TryStripArgumentName(string argument, string name, out string value)
    {
        value = string.Empty;
        if (!argument.StartsWith(name, StringComparison.Ordinal)) return false;

        var rest = argument[name.Length..].TrimStart();
        if (rest.Length == 0 || (rest[0] != ':' && rest[0] != '=')) return false;

        value = rest[1..].Trim();
        return true;
    }

    private static List<string> SplitArguments(string arguments) => SplitTopLevel(arguments, ',');

    /// <summary>
    /// Splits at a separator that appears outside parentheses/brackets/braces and string literals.
    /// </summary>
    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (IsLiteralStart(text, index))
            {
                var end = MaskLiteral(text, index, new StringBuilder(), maskContent: true);
                if (end > index)
                {
                    index = end - 1;
                    continue;
                }
            }

            switch (text[index])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    break;
                case var current when current == separator && depth == 0:
                    parts.Add(text[start..index]);
                    start = index + 1;
                    break;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static int FindMatchingParenthesis(string code, int openIndex)
    {
        var depth = 0;
        for (var index = openIndex; index < code.Length; index++)
        {
            switch (code[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0) return index;
                    break;
            }
        }

        return -1;
    }

    private static void Increment(
        SortedDictionary<(string Rule, string Scope), int> counts,
        string rule,
        string scope)
    {
        counts[(rule, scope)] = counts.TryGetValue((rule, scope), out var count) ? count + 1 : 1;
    }

    private static bool IsGatedRule(string rule) =>
        GatedRules.Contains(rule, StringComparer.OrdinalIgnoreCase);

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
        var entries = new List<AllowlistEntry>();
        var path = Path.Combine(repoRoot, ExclusionsFile);
        if (!File.Exists(path))
        {
            return new Exclusions(entries);
        }

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            // `path:RULE` allows every finding of that rule in the file; `path:RULE:symbol` only
            // the finding whose message names the quoted symbol (for example 'ProcessCommand').
            var columns = line.Split(':', 3);
            if (columns.Length < 2 || columns[0].Length == 0 || columns[1].Length == 0)
            {
                Console.Error.WriteLine($"WARNING: ignoring invalid exclusion line '{line}' in {ExclusionsFile}");
                continue;
            }

            var symbol = columns.Length == 3 && columns[2].Trim().Length > 0 ? columns[2].Trim() : null;
            entries.Add(new AllowlistEntry(NormalizePath(columns[0].Trim()), columns[1].Trim(), symbol));
        }

        return new Exclusions(entries);
    }

    private static string Relativize(string path)
    {
        var normalized = NormalizePath(path);
        var marker = normalized.LastIndexOf("/src/", StringComparison.Ordinal);
        return marker >= 0 ? normalized[(marker + 1)..] : normalized;
    }

    /// <summary>
    /// The separator-independent spelling of a path. Path semantics (relative segments, absolute
    /// paths, case) are handled by <see cref="Path"/>; this only picks "/" as the one spelling used
    /// for comparisons and messages, because git, the allowlist file and the analyzer report do not
    /// agree on a separator. On Unix a backslash is a legal filename character, so only the
    /// platform's directory separator is rewritten.
    /// </summary>
    private static string NormalizePath(string path) =>
        Path.DirectorySeparatorChar == Path.AltDirectorySeparatorChar
            ? path
            : path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// A repository-relative path for an explicit argument, so `./src/x.cs`, an absolute path and
    /// `src/x.cs` all address the same file instead of being skipped as outside the library.
    /// <see cref="Path.GetFullPath(string, string)"/> anchors the argument to the repository root,
    /// and <see cref="Path.GetRelativePath"/> resolves it back to the form the rest of the gate uses.
    /// </summary>
    internal static string CanonicalizePath(string repoRoot, string path) =>
        NormalizePath(Path.GetRelativePath(repoRoot, Path.GetFullPath(path, repoRoot)));

    private static IEnumerable<string> NullSeparated(string text) =>
        text.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static bool TryCaptureGit(string? workingDirectory, IReadOnlyList<string> arguments, out string output)
    {
        var result = Run("git", arguments, workingDirectory);
        if (result.ExitCode == 0)
        {
            output = result.StandardOutput;
            return true;
        }

        var error = result.StandardError.Trim();
        Console.Error.WriteLine($"ERROR: git {string.Join(' ', arguments)} failed (exit {result.ExitCode}).");
        if (error.Length > 0) Console.Error.WriteLine(error);
        output = string.Empty;
        return false;
    }

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

    private sealed record AllowlistEntry(string File, string Rule, string? Symbol)
    {
        public override string ToString() => Symbol is null ? $"{File}:{Rule}" : $"{File}:{Rule}:{Symbol}";
    }

    private sealed class Exclusions(IReadOnlyList<AllowlistEntry> entries)
    {
        private readonly HashSet<int> _matched = [];

        public bool IsExcluded(Finding finding)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!entry.File.Equals(finding.File, StringComparison.Ordinal)) continue;
                if (!entry.Rule.Equals(finding.Rule, StringComparison.Ordinal)) continue;
                if (entry.Symbol is { } symbol
                    && !finding.Message.Contains($"'{symbol}'", StringComparison.Ordinal))
                {
                    continue;
                }

                _matched.Add(index);
                return true;
            }

            return false;
        }

        public IEnumerable<AllowlistEntry> Unmatched(IReadOnlySet<string> analyzedFiles) =>
            entries.Where((entry, index) =>
                !_matched.Contains(index) && analyzedFiles.Contains(entry.File));
    }
}
