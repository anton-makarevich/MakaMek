using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct TurnIncrementedCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public required int TurnNumber { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_TurnIncremented");
        return string.Format(localizedTemplate, TurnNumber);
    }
}