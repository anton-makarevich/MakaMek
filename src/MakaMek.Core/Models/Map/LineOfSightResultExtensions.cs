using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Map;

/// <summary>
/// Extension methods for LineOfSightResult
/// </summary>
public static class LineOfSightResultExtensions
{
    extension(LineOfSightResult losResult)
    {
        /// <summary>
        /// Collects terrain to-hit modifiers for all hexes along the line-of-sight path that
        /// contribute an intervening factor. Only terrains with a non-zero modifier are included.
        /// </summary>
        /// <param name="rules">Rules provider supplying per-terrain to-hit modifiers</param>
        /// <returns>Terrain modifiers contributed by the hexes on the line-of-sight path</returns>
        public IReadOnlyList<TerrainRollModifier> GetTerrainToHitModifiers(IRulesProvider rules)
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
}
