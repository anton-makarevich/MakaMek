using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Game;

public readonly record struct WeaponConfigurationOptions(
    WeaponConfigurationType Type,
    IReadOnlyList<HexDirection> AvailableDirections);
