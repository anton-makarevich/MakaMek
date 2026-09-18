using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>Authoritative dice, damage, and movement effects for an attack.</summary>
public record AttackResolutionData(
    int ToHitNumber,
    List<DiceResult> AttackRoll,
    bool IsHit,
    HitDirection AttackDirection,
    int ExternalHeat,
    AttackHitLocationsData? HitLocationsData = null,
    List<PartLocation>? DestroyedParts = null,
    bool UnitDestroyed = false,
    /// <summary>Optional destination for a successful displacement effect.</summary>
    HexCoordinateData? DisplacementTarget = null);
