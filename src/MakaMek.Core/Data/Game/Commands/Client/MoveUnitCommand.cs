using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct MoveUnitCommand: IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    public string GetPayloadHash()
    {
        var sb = new StringBuilder();
        sb.Append(IsCompleted ? '1' : '0');
        sb.Append('|');
        sb.Append((int)MovementType);
        foreach (var segment in MovementPath)
        {
            sb.Append('|');
            sb.Append(segment.From.Coordinates.Q);
            sb.Append(',');
            sb.Append(segment.From.Coordinates.R);
            sb.Append(',');
            sb.Append(segment.From.Facing);
            sb.Append(':');
            sb.Append(segment.To.Coordinates.Q);
            sb.Append(',');
            sb.Append(segment.To.Coordinates.R);
            sb.Append(',');
            sb.Append(segment.To.Facing);
            sb.Append(':');
            sb.Append(segment.Cost);
            sb.Append(':');
            sb.Append(segment.IsReversed ? '1' : '0');
            sb.Append(':');
            sb.Append(segment.ElevationChange);
        }
        return sb.ToString();
    }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (unit is not { Position: not null }) return string.Empty;
        var localizedTemplate = localizationService.GetString("Command_MoveUnit");
        var position = MovementPath.Count>0 ? 
            MovementPath[^1].To
            : unit.Position.ToData();
        var facingHex = new HexCoordinates(position.Coordinates).GetNeighbour((HexDirection)position.Facing);
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendFormat(localizedTemplate,
            player?.Name,
            unit.Model,
            new HexCoordinates(position.Coordinates),
            facingHex,
            MovementType).AppendLine();
        var completedKey = IsCompleted ? "Command_MoveUnit_Completed" : "Command_MoveUnit_Incomplete";
        var completedTemplate = localizationService.GetString(completedKey);
        stringBuilder.Append(completedTemplate);
        return stringBuilder.ToString();
    }

    public required Guid UnitId { get; init; }
    public required MovementType MovementType { get; init; }
    public required IReadOnlyList<PathSegmentData> MovementPath { get; init; }
    public Guid PlayerId { get; init; }
    public bool IsCompleted { get; init; }
}