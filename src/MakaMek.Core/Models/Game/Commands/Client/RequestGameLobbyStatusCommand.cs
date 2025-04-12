using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Client;

/// <summary>
/// Command sent by a client to request the current game lobby status
/// </summary>
public record struct RequestGameLobbyStatusCommand : IGameCommand
{
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_RequestGameLobbyStatus");
        return string.Format(localizedTemplate, GameOriginId);
    }
}
