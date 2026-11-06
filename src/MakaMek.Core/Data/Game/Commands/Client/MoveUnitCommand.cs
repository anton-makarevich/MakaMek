using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct MoveUnitCommand: IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    /// <summary>
    /// The Charge or DFA declaration carried by this move, or null when the unit attacks nothing.
    /// The server validates it, computes <see cref="PhysicalAttackDeclarationData.HexesMoved"/> and
    /// rebroadcasts the move with the stored declaration.
    /// </summary>
    public PhysicalAttackDeclarationData? Declaration { get; init; }

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
            foreach (var cost in segment.Costs)
            {
                sb.Append('[');
                sb.Append(cost.GetType().Name);
                sb.Append(':');
                sb.Append(cost.Value);
                if (cost is TerrainMovementCost terrainCost)
                {
                    sb.Append(':');
                    sb.Append((int)terrainCost.TerrainId);
                }
                sb.Append(']');
            }
            sb.Append(':');
            sb.Append(segment.IsReversed ? '1' : '0');
            sb.Append(':');
            sb.Append(segment.ElevationChange);
        }

        // The declaration changes what the move means, so it belongs to the command identity.
        if (Declaration is { } declaration)
        {
            sb.Append('|');
            sb.Append((int)declaration.AttackType);
            sb.Append(':');
            sb.Append(declaration.TargetId);
            sb.Append(':');
            sb.Append(declaration.HexesMoved?.ToString() ?? string.Empty);
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

        if (Declaration is { } declaration
            && game.Players.SelectMany(p => p.Units).FirstOrDefault(u => u.Id == declaration.TargetId) is { } target)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_MoveUnit_Declaration"),
                player?.Name,
                unit.Model,
                localizationService.GetString($"PhysicalAttackType_{declaration.AttackType}"),
                target.Model);
        }

        return stringBuilder.ToString();
    }

    public required Guid UnitId { get; init; }
    public required MovementType MovementType { get; init; }
    public required IReadOnlyList<PathSegmentData> MovementPath { get; init; }
    public Guid PlayerId { get; init; }
    public bool IsCompleted { get; init; }
}