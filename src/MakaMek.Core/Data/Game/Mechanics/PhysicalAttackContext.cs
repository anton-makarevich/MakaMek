using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// The input of a physical attack resolution. A context describes exactly one attacking limb
/// against one target, so a two-arm punch is resolved as two independent contexts with
/// independent rolls.
/// </summary>
public record PhysicalAttackContext
{
    /// <summary>
    /// The unit making the physical attack.
    /// </summary>
    public required IUnit Attacker { get; init; }

    /// <summary>
    /// The unit being attacked.
    /// </summary>
    public required IUnit Target { get; init; }

    /// <summary>
    /// The kind of physical attack being made.
    /// </summary>
    public required PhysicalAttackType AttackType { get; init; }

    /// <summary>
    /// The single attacking limb used for this resolution: an arm for a punch, a leg for a kick.
    /// </summary>
    public required PartLocation AttackerLimb { get; init; }

    /// <summary>
    /// The map the attack is made on.
    /// </summary>
    public required IBattleMap Map { get; init; }
}
