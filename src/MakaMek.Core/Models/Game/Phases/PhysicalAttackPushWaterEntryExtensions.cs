using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

/// <summary>
/// The water-entry modifier a push adds to its own PushHit piloting skill roll.
/// </summary>
internal static class PhysicalAttackPushWaterEntryExtensions
{
    extension(IUnit target)
    {
        /// <summary>
        /// Adds the standard water-entry modifier to the PushHit PSR when the target was displaced
        /// into water. Entering water is not a separate roll (Total Warfare, p. 148, 151).
        /// </summary>
        public void ApplyPushWaterEntryModifier(IBattleMap map, PhysicalAttackPhaseState state)
        {
            if (!state.PushHitContexts.TryGetValue(target.Id, out var entry)) return;
            if (target.Position is not { Surface: HexSurface.Ground } position) return;

            var waterDepth = map.GetHex(position.Coordinates)?.GetWaterDepth() ?? 0;
            if (waterDepth < 1) return;

            // Use reference identity: two identical PushHit contexts (same record values) can exist
            // in the list, and value equality would target the wrong push's roll.
            var index = entry.Contexts.FindIndex(c => ReferenceEquals(c, entry.Context));
            if (index >= 0)
                entry.Contexts[index] = entry.Context with { WaterDepth = waterDepth };
        }
    }
}
