using Sanet.MekForge.Core.Data.Map;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Services.Localization;

namespace Sanet.MekForge.Core.Models.Game.Commands.Client;

public record struct DeployUnitCommand : IClientCommand
{
    public required Guid UnitId { get; init; }
    public required HexCoordinateData Position { get; init; }
    public required int Direction { get; init; }

    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; init; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;

        var unit = player.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (unit == null) return string.Empty;

        var position = new HexCoordinates(Position);
        var facingHex = position.Neighbor((HexDirection)Direction);

        var localizedTemplate = localizationService.GetString("Command_DeployUnit"); 
        
        return string.Format(localizedTemplate,
            player.Name,
            unit.Model,
            position,
            facingHex);
    }

    public Guid PlayerId { get; init; }
}