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
                explicitFiles.Add(NormalizePath(arg));
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

        // A diff must not hide new debt behind a suppression the base ref did not have.
        var suppressionFailures = FindNewSuppressions(
            repoRoot, baseRef, projects.Values.SelectMany(files => files));
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
    /// Suppressions a changed file added relative to the base ref.
    /// </summary>
    private static List<string> FindNewSuppressions(string repoRoot, string baseRef, IEnumerable<string> files)
    {
        var failures = new List<string>();
        foreach (var file in files)
        {
            var path = Path.Combine(repoRoot, file);
            if (!File.Exists(path)) continue;

            failures.AddRange(FindNewSuppressionsIn(
                file,
                ReadAtBase(repoRoot, baseRef, file),
                File.ReadAllText(path)));
        }

        return failures;
    }

    /// <summary>
    /// The suppressions a file added relative to the base ref. Each suppression is identified by its
    /// rule and by the declaration it covers, so a pre-existing suppression authorizes neither a
    /// second occurrence, nor the same rule moved to another member, nor a wider region created by a
    /// removed restore. Renaming a suppressed declaration therefore also fails the gate: the member
    /// is expected to lose its suppression in the change that moves it. A suppression whose rule
    /// argument cannot be resolved to a literal value always fails, because the gate cannot tell
    /// which rule it hides.
    /// </summary>
    internal static List<string> FindNewSuppressionsIn(string file, string? baselineSource, string currentSource)
    {
        var failures = new List<string>();
        var current = ScanSuppressions(currentSource);

        foreach (var unresolvable in current.Unresolvable)
        {
            failures.Add(
                $"{file}({unresolvable.Line}): the suppression rule argument `{unresolvable.Expression}` " +
                $"cannot be resolved (covering {unresolvable.Scope}), so the gate cannot verify which rule it hides. " +
                "Use a single string literal or constants the gate can read.");
        }

        if (current.Scopes.Count == 0) return failures;

        var baseline = ScanSuppressions(baselineSource).Scopes;
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

    private sealed record UnresolvableSuppression(int Line, string Scope, string Expression);

    /// <summary>
    /// Every gated-rule suppression in the source, keyed by rule and by the declaration it covers.
    /// Comments and literals are masked first, so text that only mentions a directive or an attribute
    /// cannot count, and a suppression is attributed to the declaration it applies to rather than to
    /// a line number that every edit would move.
    /// </summary>
    internal static SortedDictionary<(string Rule, string Scope), int> SuppressionScopes(string? source) =>
        ScanSuppressions(source).Scopes;

    private static SuppressionScan ScanSuppressions(string? source)
    {
        var scopes = new SortedDictionary<(string Rule, string Scope), int>();
        var unresolvable = new List<UnresolvableSuppression>();
        if (string.IsNullOrEmpty(source)) return new SuppressionScan(scopes, unresolvable);

        var code = MaskLiteralsAndComments(source);
        // Attribute arguments hold the rule id as a string literal, so they are read from a variant
        // that keeps literals and only removes comments.
        var argumentsCode = MaskComments(source);
        var lines = ReadLines(code);
        var declarations = FindDeclarations(lines);

        foreach (var (rule, start, end, depth) in FindPragmaRegions(lines, code.Length))
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
                    Increment(scopes, suppressedRule, scope);
                }
            }
        }

        foreach (var attribute in FindSuppressMessageAttributes(code, argumentsCode))
        {
            var scope = ScopeAfter(declarations, attribute.End);
            var ruleArgument = ReadRuleArgument(attribute.Arguments);
            if (ruleArgument is null) continue;

            if (!TryEvaluateRuleArgument(ruleArgument, argumentsCode, out var ruleText))
            {
                unresolvable.Add(new UnresolvableSuppression(LineAt(code, attribute.Start), scope, ruleArgument));
                continue;
            }

            foreach (Match id in RuleIdInAttributePattern.Matches(ruleText))
            {
                if (IsGatedRule(id.Groups["id"].Value))
                {
                    Increment(scopes, id.Groups["id"].Value.ToUpperInvariant(), scope);
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
    private static bool TryEvaluateRuleArgument(string expression, string source, out string value) =>
        TryEvaluateExpression(expression, source, 0, out value);

    private static bool TryEvaluateExpression(string expression, string source, int depth, out string value)
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
            if (!TryResolveStringConstant(operand, source, depth, out var constant)) return false;
            builder.Append(constant);
        }

        value = builder.ToString();
        return true;
    }

    private static bool TryResolveStringConstant(string path, string source, int depth, out string value)
    {
        value = string.Empty;
        var name = path[(path.LastIndexOf('.') + 1)..];

        foreach (Match declaration in ConstantDeclarationPattern.Matches(source))
        {
            if (!string.Equals(declaration.Groups["name"].Value, name, StringComparison.Ordinal)) continue;

            var start = declaration.Index + declaration.Length;
            var end = FindStatementEnd(source, start);
            if (end < 0) continue;

            return TryEvaluateExpression(source[start..end], source, depth + 1, out value);
        }

        return false;
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
        if (scope == NoDeclaration) return null;

        var lines = ReadLines(MaskLiteralsAndComments(source));
        for (var index = 0; index < lines.Count; index++)
        {
            if (DeclarationKey(lines[index].Text) == scope) return index + 1;
        }

        return null;
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

    private sealed record PragmaStatement(int Offset, int Depth, bool IsDisable, IReadOnlyList<string> Rules);

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

    private static List<Declaration> FindDeclarations(IReadOnlyList<SourceLine> lines)
    {
        var declarations = new List<Declaration>();
        foreach (var line in lines)
        {
            var key = DeclarationKey(line.Text);
            if (key is not null) declarations.Add(new Declaration(line.Offset, line.Depth, key));
        }

        return declarations;
    }

    private static string? DeclarationKey(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return null;

        var typeMatch = TypeDeclarationPattern.Match(trimmed);
        if (typeMatch.Success) return $"type:{typeMatch.Groups["name"].Value}";

        var memberMatch = MemberDeclarationPattern.Match(trimmed);
        return memberMatch.Success ? $"member:{memberMatch.Groups["name"].Value}" : null;
    }

    private static List<(string Rule, int Start, int End, int Depth)> FindPragmaRegions(
        IReadOnlyList<SourceLine> lines,
        int codeLength)
    {
        var directives = new List<PragmaStatement>();
        foreach (var line in lines)
        {
            var match = PragmaDirectivePattern.Match(line.Text);
            if (!match.Success) continue;

            directives.Add(new PragmaStatement(
                line.Offset,
                line.Depth,
                match.Groups["action"].Value == "disable",
                ParseRules(match.Groups["rules"].Value)));
        }

        var open = new Dictionary<string, Stack<(int Start, int Depth)>>(StringComparer.Ordinal);
        var regions = new List<(string Rule, int Start, int End, int Depth)>();
        foreach (var directive in directives.OrderBy(directive => directive.Offset))
        {
            if (directive.IsDisable)
            {
                foreach (var rule in directive.Rules)
                {
                    if (!open.TryGetValue(rule, out var stack))
                    {
                        stack = new Stack<(int Start, int Depth)>();
                        open[rule] = stack;
                    }

                    stack.Push((directive.Offset, directive.Depth));
                }

                continue;
            }

            var closingRules = directive.Rules.Contains(AnySuppression)
                ? open.Keys.ToList()
                : directive.Rules;
            foreach (var rule in closingRules)
            {
                if (!open.TryGetValue(rule, out var stack) || stack.Count == 0) continue;
                var (start, depth) = stack.Pop();
                regions.Add((rule, start, directive.Offset, depth));
            }
        }

        foreach (var (rule, stack) in open)
        {
            while (stack.Count > 0)
            {
                var (start, depth) = stack.Pop();
                regions.Add((rule, start, codeLength, depth));
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

    private static IEnumerable<(int Start, int End, string Arguments)> FindSuppressMessageAttributes(
        string code,
        string argumentsCode)
    {
        const string attributeName = "SuppressMessage";
        var searchIndex = 0;
        while (searchIndex < code.Length)
        {
            var nameIndex = code.IndexOf(attributeName, searchIndex, StringComparison.Ordinal);
            if (nameIndex < 0) yield break;

            var openParenthesis = code.IndexOf('(', nameIndex + attributeName.Length);
            if (openParenthesis < 0) yield break;

            var closeParenthesis = FindMatchingParenthesis(code, openParenthesis);
            if (closeParenthesis < 0)
            {
                searchIndex = nameIndex + 1;
                continue;
            }

            yield return (nameIndex, closeParenthesis, argumentsCode[(openParenthesis + 1)..closeParenthesis]);
            searchIndex = closeParenthesis + 1;
        }
    }

    /// <summary>
    /// Reads the <c>checkId</c> argument of a <c>[SuppressMessage]</c> attribute. <c>NonLiteral</c> is
    /// returned when the argument is not exactly one string literal (a concatenation, a constant, an
    /// interpolated string): the compiler still honours such a suppression, so the gate records it
    /// instead of silently missing it.
    /// </summary>
    /// <summary>
    /// The <c>checkId</c> argument of a <c>[SuppressMessage]</c> attribute: positional or named, with
    /// nested parentheses and string literals respected. Returns <c>null</c> when the attribute does
    /// not carry a rule argument.
    /// </summary>
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
        if (rest.Length == 0 || rest[0] != ':') return false;

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
        var normalized = path.Replace('\\', '/');
        var marker = normalized.LastIndexOf("/src/", StringComparison.Ordinal);
        return marker >= 0 ? normalized[(marker + 1)..] : normalized;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

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
