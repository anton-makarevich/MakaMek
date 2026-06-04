using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct BridgeCollapsedCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required HexCoordinates Coordinates { get; init; }
    public int ConstructionFactor { get; init; }
    public required Guid TriggeringUnitId { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        return string.Format(localizationService.GetString("Command_BridgeCollapsed"),
            Coordinates.Q, Coordinates.R, ConstructionFactor);
    }
}
