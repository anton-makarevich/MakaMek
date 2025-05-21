using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.ViewModels;

public record WeaponAttackViewModel
{
    public required HexCoordinates From { get; init; }
    public required HexCoordinates To { get; init; }
    public required Weapon Weapon { get; init; }
    public required string AttackerTint { get; init; } 
    public required int LineOffset { get; init; }
    public required Guid TargetId { get; init; }
}
