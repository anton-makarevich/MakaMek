using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

/// <summary>
/// Command sent when a unit needs to make a piloting skill roll
/// </summary>
public record struct PilotingSkillRollCommand : IGameCommand
{
    /// <summary>
    /// The ID of the unit that needs to make the piloting skill roll
    /// </summary>
    public required Guid UnitId { get; init; }
    
    /// <summary>
    /// The type of piloting skill roll to make
    /// </summary>
    public required PilotingSkillRollType RollType { get; init; }
    
    /// <summary>
    /// The result of the piloting skill roll (2d6)
    /// </summary>
    public required int[] DiceResults { get; init; }
    
    /// <summary>
    /// Whether the roll was successful (roll >= target number from PsrBreakdown.Total)
    /// </summary>
    public required bool IsSuccessful { get; init; }
    
    /// <summary>
    /// The breakdown of the piloting skill roll calculation
    /// </summary>
    public required PsrBreakdown PsrBreakdown { get; init; }

    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Format(ILocalizationService localizationService, IGame game)
    {
        throw new NotImplementedException();
    }
}
