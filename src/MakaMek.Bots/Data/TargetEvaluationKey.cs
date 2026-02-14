using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Data;

/// <summary>
/// Key for caching target evaluation data between movement and weapons phases
/// </summary>
public readonly record struct TargetEvaluationKey(
    Guid AttackerId, 
    HexCoordinates AttackerCoords, 
    HexDirection AttackerFacing,
    MovementType AttackerMovementType,
    Guid TargetId,
    HexCoordinates TargetCoords,
    HexDirection TargetFacing);
