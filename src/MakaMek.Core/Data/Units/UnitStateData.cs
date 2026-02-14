using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Units;

public readonly record struct UnitStateData
{
    public IReadOnlyList<UnitStatus>? StatusFlags { get; init; }

    public IReadOnlyList<UnitPartStateData>? UnitPartStates { get; init; }

    public IReadOnlyList<PathSegmentData>? MovementPathSegments { get; init; }

    public HexPositionData? Position { get; init; }

    public IReadOnlyList<WeaponTargetData>? DeclaredWeaponTargets { get; init; }

    public int CurrentHeat { get; init; }
}
