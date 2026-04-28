using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Base record for piloting skill roll contexts with optional additional data
/// </summary>
public record PilotingSkillRollContext(PilotingSkillRollType RollType)
{
    /// <summary>
    /// Renders the context as a localized string showing the roll type name
    /// and any relevant extra data (e.g. water depth or levels fallen).
    /// </summary>
    public virtual string Render(ILocalizationService localizationService)
        => localizationService.GetString($"PilotingSkillRollType_{RollType}");
}
