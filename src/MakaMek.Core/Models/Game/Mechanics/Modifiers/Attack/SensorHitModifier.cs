using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// Modifier applied when a Mech has sensor critical hits affecting weapon accuracy
/// </summary>
public record SensorHitModifier : RollModifier
{
    /// <summary>
    /// Number of sensor critical hits
    /// </summary>
    public required int SensorHits { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_SensorHit"), Value);
}