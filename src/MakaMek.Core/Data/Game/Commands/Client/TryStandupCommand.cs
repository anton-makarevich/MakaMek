using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public record struct TryStandupCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    
    public required Guid UnitId { get; init; }
    public required Guid PlayerId { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unit = player?.Units.FirstOrDefault(u => u.Id == command.UnitId);
        
        if (unit is null) return string.Empty;
        
        var localizedTemplate = localizationService.GetString("Command_TryStandup");
        return string.Format(localizedTemplate, player?.Name, unit.Name);
    }
}
