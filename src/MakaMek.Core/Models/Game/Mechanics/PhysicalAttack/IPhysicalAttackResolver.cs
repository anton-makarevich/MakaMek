using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>Resolves the supported BattleMech physical attacks.</summary>
public interface IPhysicalAttackResolver
{
    /// <summary>
    /// Calculates a supported physical attack without mutating either unit. Damage and movement
    /// effects are applied by the authoritative phase from the returned result.
    /// </summary>
    AttackResolutionData Resolve(IUnit attacker, IUnit target, PhysicalAttackType attackType);
}
