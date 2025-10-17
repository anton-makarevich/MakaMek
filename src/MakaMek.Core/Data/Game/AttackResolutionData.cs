using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

public record AttackResolutionData(
    int ToHitNumber,
    List<DiceResult> AttackRoll,
    bool IsHit,
    HitDirection AttackDirection,
    int ExternalHeat,
    AttackHitLocationsData? HitLocationsData = null,
    List<PartLocation>? DestroyedParts = null,
    bool UnitDestroyed = false);
