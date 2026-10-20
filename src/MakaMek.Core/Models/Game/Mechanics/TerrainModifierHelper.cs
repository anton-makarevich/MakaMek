using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Shared extraction of terrain to-hit modifiers from a line-of-sight result.
/// Used by both the weapon and physical attack to-hit calculators.
/// </summary>
internal static class TerrainModifierHelper
{
    /// <summary>
    /// Collects terrain to-hit modifiers for all hexes along the line-of-sight path that
    /// contribute an intervening factor. Only terrains with a non-zero modifier are included.
    /// </summary>
    /// <param name="losResult">The line-of-sight result whose path is inspected</param>
    /// <param name="rules">Rules provider supplying per-terrain to-hit modifiers</param>
    public static IReadOnlyList<TerrainRollModifier> GetTerrainModifiers(
        LineOfSightResult losResult,
        IRulesProvider rules)
    {
        var modifiers = new List<TerrainRollModifier>();
        foreach (var hexInfo in losResult.HexPath)
        {
            if (hexInfo.InterveningFactor <= 0) continue;
            foreach (var terrain in hexInfo.Hex.GetTerrains())
            {
                var modifier = rules.GetTerrainToHitModifier(terrain.Id);
                if (modifier != 0)
                    modifiers.Add(new TerrainRollModifier
                    {
                        Value = modifier,
                        Location = hexInfo.Hex.Coordinates,
                        TerrainId = terrain.Id
                    });
            }
        }
        return modifiers;
    }
}
