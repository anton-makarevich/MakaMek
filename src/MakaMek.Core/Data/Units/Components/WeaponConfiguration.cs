namespace Sanet.MakaMek.Core.Data.Units.Components;

public record WeaponConfiguration
{
    public required WeaponConfigurationType Type { get; init; }
    public required int Value { get; init; }
}
