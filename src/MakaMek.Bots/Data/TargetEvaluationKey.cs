using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Key for caching target evaluation data between movement and weapons phases. The target's
/// airborne level distinguishes a pending DFA attacker from a grounded unit at the same
/// coordinates. It is in-memory only and never serialized.
/// </summary>
public readonly record struct TargetEvaluationKey(
    Guid AttackerId, 
    HexCoordinates AttackerCoords, 
    HexDirection AttackerFacing,
    MovementType AttackerMovementType,
    Guid TargetId,
    HexCoordinates TargetCoords,
    HexDirection TargetFacing,
    int? TargetAirborneLevel = null);
