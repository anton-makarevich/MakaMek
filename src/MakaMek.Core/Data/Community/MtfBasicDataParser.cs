using System.Text.RegularExpressions;

namespace Sanet.MakaMek.Core.Data.Community;

/// <summary>
/// Accumulates the header key/value pairs of an MTF file, splitting the model field into model and
/// nickname and numbering the repeated quirk and system entries. Extracted from
/// <see cref="MtfDataProvider"/> to keep the per-line parsing flat.
/// </summary>
internal sealed partial class MtfBasicDataParser
{
    private readonly Dictionary<string, string> _mechData = new(StringComparer.OrdinalIgnoreCase);
    private int _quirksCount;
    private int _systemsCount;

    [GeneratedRegex(@"^([^']+)'([^']+)'$")]
    private static partial Regex NicknameSingleQuotePattern();

    [GeneratedRegex(@"^([^(]+)\(([^)]+)\)$")]
    private static partial Regex NicknameParenPattern();

    public static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var parser = new MtfBasicDataParser();
        foreach (var line in lines)
            parser.Add(line);
        return parser._mechData;
    }

    private void Add(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("Config:", StringComparison.Ordinal))
            return;

        var colonIndex = line.IndexOf(':');
        if (colonIndex <= 0) return;

        var key = line[..colonIndex].ToLowerInvariant().Trim();
        var value = line[(colonIndex + 1)..].Trim();

        if (key.Equals("engine"))
            AddEngine(value);
        else
            AddEntry(key, value);
    }

    private void AddEngine(string value)
    {
        var engineData = value.Replace("Fusion Engine(IS)", "Fusion Engine").Split(' ');
        if (engineData.Length < 2) return;

        _mechData["engine-rating"] = engineData[0];
        _mechData["engine-type"] = engineData[1];
    }

    private void AddEntry(string key, string value)
    {
        if (key.StartsWith("quirk", StringComparison.Ordinal))
            key = $"{key}{++_quirksCount}";
        if (key.StartsWith("system", StringComparison.Ordinal))
            key = $"{key}{++_systemsCount}";
        if (key.StartsWith("model", StringComparison.Ordinal))
        {
            AddModel(value);
            return;
        }

        _mechData[key] = value;
    }

    /// <summary>
    /// Extracts the model and nickname from the model field (format: "MODEL 'NICKNAME'" or
    /// "MODEL (NICKNAME)").
    /// </summary>
    private void AddModel(string value)
    {
        var (model, nickname) = ExtractModelAndNickname(value);
        _mechData["model"] = model;
        if (!string.IsNullOrEmpty(nickname))
            _mechData["nickname"] = nickname;
    }

    private static (string model, string? nickname) ExtractModelAndNickname(string modelNickname)
    {
        var model = modelNickname;
        string? nickname = null;

        var match = NicknameSingleQuotePattern().Match(model);
        if (match.Success)
        {
            model = match.Groups[1].Value.Trim();
            nickname = match.Groups[2].Value.Trim();
        }
        else
        {
            match = NicknameParenPattern().Match(model);
            if (match.Success)
            {
                model = match.Groups[1].Value.Trim();
                nickname = match.Groups[2].Value.Trim();
            }
        }

        return (model, nickname);
    }
}
