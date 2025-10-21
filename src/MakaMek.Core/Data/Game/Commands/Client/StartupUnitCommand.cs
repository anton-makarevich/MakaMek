using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

/// <summary>
/// Client command for voluntary unit startup
/// </summary>
public record struct StartupUnitCommand : IClientUnitCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? IdempotencyKey { get; init; }

    /// <summary>
    /// The ID of the unit to start up
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The ID of the player requesting the startup
    /// </summary>
    public required Guid PlayerId { get; init; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var unitCommand = this;
        var unit = player?.Units.FirstOrDefault(u => u.Id == unitCommand.UnitId);
        
        if (player == null || unit == null) return string.Empty;
        
        var localizedTemplate = localizationService.GetString("Command_StartupUnit");
        return string.Format(localizedTemplate, player.Name, unit.Model);
    }
}
