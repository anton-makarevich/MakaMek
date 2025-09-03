using System.Text;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics;

/// <summary>
/// Data for a piloting skill roll
/// </summary>
public record PilotingSkillRollData
{
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

    /// <summary>
    /// Renders the piloting skill roll data as a string
    /// </summary>
    /// <param name="localizationService">Localization service to get localized strings</param>
    /// <returns>Rendered string representation of the piloting skill roll</returns>
    public string Render(ILocalizationService localizationService)
    {
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
            stringBuilder.AppendFormat(impossibleTemplate, rollTypeName).AppendLine();
        }
        else if (IsSuccessful)
        {
            // Success case
            var successTemplate = localizationService.GetString("Command_PilotingSkillRoll_Success");
            stringBuilder.AppendFormat(successTemplate, rollTypeName).AppendLine();
        }
        else
        {
            // Failure case
            var failureTemplate = localizationService.GetString("Command_PilotingSkillRoll_Failure");
            stringBuilder.AppendFormat(failureTemplate, rollTypeName).AppendLine();
        }

        // Add breakdown of modifiers
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill"),
            PsrBreakdown.BasePilotingSkill).AppendLine();

        if (PsrBreakdown.Modifiers.Count > 0)
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_PilotingSkillRoll_Modifiers"));
            
            foreach (var modifier in PsrBreakdown.Modifiers)
            {
                stringBuilder.AppendLine(modifier.Render(localizationService));
            }
        }

        // Add the total target number
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber"),
            PsrBreakdown.ModifiedPilotingSkill).AppendLine();

        // Add roll result if not an impossible roll
        if (!PsrBreakdown.IsImpossible)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_RollResult"),
                rollTotal).AppendLine();
        }
        
        return stringBuilder.ToString().TrimEnd();
    }
}
