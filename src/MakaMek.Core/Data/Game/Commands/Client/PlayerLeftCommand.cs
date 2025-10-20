using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct PlayerLeftCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required Guid PlayerId { get; init; }
    public Guid? IdempotencyKey { get; init; }

    public readonly string Render(ILocalizationService localizationService, IGame game)
    {
        var playerId = PlayerId; // Copy to local variable to avoid capturing 'this' in lambda
        var player = game.Players.FirstOrDefault(p => p.Id == playerId);
        var playerName = player?.Name ?? "Unknown";
        var localizedTemplate = localizationService.GetString("Command_PlayerLeft");
        return string.Format(localizedTemplate, playerName);
    }
}

