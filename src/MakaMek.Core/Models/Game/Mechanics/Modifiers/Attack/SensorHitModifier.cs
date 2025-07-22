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
    
    public static SensorHitModifier? Create(int sensorHits)
    {
        if (sensorHits == 0) return null;
        var value = sensorHits switch
            {
                1 => 2,
                >1 => 13, // Impossible hit
                _ => 0
            };
        
        return new SensorHitModifier
        {
            SensorHits = sensorHits,
            Value = value
        };
    }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_SensorHit"), Value);
}