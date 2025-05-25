using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Client;

public record struct UpdatePlayerStatusCommand: IClientCommand
{
    public required PlayerStatus PlayerStatus { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;
        var localizedTemplate = localizationService.GetString("Command_UpdatePlayerStatus"); 
        return string.Format(localizedTemplate, player.Name, PlayerStatus);
    }

    public Guid PlayerId { get; init; }
}