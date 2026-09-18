using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game;

public record AttackResolutionData(
    int ToHitNumber,
    List<DiceResult> AttackRoll,
    bool IsHit,
    HitDirection AttackDirection,
    int ExternalHeat,
    AttackHitLocationsData? HitLocationsData = null,
    List<PartLocation>? DestroyedParts = null,
    bool UnitDestroyed = false,
    HexCoordinateData? DisplacementTarget = null);
