using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The input of a charge resolution. Unlike a limb-based physical attack, a charge is resolved as
/// one attack against the unit that occupies the committed destination hex when the attack
/// resolves.
/// </summary>
public record ChargeAttackContext
{
    /// <summary>The unit making the charge.</summary>
    public required IUnit Attacker { get; init; }

    /// <summary>
    /// The unit in the destination hex at resolution time: the original target or a replacement.
    /// </summary>
    public required IUnit Target { get; init; }

    /// <summary>The map the charge is made on.</summary>
    public required IBattleMap Map { get; init; }

    /// <summary>The hexes the attacker moved before entering the target hex.</summary>
    public required int HexesMoved { get; init; }

    /// <summary>Whether <see cref="Target"/> replaced the originally declared target.</summary>
    public bool IsReplacementTarget { get; init; }

    /// <summary>The unit the charge was declared against.</summary>
    public required Guid OriginalTargetId { get; init; }
}
