using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Client;

public record struct RollDiceCommand : IClientCommand
{
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;
        var localizedTemplate = localizationService.GetString("Command_RollDice");

        return string.Format(localizedTemplate, player.Name);
    }

    public Guid PlayerId { get; init; }
}
