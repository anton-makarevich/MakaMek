using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Interrupters;

public class WaterEntryInterruptHandler : TerrainEntryInterruptHandler
{
    protected override PilotingSkillRollContext? GetRollContext(
        MovementInterruptContext context, PathSegmentData segment)
    {
        var dest = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.To.Coordinates));
        if (dest?.GetTerrain(MakaMekTerrains.Water) is not WaterTerrain { Height: <= -1 } water) return null;

        var source = context.Game.BattleMap?.GetHex(new HexCoordinates(segment.From.Coordinates));
        if (source is not null && dest.IsOnRoadOrBridge(source,
                (HexSurface)segment.From.Surface, (HexSurface)segment.To.Surface)) return null;

        return new EnteringDeepWaterRollContext(-1 * water.Height);
    }
}
