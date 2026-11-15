using System.Text;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Completes a pending Death From Above jump. The landing hex, facing and surface are derived from
/// the movement path the unit already recorded during the Movement Phase, so the command only
/// identifies the unit and why it grounded.
/// </summary>
public record struct DfaLandingCommand : IGameCommand
{
    public required Guid UnitId { get; init; }

    public required DfaLandingKind LandingKind { get; init; }

    public required Guid GameOriginId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
        var unitName = unit?.Model ?? command.UnitId.ToString();

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_DfaLanding"),
            unitName,
            localizationService.GetString($"DfaLandingKind_{command.LandingKind}"));
        return stringBuilder.ToString().TrimEnd();
    }
}
