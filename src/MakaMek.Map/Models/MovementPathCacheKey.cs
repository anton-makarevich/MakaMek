namespace Sanet.MakaMek.Map.Models;

public readonly record struct MovementPathCacheKey(
    HexPosition? Start,
    HexPosition? Destination,
    bool IsJump,
    int? MaxLevelChange,
    int UnitHeight);