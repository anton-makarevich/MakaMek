using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

/// <summary>
/// Applies and checks a 'Mech's weapon configuration (torso rotation, arm flip). Extracted from
/// <see cref="Mech"/> so the mech does not need to switch on the configuration types itself.
/// </summary>
internal static class MechWeaponConfigurationApplier
{
    /// <summary>
    /// Determines if a weapon configuration is currently applied to this mech.
    /// </summary>
    public static bool IsApplied(Mech mech, WeaponConfiguration config)
    {
        if (config.Type == WeaponConfigurationType.TorsoRotation)
        {
            var desiredFacing = (HexDirection)config.Value;
            return mech.Facing == desiredFacing && mech.Position?.Facing != desiredFacing;
        }

        return false;
    }

    public static void Apply(Mech mech, WeaponConfiguration weaponConfiguration)
    {
        if (mech.Position == null)
        {
            return;
        }

        switch (weaponConfiguration.Type)
        {
            case WeaponConfigurationType.TorsoRotation:
                var targetDirection = (HexDirection)weaponConfiguration.Value;
                mech.RotateTorso(targetDirection);
                break;
            case WeaponConfigurationType.ArmsFlip:
                break; //TODO to be implemented
        }
    }
}
