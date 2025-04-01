using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

public record struct DiceRolledCommand : IGameCommand
{
    public required Guid PlayerId { get; init; }
    public required int Roll { get; init; }
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;
        var localizedTemplate = localizationService.GetString("Command_DiceRolled");
        return string.Format(localizedTemplate, player.Name, Roll);
    }
}
