using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct TurnEndedCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public Guid PlayerId { get; init; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var playerId = PlayerId;
        var player = game.Players.FirstOrDefault(p => p.Id == playerId);
        var localizedTemplate = localizationService.GetString("Command_TurnEnded");
        return string.Format(localizedTemplate, player?.Name);
    }
}
