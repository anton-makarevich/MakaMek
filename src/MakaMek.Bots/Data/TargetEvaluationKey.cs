using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Key for caching target evaluation data between movement and weapons phases
/// </summary>
public readonly record struct TargetEvaluationKey(
    Guid AttackerId, 
    HexCoordinates AttackerCoords, 
    HexDirection AttackerFacing,
    Guid TargetId,
    HexCoordinates TargetCoords,
    HexDirection TargetFacing);
