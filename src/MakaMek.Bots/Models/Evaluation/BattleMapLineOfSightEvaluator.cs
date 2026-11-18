using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Line-of-sight evaluator backed by the game's battle map.
/// </summary>
public sealed class BattleMapLineOfSightEvaluator(IClientGame game) : ILineOfSightEvaluator
{
    public bool HasLineOfSight(HexPosition origin, HexPosition target, int originHeight, int targetHeight)
    {
        var battleMap = game.BattleMap;
        return battleMap != null
               && battleMap.GetLineOfSight(origin, target, originHeight, targetHeight).HasLineOfSight;
    }
}
