using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Services.Localization;
using System.Text;

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

    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
        
        if (unit == null || unit.Owner == null)
        {
            return string.Empty;
        }

        var player = unit.Owner;
        var rollTotal = DiceResults.Sum();
        var stringBuilder = new StringBuilder();

        // Get the localized roll type name
        var rollTypeKey = $"PilotingSkillRollType_{RollType}";
        var rollTypeName = localizationService.GetString(rollTypeKey);

        // Check if the roll is impossible first
        if (PsrBreakdown.IsImpossible)
        {
            // Impossible roll case (auto-fail)
            var impossibleTemplate = localizationService.GetString("Command_PilotingSkillRoll_ImpossibleRoll");
            stringBuilder.AppendLine(string.Format(impossibleTemplate,
                player.Name,
                unit.Name,
                rollTypeName));
        }
        else if (IsSuccessful)
        {
            // Success case
            var successTemplate = localizationService.GetString("Command_PilotingSkillRoll_Success");
            stringBuilder.AppendLine(string.Format(successTemplate,
                player.Name,
                unit.Name,
                rollTypeName));
        }
        else
        {
            // Failure case
            var failureTemplate = localizationService.GetString("Command_PilotingSkillRoll_Failure");
            stringBuilder.AppendLine(string.Format(failureTemplate,
                player.Name,
                unit.Name,
                rollTypeName));
        }

        // Add breakdown of modifiers
        stringBuilder.AppendLine(string.Format(
            localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill"),
            PsrBreakdown.BasePilotingSkill));

        if (PsrBreakdown.Modifiers.Count > 0)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_PilotingSkillRoll_Modifiers"));
            
            foreach (var modifier in PsrBreakdown.Modifiers)
            {
                stringBuilder.AppendLine(string.Format(
                    localizationService.GetString("Command_PilotingSkillRoll_Modifier"),
                    modifier.Render(localizationService),
                    modifier.Value));
            }
        }

        // Add total target number
        stringBuilder.AppendLine(string.Format(
            localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber"),
            PsrBreakdown.ModifiedPilotingSkill));

        // Add roll result if not an impossible roll
        if (!PsrBreakdown.IsImpossible)
        {
            stringBuilder.AppendLine(string.Format(
                localizationService.GetString("Command_PilotingSkillRoll_RollResult"),
                rollTotal));
        }
        
        return stringBuilder.ToString().TrimEnd();
    }
}
