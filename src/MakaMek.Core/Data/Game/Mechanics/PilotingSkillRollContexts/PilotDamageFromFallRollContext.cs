using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for determining pilot damage from a fall
/// </summary>
public record PilotDamageFromFallRollContext(int LevelsFallen) : PilotingSkillRollContext(PilotingSkillRollType.PilotDamageFromFall)
{
    /// <inheritdoc />
    public override string Render(ILocalizationService localizationService)
        => string.Format(CultureInfo.InvariantCulture, localizationService.GetString("PilotingSkillRollType_PilotDamageFromFall_WithLevels"),
            base.Render(localizationService), LevelsFallen);
}
