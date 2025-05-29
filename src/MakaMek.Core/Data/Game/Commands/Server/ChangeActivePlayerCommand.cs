using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct ChangeActivePlayerCommand : IGameCommand
{
    public required Guid? PlayerId { get; init; }
    public required int UnitsToPlay { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return string.Empty;
        var localizationKey = UnitsToPlay >0 
            ? "Command_ChangeActivePlayerUnits"
            : "Command_ChangeActivePlayer";
        var localizedTemplate = localizationService.GetString(localizationKey); 
        
        return string.Format(localizedTemplate, player.Name, UnitsToPlay);
    }
}