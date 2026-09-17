using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>Resolves the supported BattleMech physical attacks.</summary>
public interface IPhysicalAttackResolver
{
    /// <summary>Calculates a punch or kick without mutating the target.</summary>
    AttackResolutionData Resolve(IUnit attacker, IUnit target, PhysicalAttackType attackType);
}
