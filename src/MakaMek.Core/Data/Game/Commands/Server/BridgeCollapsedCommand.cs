using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct BridgeCollapsedCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required HexCoordinateData Coordinates { get; init; }
    public int ConstructionFactor { get; init; }
    public int TotalTonnage { get; init; }
    public required Guid TriggeringUnitId { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var triggeringUnitId = TriggeringUnitId;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == triggeringUnitId);
        var unitName = unit?.Model ?? triggeringUnitId.ToString();

        return string.Format(localizationService.GetString("Command_BridgeCollapsed"),
            unitName, Coordinates, ConstructionFactor, TotalTonnage);
    }
}
