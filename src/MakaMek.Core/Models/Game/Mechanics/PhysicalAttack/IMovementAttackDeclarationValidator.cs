using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Validates a Charge or DFA declared as part of a movement command, before the move is applied.
/// The declaration is made and stored during the Movement Phase, so the movement itself carries
/// the evidence the attack is possible: the mode, the terminal hex and the MP spent entering it.
/// <para>
/// A rejected declaration must not change any state: the caller keeps the unit unmoved and lets it
/// submit another command, the same way an already completed unit is refused.
/// </para>
/// </summary>
public interface IMovementAttackDeclarationValidator
{
    /// <summary>
    /// Validates the declaration against the attacker, the movement path and the current board.
    /// The result either carries the declaration to store, with <see cref="PhysicalAttackDeclarationData.HexesMoved"/>
    /// computed by the server, or the error that rejects the command.
    /// </summary>
    /// <param name="attacker">The unit making the move.</param>
    /// <param name="path">The movement path carried by the command.</param>
    /// <param name="movementType">The movement mode carried by the command.</param>
    /// <param name="declaration">The declaration carried by the command. Any client-supplied distance is ignored.</param>
    /// <param name="units">Every unit in the game, whatever its state.</param>
    /// <param name="map">The battle map the move happens on.</param>
    MovementAttackDeclarationValidation Validate(
        IUnit attacker,
        IReadOnlyList<PathSegmentData> path,
        MovementType movementType,
        PhysicalAttackDeclarationData declaration,
        IEnumerable<IUnit> units,
        IBattleMap map);
}
