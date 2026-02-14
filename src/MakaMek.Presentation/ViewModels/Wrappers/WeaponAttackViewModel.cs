using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public record WeaponAttackViewModel
{
    public required HexCoordinates From { get; init; }
    public required HexCoordinates To { get; init; }
    public required Weapon Weapon { get; init; }
    public required string AttackerTint { get; init; } 
    public required int LineOffset { get; init; }
    public required Guid TargetId { get; init; }
}
