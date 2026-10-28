using MakaMek.Tools.MW5Extractor.Discovery;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Animation;

/// <summary>
/// Builds the per-chassis clip inventory: which canonical clips exist, which asset supplies each one, and which
/// canonical clips MW5 never shipped.
/// </summary>
/// <remarks>
/// The default pass is path-only, because enumeration is cheap and decoding every candidate is not. <see cref="Probe"/>
/// upgrades the inventory with real frame counts, fps and loop closure for the clips that were chosen.
/// </remarks>
public static class ClipInventory
{
    public static ClipInventoryReport Build(UnrealArchive archive, string filter, ClipVocabulary vocabulary)
    {
        var discovery = ChassisDiscovery.Build(archive, filter);
        var chassis = ChassisDiscovery.Select(discovery.Chassis, filter);
        var animations = FindAnimations(archive, chassis.Folder);

        var warnings = new List<string>();
        var clips = new List<ClipEntry>();
        var ignored = new List<string>();

        foreach (var animation in animations)
        {
            // Excluded aliases are still part of the inventory: the report lists them as ignored, never drops them.
            if (vocabulary.IsExcluded(animation.Name) || vocabulary.Match(animation.Name) is null)
            {
                ignored.Add(animation.Name);
            }
        }

        foreach (var definition in vocabulary.Clips)
        {
            var candidates = animations
                .Where(animation => !vocabulary.IsExcluded(animation.Name))
                .Select(animation => (Animation: animation, Match: vocabulary.Match(animation.Name)))
                .Where(candidate => candidate.Match is not null &&
                                    string.Equals(candidate.Match.Value.Definition.Name, definition.Name,
                                        StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.Match!.Value.Rank)
                .ThenBy(candidate => candidate.Animation.Path, StringComparer.Ordinal)
                .ToList();

            var chosen = candidates.Count > 0 ? candidates[0].Animation : default;
            if (chosen.Name is null)
            {
                warnings.Add($"{definition.Name}: no third-person clip matched {string.Join(", ", definition.Patterns)}");
            }

            clips.Add(new ClipEntry
            {
                Name = definition.Name,
                Status = chosen.Name is null ? "missing" : "available",
                AssetPath = chosen.Name is null ? null : chosen.Path,
                Candidates = candidates
                    .Select(candidate => candidate.Animation.Path)
                    .ToList(),
                Loop = definition.Loop,
                Hold = definition.Hold
            });
        }

        return new ClipInventoryReport
        {
            Chassis = chassis.Folder,
            Variant = string.Equals(chassis.Folder, filter, StringComparison.OrdinalIgnoreCase) ? null : filter,
            Clips = clips,
            Absent = vocabulary.Absent
                .Select(pair => new AbsentClip { Name = pair.Key, Reason = pair.Value })
                .OrderBy(clip => clip.Name, StringComparer.Ordinal)
                .ToList(),
            Ignored = ignored
                .Select(name => name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            Warnings = warnings
        };
    }

    /// <summary>
    /// Decodes every available clip and fills in frame counts, fps, duration, animated bones, root travel and loop
    /// closure. A clip that fails to decode is reported as <c>error</c>, never silently dropped.
    /// </summary>
    public static ClipInventoryReport Probe(UnrealArchive archive, ClipInventoryReport report,
        ClipVocabulary vocabulary, int shippedFrames = 10)
    {
        var clips = new List<ClipEntry>();
        var warnings = new List<string>(report.Warnings);

        foreach (var entry in report.Clips)
        {
            if (entry.AssetPath is null)
            {
                clips.Add(entry);
                continue;
            }

            var definition = vocabulary.Clips.FirstOrDefault(clip =>
                string.Equals(clip.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
            if (definition is null)
            {
                clips.Add(entry);
                continue;
            }

            try
            {
                var sample = AnimationClipReader.Read(archive, definition, entry.AssetPath, shippedFrames: shippedFrames);
                clips.Add(sample.Entry with { Candidates = entry.Candidates, Status = "available" });
                warnings.AddRange(sample.Entry.Warnings);
            }
            catch (Exception exception)
            {
                clips.Add(entry with
                {
                    Status = "error",
                    Warnings = [exception.Message]
                });
                warnings.Add($"{entry.Name}: {exception.Message}");
            }
        }

        return report with { Clips = clips, Warnings = warnings };
    }

    private static List<(string Name, string Path)> FindAnimations(UnrealArchive archive, string chassisFolder)
    {
        var animations = new List<(string Name, string Path)>();
        foreach (var file in archive.Files)
        {
            var parsed = Mw5AssetPath.TryParse(file.Path);
            if (parsed is { Kind: Mw5AssetKind.Animation } &&
                string.Equals(parsed.ChassisFolder, chassisFolder, StringComparison.OrdinalIgnoreCase))
            {
                animations.Add((parsed.Name, parsed.VirtualPath));
            }
        }

        return animations
            .Distinct()
            .OrderBy(animation => animation.Path, StringComparer.Ordinal)
            .ToList();
    }
}
