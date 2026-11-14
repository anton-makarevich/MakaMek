using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The input of a Death From Above resolution. The attack direction and the target's displacement
/// vector are both derived from the hex where the jumping 'Mech started its movement, not from the
/// airborne approach hex (Total Warfare, p. 150).
/// </summary>
public record DfaAttackContext
{
    /// <summary>The unit making the Death From Above.</summary>
    public required IUnit Attacker { get; init; }

    /// <summary>The unit in the declared target hex at resolution time.</summary>
    public required IUnit Target { get; init; }

    /// <summary>The map the attack is made on.</summary>
    public required IBattleMap Map { get; init; }

    /// <summary>
    /// The hex where the attacker started its movement path: the attack direction and the push
    /// vector are measured from here.
    /// </summary>
    public required HexCoordinates StartCoordinates { get; init; }

    /// <summary>The hex the airborne attacker waits in before the attack resolves.</summary>
    public required HexPosition ApproachCoordinates { get; init; }
}
