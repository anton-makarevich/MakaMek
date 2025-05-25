using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when targeting a secondary target
/// </summary>
public record SecondaryTargetModifier : RollModifier
{
    /// <summary>
    /// Whether the target is in the front arc
    /// </summary>
    public required bool IsInFrontArc { get; init; }

    public override string Render(ILocalizationService localizationService)
    {
        var template = IsInFrontArc
            ? localizationService.GetString("Attack_SecondaryTargetFrontArc")
            : localizationService.GetString("Attack_SecondaryTargetOtherArc");
        
        return string.Format(template, Value);
    }
}
