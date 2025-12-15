using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct StartPhaseCommand : IGameCommand
{
    public required PhaseNames Phase { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_StartPhase"); 
        
        return string.Format(localizedTemplate, Phase);
    }
}

