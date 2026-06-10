using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct DisplaceUnitCommand : IGameCommand
{
    public required Guid UnitId { get; init; }
    public required HexCoordinateData FromCoordinates { get; init; }
    public required HexCoordinateData ToCoordinates { get; init; }
    public int NewFacing { get; init; }
    public DisplacementReason DisplacementReason { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
        var unitName = unit?.Model ?? command.UnitId.ToString();

        return string.Format(localizationService.GetString("Command_DisplaceUnit"),
            unitName, FromCoordinates, ToCoordinates);
    }
}
