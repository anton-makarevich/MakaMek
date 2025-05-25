using Sanet.MakaMek.Core.Data.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Client;

public record struct DeployUnitCommand : IClientCommand
{
    public required Guid UnitId { get; init; }
    public required HexCoordinateData Position { get; init; }
    public required int Direction { get; init; }

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
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