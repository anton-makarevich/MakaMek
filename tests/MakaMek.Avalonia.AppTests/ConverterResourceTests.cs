using System.Text.RegularExpressions;
using global::Avalonia;
using Sanet.MakaMek.Avalonia;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// Every converter a markup file reaches for has to actually exist when the real application runs.
///
/// This is here because a missing one is invisible to the rest of the suite: the Avalonia tests
/// boot a stub App with its own resource dictionary, so a converter registered only there passes
/// every view test and then throws KeyNotFoundException the moment the real app builds the view.
/// That happened with NullToBooleanConverter in UnitRecordSheet, and the only thing that caught it
/// was booting the real App.
///
/// A reference is satisfied either by the application's resources or by the file's own
/// UserControl.Resources, which is how several panels declare the converters only they use.
/// </summary>
public class ConverterResourceTests
{
    private static readonly Regex StaticResourceConverter =
        new(@"StaticResource\s+(?<key>\w*Converter)\b", RegexOptions.Compiled);

    private static readonly Regex LocallyDeclared =
        new(@"x:Key\s*=\s*""(?<key>\w*Converter)""", RegexOptions.Compiled);

    [Fact]
    public Task EveryConverterTheMarkupUses_ResolvesInTheRealApp() => HarnessSession.Run(() =>
    {
        var app = (App)Application.Current!;
        var unresolved = new List<string>();
        var checkedFiles = 0;

        foreach (var file in Directory.EnumerateFiles(MarkupRoot(), "*.axaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var markup = File.ReadAllText(file);
            checkedFiles++;

            var declaredHere = LocallyDeclared.Matches(markup)
                .Select(match => match.Groups["key"].Value)
                .ToHashSet();

            foreach (var key in StaticResourceConverter.Matches(markup)
                         .Select(match => match.Groups["key"].Value)
                         .Distinct())
            {
                if (declaredHere.Contains(key)) continue;
                if (app.Resources.ContainsKey(key)) continue;

                unresolved.Add($"{Path.GetFileName(file)} uses {{StaticResource {key}}}");
            }
        }

        checkedFiles.ShouldBeGreaterThan(10, "the markup should have been found and scanned");
        unresolved.ShouldBeEmpty(
            "these would throw KeyNotFoundException when the view is built: "
            + string.Join("; ", unresolved));
    });

    /// <summary>
    /// The Avalonia source tree, found by walking up from the test binaries the way the game data
    /// is. Keyed on the folder itself so a wrong answer fails here rather than as an empty scan.
    /// </summary>
    private static string MarkupRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MakaMek.Avalonia");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"src/MakaMek.Avalonia not found above {AppContext.BaseDirectory}");
    }
}
