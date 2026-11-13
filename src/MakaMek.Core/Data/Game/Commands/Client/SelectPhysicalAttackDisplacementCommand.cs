using System.Text;
using Sanet.Localization;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Map.Data;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>
/// The target controller's choice for the hex a unit is displaced into after a missed Death From
/// Above. The server accepts it only when it matches the pending request and the candidate list.
/// </summary>
public record struct SelectPhysicalAttackDisplacementCommand : IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid UnitId { get; init; }

    /// <summary>The id of the <see cref="Server.PhysicalAttackDisplacementCandidatesCommand"/> this answers.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>The chosen destination coordinates.</summary>
    public required HexCoordinateData Coordinates { get; init; }

    /// <summary>The chosen destination surface as a <see cref="Map.Models.HexSurface"/> value.</summary>
    public int Surface { get; init; }

    public string GetPayloadHash()
    {
        var sb = new StringBuilder();
        sb.Append(RequestId);
        sb.Append('|');
        sb.Append(Coordinates.Q);
        sb.Append(',');
        sb.Append(Coordinates.R);
        sb.Append(',');
        sb.Append(Surface);
        return sb.ToString();
    }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        if (player == null || unit == null) return string.Empty;

        return string.Format(
            localizationService.GetString("Command_DfaDisplacementSelected"),
            player.Name,
            unit.Model,
            command.Coordinates);
    }
}
