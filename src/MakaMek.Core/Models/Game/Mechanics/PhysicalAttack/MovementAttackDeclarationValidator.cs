using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The default Charge and DFA declaration policy (Total Warfare p. 148-149 and the physical attacks
/// PRD, R6.1). A movement-origin declaration is valid only when:
/// <list type="bullet">
/// <item>the attacker is a standing, intact Mech that has not moved or declared an attack yet;</item>
/// <item>the attack is a Charge with Walk/Run movement or a DFA with Jump movement;</item>
/// <item>the target is an enemy Mech that has already completed its movement and stands in the
/// hex the path terminates in;</item>
/// <item>the last segment enters that hex and pays the MP for the entry;</item>
/// <item>the move fits the remaining movement points; and</item>
/// <item>the target is not already reserved by another displacement attack this turn.</item>
/// </list>
/// </summary>
public class MovementAttackDeclarationValidator : IMovementAttackDeclarationValidator
{
    public MovementAttackDeclarationValidation Validate(
        IUnit attacker,
        IReadOnlyList<PathSegmentData> path,
        MovementType movementType,
        PhysicalAttackDeclarationData declaration,
        IEnumerable<IUnit> units,
        IBattleMap map)
    {
        if (!IsValidAttacker(attacker)) return Invalid();
        if (!IsValidDeclaration(declaration)) return Invalid();
        if (!IsModeAllowed(declaration.AttackType, movementType)) return Invalid();
        if (path is not { Count: > 0 }) return Invalid();

        var allUnits = units.ToList();
        var target = allUnits.FirstOrDefault(unit => unit.Id == declaration.TargetId);
        if (!IsValidTarget(attacker, target)) return Invalid();

        var movementPath = new MovementPath(path, movementType);
        if (movementPath.Start.Coordinates != attacker.Position!.Coordinates) return Invalid();

        var targetPosition = target!.Position!;
        if (movementPath.Destination.Coordinates != targetPosition.Coordinates
            || movementPath.Destination.Surface != targetPosition.Surface)
            return Invalid();

        if (!EntersTargetHex(movementPath, movementType, map)) return Invalid();
        if (movementPath.TotalCost > attacker.GetMovementPoints(movementType)) return Invalid();
        if (PhysicalAttackDisplacementBudget.IsReserved(target.Id, allUnits)) return Invalid();

        var hexesMoved = CountHexesMovedBeforeTarget(attacker, movementPath, targetPosition.Coordinates);
        return MovementAttackDeclarationValidation.Valid(declaration with
        {
            AttackerLimbs = [],
            HexesMoved = hexesMoved
        });
    }

    private static MovementAttackDeclarationValidation Invalid() =>
        MovementAttackDeclarationValidation.Invalid(ErrorCode.ValidationFailed);

    private static bool IsValidAttacker(IUnit attacker) =>
        attacker is Mech { IsDestroyed: false, IsProne: false, Position: not null }
        && !attacker.HasDeclaredPhysicalAttack
        && !attacker.HasMoved;

    /// <summary>
    /// A movement declaration has no limb choice: the limbs belong to the attacks declared in the
    /// physical attack phase, and the attack type must be one of the two movement-origin attacks.
    /// </summary>
    private static bool IsValidDeclaration(PhysicalAttackDeclarationData declaration) =>
        PhysicalAttackDefinitions.IsMovementAttack(declaration.AttackType)
        && declaration.AttackerLimbs.Count == 0;

    private static bool IsModeAllowed(PhysicalAttackType attackType, MovementType movementType) =>
        attackType switch
        {
            PhysicalAttackType.Charge => movementType is MovementType.Walk or MovementType.Run,
            PhysicalAttackType.DFA => movementType == MovementType.Jump,
            _ => false
        };

    /// <summary>
    /// An enemy Mech that is still in the game and has already finished its movement. A target that
    /// has not moved could still move away, and the declaration is only allowed against a unit the
    /// attacker has seen complete its move (Background item 11).
    /// </summary>
    private static bool IsValidTarget(IUnit attacker, IUnit? target) =>
        target is Mech { IsDestroyed: false, Position: not null }
        && target.Owner?.Id != attacker.Owner?.Id
        && target.HasMoved;

    /// <summary>
    /// The path has to enter the target hex from an adjacent hex and pay at least the entry cost
    /// this hex charges. For a jump every hex costs one MP, so the entering segment is a single
    /// jump step; for a ground move the entry cost is the hex entry plus the elevation change. A
    /// path may end with facing changes at the destination, so the last segment that actually
    /// changes hex is the one that enters the target.
    /// </summary>
    private static bool EntersTargetHex(MovementPath path, MovementType movementType, IBattleMap map)
    {
        var enteringSegment = path.Segments.LastOrDefault(segment =>
            segment.From.Coordinates != segment.To.Coordinates);
        if (enteringSegment is null) return false;
        if (enteringSegment.To.Coordinates != path.Destination.Coordinates) return false;
        if (enteringSegment.From.Coordinates.DistanceTo(enteringSegment.To.Coordinates) != 1) return false;

        if (movementType == MovementType.Jump) return enteringSegment.Cost >= 1;

        var toHex = map.GetHex(enteringSegment.To.Coordinates);
        var fromHex = map.GetHex(enteringSegment.From.Coordinates);
        if (toHex is null || fromHex is null) return false;

        var entryCost = toHex
            .GetEnterMovementCost(fromHex, enteringSegment.From.Surface, enteringSegment.To.Surface)
            .Sum(cost => cost.Value);
        var elevationCost = Math.Abs(toHex.GetElevationChange(
            fromHex, enteringSegment.From.Surface, enteringSegment.To.Surface));
        return enteringSegment.Cost >= entryCost + elevationCost;
    }

    /// <summary>
    /// Hexes traversed before entering the target hex. A deferred move that continues after a fall
    /// is measured over the combined path, because the executed path is already on the unit.
    /// </summary>
    private static int CountHexesMovedBeforeTarget(IUnit attacker, MovementPath continuation,
        HexCoordinates targetCoordinates)
    {
        var combined = attacker.MovementTaken is { } taken
                       && taken.MovementType == continuation.MovementType
                       && taken.Destination.Coordinates == continuation.Start.Coordinates
            ? taken.Append(continuation)
            : continuation;

        return combined.Hexes
            .Distinct()
            .Count(coordinates =>
                coordinates != combined.Start.Coordinates
                && coordinates != targetCoordinates);
    }
}
