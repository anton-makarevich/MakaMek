using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Data.Game;

public readonly record struct WeaponConfigurationOptions(
    WeaponConfigurationType Type,
    IReadOnlyList<HexDirection> AvailableDirections);
