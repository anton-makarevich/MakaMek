using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct WeaponConfigurationCommand : IClientCommand
{
    public required Guid UnitId { get; init; }
    public required WeaponConfiguration Configuration { get; set; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;

        var unit = player.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (unit == null || !unit.IsDeployed) return string.Empty;

        return Configuration.Type switch
        {
            WeaponConfigurationType.TorsoRotation => string.Format(
                localizationService.GetString("Command_WeaponConfiguration_TorsoRotation"),
                player.Name,
                unit.Name,
                unit.Position!.Coordinates.Neighbor((HexDirection)Configuration.Value)),
            WeaponConfigurationType.ArmsFlip => string.Format(
                localizationService.GetString("Command_WeaponConfiguration_ArmsFlip"),
                player.Name,
                unit.Name,
                Configuration.Value == 1 
                    ? localizationService.GetString("Direction_Forward")
                    : localizationService.GetString("Direction_Backward")),
            _ => string.Empty
        };
    }

    public Guid PlayerId { get; init; }
}
