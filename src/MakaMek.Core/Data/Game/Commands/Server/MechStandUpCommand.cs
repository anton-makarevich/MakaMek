using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent when a mech successfully stands up from prone position
/// </summary>
public record struct MechStandUpCommand : IGameCommand
{
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The ID of the mech that stood up
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The piloting skill roll data for the standup attempt
    /// </summary>
    public required PilotingSkillRollData PilotingSkillRoll { get; init; }

    /// <summary>
    /// The facing direction the mech chose when standing up
    /// </summary>
    public HexDirection NewFacing { get; init; }
    
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
            
        if (unit is null) return string.Empty;
        
        var localizedTemplate = localizationService.GetString("Command_MechStandup");
        return string.Format(localizedTemplate, unit.Model, PilotingSkillRoll.Render(localizationService));
    }
}
