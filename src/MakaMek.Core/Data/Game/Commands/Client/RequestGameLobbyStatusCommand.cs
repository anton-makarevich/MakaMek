using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>
/// Command sent by a client to request the current game lobby status
/// </summary>
public record struct RequestGameLobbyStatusCommand : IClientCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_RequestGameLobbyStatus");
        return string.Format(localizedTemplate, GameOriginId);
    }

    public Guid PlayerId { get; init; }
    public Guid? IdempotencyKey { get; init; }
}
