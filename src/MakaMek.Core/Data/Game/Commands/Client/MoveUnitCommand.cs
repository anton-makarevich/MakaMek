using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct MoveUnitCommand: IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (unit is not { Position: not null }) return string.Empty;
        var localizedTemplate = localizationService.GetString("Command_MoveUnit");
        var position = MovementPath.Count>0 ? 
            MovementPath.Last().To
            : unit.Position.ToData();
        var facingHex = new HexCoordinates(position.Coordinates).Neighbor((HexDirection)position.Facing);
        return string.Format(localizedTemplate,
            player?.Name,
            unit.Model,
            new HexCoordinates(position.Coordinates),
            facingHex,
            MovementType);
    }

    public required Guid UnitId { get; init; }
    public required MovementType MovementType { get; init; }
    public required IReadOnlyList<PathSegmentData> MovementPath { get; init; }
    public Guid PlayerId { get; init; }
}