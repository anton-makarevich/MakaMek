using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct GameEndedCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required GameEndReason Reason { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var key = $"Command_GameEnded_{Reason}";
        return localizationService.GetString(key);
    }
}