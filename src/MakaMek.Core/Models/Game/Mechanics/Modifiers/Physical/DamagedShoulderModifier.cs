using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// A push with a damaged shoulder actuator adds +2 to the pushing 'Mech's target number for each
/// damaged shoulder (Total Warfare, p. 148). Unlike a punch, the damage does not block the attack.
/// </summary>
public record DamagedShoulderModifier : RollModifier
{
    /// <summary>The number of shoulder actuators damaged across the attacker's arms.</summary>
    public required int DamagedShoulders { get; init; }

    public const int ModifierPerDamagedShoulder = 2;

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_DamagedShoulder"),
            DamagedShoulders,
            Value);
}
