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
/// <item>the target is an enemy Mech that has already completed its movement, is not itself
/// performing a Charge or DFA, and stands in the hex the path terminates in;</item>
/// <item>a Charge is not declared against a prone target, while a DFA against a prone target is
/// legal (Total Warfare, p. 151);</item>
/// <item>a Charge is declared only when the effective standing levels of the approach hex — the
/// hex the attacker charges from — and the target differ by at most one level, counting water
/// depth as negative levels (Total Warfare, p. 109 and p. 149-151, Different Levels Table);</item>
/// <item>a physical attack cannot cross the water line: a submerged target can only be attacked
/// by a submerged attacker (Total Warfare, p. 151);</item>
/// <item>the last segment enters that hex and pays the MP for the entry;</item>
/// <item>a DFA has enough Jumping MP to clear the target hex level plus the target's height,
/// even though entering the hex itself costs one MP;</item>
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
        if (!IsValidTarget(attacker, target, declaration.AttackType)) return Invalid();

        var movementPath = new MovementPath(path, movementType);
        if (movementPath.Start.Coordinates != attacker.Position!.Coordinates) return Invalid();

        var targetPosition = target!.Position!;
        var enteringSegment = FindEnteringSegment(movementPath);
        if (movementPath.Destination.Coordinates != targetPosition.Coordinates
            || movementPath.Destination.Surface != targetPosition.Surface
            || !EntersTargetHex(movementPath, movementType, map)
            || declaration.AttackType == PhysicalAttackType.Charge
            && !IsChargeAllowed(attacker, target, enteringSegment, targetPosition, map)
            || declaration.AttackType == PhysicalAttackType.DFA
            && !IsDfaAllowed(attacker, target, targetPosition, map)
            || movementPath.TotalCost > attacker.GetMovementPoints(movementType)
            || PhysicalAttackDisplacementBudget.IsReserved(target.Id, allUnits))
            return Invalid();

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
        && attacker is { HasDeclaredPhysicalAttack: false, HasMoved: false };

    /// <summary>
    /// A movement declaration has no limb choice: the limbs belong to the attacks declared in the
    /// physical attack phase, and the attack type must be one of the two movement-origin attacks.
    /// </summary>
    private static bool IsValidDeclaration(PhysicalAttackDeclarationData declaration) =>
        PhysicalAttackDefinitions.IsMovementAttack(declaration.AttackType)
        && declaration.AttackerLimbs is { Count: 0 };

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
    /// attacker has seen complete its move (Background item 11). A unit performing a Charge or DFA
    /// is not finished moving until the Physical Attack Phase, so it cannot be the target of
    /// another Charge or DFA (Total Warfare, p. 148-149). A unit that began the turn shutdown or
    /// with an unconscious pilot counts as having taken its movement action first for this purpose
    /// (Total Warfare, p. 144).
    /// </summary>
    private static bool IsValidTarget(IUnit attacker, IUnit? target, PhysicalAttackType attackType) =>
        target is Mech { IsDestroyed: false, Position: not null }
        && target.Owner?.Id != attacker.Owner?.Id
        && target is { HasPendingMovementAttack: false }
        && (target.HasMoved || StartedTurnIncapacitated(target))
        && IsLegalAgainstProneTarget(target, attackType);

    /// <summary>
    /// A unit observed shutdown or with an unconscious pilot during the Movement Phase cannot take
    /// a movement action this turn: shutdown is entered in the heat or end phase, and an unconscious
    /// pilot cannot move. It is therefore treated as having taken its movement action first
    /// (Total Warfare, p. 144).
    /// </summary>
    private static bool StartedTurnIncapacitated(IUnit target) =>
        target.IsShutdown || target.Pilot is { IsConscious: false };

    /// <summary>
    /// A Mech cannot charge a prone target, but a DFA against a prone target remains legal
    /// (Total Warfare, p. 151).
    /// </summary>
    private static bool IsLegalAgainstProneTarget(IUnit target, PhysicalAttackType attackType) =>
        attackType != PhysicalAttackType.Charge || target is not Mech { IsProne: true };

    /// <summary>
    /// The last segment that actually changes hex enters the destination hex. A path may end with
    /// facing changes at the destination, so trailing turn-only segments are ignored.
    /// </summary>
    private static PathSegment? FindEnteringSegment(MovementPath path) =>
        path.Segments.LastOrDefault(segment => segment.From.Coordinates != segment.To.Coordinates);

    /// <summary>
    /// The path has to enter the target hex from an adjacent hex and pay at least the entry cost
    /// this hex charges. For a jump every hex costs one MP, so the entering segment is a single
    /// jump step; for a ground move the entry cost is the hex entry plus the elevation change.
    /// </summary>
    private static bool EntersTargetHex(MovementPath path, MovementType movementType, IBattleMap map)
    {
        var enteringSegment = FindEnteringSegment(path);
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
    /// A charge has to keep the attacker and the target on the same side of the water line and
    /// within one effective level of each other. The comparison uses the effective standing levels
    /// of the approach hex — the hex the attacker charges from — and the target hex, so water depth
    /// counts as negative levels. Raw hex levels would let a surface attacker charge a target that
    /// is effectively two levels below it in deep water. The attacker's starting hex is irrelevant,
    /// because the attacker may have climbed or descended on the way there. A larger difference
    /// can only be bridged by a DFA
    /// (Total Warfare, p. 109 and p. 149-151, Different Levels Table).
    /// </summary>
    private static bool IsChargeAllowed(IUnit attacker, IUnit target, PathSegment? enteringSegment,
        HexPosition targetPosition, IBattleMap map)
    {
        if (enteringSegment is null) return false;

        var approachEndpoint = GetEndpoint(attacker, enteringSegment.From, map);
        var targetEndpoint = GetEndpoint(target, targetPosition, map);
        if (approachEndpoint is not { } approach || targetEndpoint is not { } targetState) return false;

        return approach.IsSubmerged == targetState.IsSubmerged
               && Math.Abs(targetState.StandingLevel - approach.StandingLevel) <= 1;
    }

    /// <summary>
    /// A DFA may bridge a level difference of two or more, so the Different Levels Table does not
    /// apply to it. It still cannot reach a submerged target: the attack comes from above the
    /// water line, and a unit in Depth 2+ water can only be attacked by units underwater
    /// (Total Warfare, p. 149-151).
    /// </summary>
    private static bool IsDfaAllowed(IUnit attacker, IUnit target, HexPosition targetPosition,
        IBattleMap map)
    {
        var targetEndpoint = GetEndpoint(target, targetPosition, map);
        return targetEndpoint is { IsSubmerged: false }
               && CanClearDfaHeight(attacker, target, targetPosition, map);
    }

    /// <summary>
    /// Where a unit stands relative to the water line, resolved from the map surface it occupies
    /// rather than the stored unit hex, which replacing a map hex does not update.
    /// </summary>
    private sealed record UnitEndpoint(int StandingLevel, bool IsSubmerged);

    private static UnitEndpoint? GetEndpoint(IUnit unit, HexPosition position, IBattleMap map)
    {
        var hex = map.GetHex(position.Coordinates);
        if (hex is null) return null;

        var (standingLevel, isSubmerged) = hex.GetLosEndpointInfo(position.Surface, unit.Height);
        return new UnitEndpoint(standingLevel, isSubmerged);
    }

    /// <summary>
    /// A DFA must have enough Jumping MP to clear the target hex level plus the target's height,
    /// even though entering the target hex costs only one MP. A standing Mech is two levels high
    /// and a prone one is a single level (Total Warfare, p. 149-150).
    /// </summary>
    private static bool CanClearDfaHeight(IUnit attacker, IUnit target, HexPosition targetPosition, IBattleMap map)
    {
        var targetHex = map.GetHex(targetPosition.Coordinates);
        if (targetHex is null) return false;

        var heightRequirement = targetHex.GetStandingLevel(targetPosition.Surface) + target.Height;
        return attacker.GetMovementPoints(MovementType.Jump) >= heightRequirement;
    }

    /// <summary>
    /// Hexes traversed before entering the target hex, counting only the last leg after a
    /// direction reversal and counting repeated hexes. A deferred move that continues after a fall
    /// is measured over the combined path, because the executed path is already on the unit
    /// (Total Warfare, p. 148).
    /// </summary>
    private static int CountHexesMovedBeforeTarget(IUnit attacker, MovementPath continuation,
        HexCoordinates targetCoordinates)
    {
        var combined = attacker.MovementTaken is { } taken
                       && taken.MovementType == continuation.MovementType
                       && taken.Destination.Coordinates == continuation.Start.Coordinates
            ? taken.Append(continuation)
            : continuation;

        var enteredInLastLeg = combined.HexesEnteredInLastLeg;
        return Math.Max(0, combined.Destination.Coordinates == targetCoordinates
            ? enteredInLastLeg - 1
            : enteredInLastLeg);
    }
}
