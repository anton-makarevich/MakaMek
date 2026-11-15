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
public interface IMovementAttackValidator
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

    /// <summary>
    /// The units that could be the target of the movement attack this movement type allows: enemy
    /// Mechs that have already finished their movement and are not themselves performing a movement
    /// attack. This is the target half of <see cref="Validate"/>, exposed so a caller that searches
    /// many candidate paths does not have to restate the rule. It never decides whether the attacker
    /// may move and never looks at the path or the remaining movement points.
    /// </summary>
    /// <param name="attacker">The unit that would make the move.</param>
    /// <param name="movementType">The movement mode, which selects the Charge or DFA rules.</param>
    /// <param name="units">Every unit in the game, whatever its state.</param>
    IReadOnlyList<IUnit> GetPotentialTargets(IUnit attacker, MovementType movementType, IEnumerable<IUnit> units);

    /// <summary>
    /// Whether the attacker can still make the declared movement attack at resolution time, after
    /// the rest of the turn. The declaration itself was validated when the move was processed; this
    /// is the same policy's view of the attacker and its committed path.
    /// </summary>
    /// <param name="attacker">The unit that declared the attack.</param>
    /// <param name="declaration">The declaration stored on the unit.</param>
    MovementAttackAttackerState GetAttackerState(IUnit attacker, PhysicalAttackDeclarationData declaration);

    /// <summary>
    /// The unit the declared movement attack resolves against: the declared target while it is still
    /// a legal target, the enemy that replaced it, or an automatic failure. Uses the same target
    /// rule as <see cref="Validate"/> and <see cref="GetPotentialTargets"/>.
    /// </summary>
    /// <param name="attacker">The unit that declared the attack.</param>
    /// <param name="declaration">The declaration stored on the unit.</param>
    /// <param name="units">Every unit in the game, whatever its state.</param>
    MovementAttackTargetSelection SelectResolutionTarget(
        IUnit attacker,
        PhysicalAttackDeclarationData declaration,
        IEnumerable<IUnit> units);
}
