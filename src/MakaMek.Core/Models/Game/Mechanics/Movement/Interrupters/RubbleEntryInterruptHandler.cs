using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class RubbleEntryInterruptHandler : TerrainEntryInterruptHandler
{
    protected override PilotingSkillRollContext? GetRollContext(
        MovementInterruptContext context, PathSegmentData segment)
    {
        var dest = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
        return dest?.GetTerrain(MakaMekTerrains.Rubble) != null ? new RubbleEntryRollContext() : null;
    }
}
