using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Extensions for determining weapon selection availability and restrictions
/// </summary>
public static class WeaponSelectionExtensions
{
    public static bool IsAvailableForAttack(this Weapon weapon)
    {
        // Basic weapon availability checks
        if (!weapon.IsAvailable)
            return false;
            
        var attacker = weapon.GetFirstMountPart()?.Unit;

        // Check prone-specific restrictions
        return attacker is not Mech { IsProne: true } 
               || IsWeaponAvailableWhenProne(weapon, attacker);
    }
    
    public static string GetWeaponRestrictionReason(this Weapon weapon, ILocalizationService localizationService)
    {
        if (!weapon.IsAvailable)
            return localizationService.GetString("WeaponRestriction_NotAvailable");
        
        var attacker = weapon.GetFirstMountPart()?.Unit;

        if (attacker is not Mech { IsProne: true }) return string.Empty; // No restriction
        var location = weapon.GetFirstMountPart()?.Location;
                
        // Check leg weapon restriction
        if (location?.IsLeg() == true)
            return localizationService.GetString("WeaponRestriction_ProneLegs");
                
        // Check arm weapon restriction
        if (location?.IsArm() == false) return string.Empty; // No restriction
        var committedArm = attacker.WeaponAttackState.CommittedArmLocation;
        if (committedArm != null && committedArm != location)
            return localizationService.GetString("WeaponRestriction_ProneOtherArm");

        return string.Empty; // No restriction
    }

    private static bool IsWeaponAvailableWhenProne(Weapon weapon, Unit attacker)
    {
        var location = weapon.GetFirstMountPart()?.Location;
        if (location == null)
            return false;
            
        // Leg weapons cannot be used when prone
        if (location.Value.IsLeg())
            return false;
            
        // Arm weapon restrictions - only one arm can be used
        if (!location.Value.IsArm()) return true;
        var committedArm = attacker.WeaponAttackState.CommittedArmLocation;
            
        // If no arm is committed yet, this weapon can be selected
        if (committedArm == null)
            return true;
                
        // If an arm is committed, only weapons from that arm can be selected
        return committedArm == location;

        // Torso and head weapons are always available when prone
    }
}