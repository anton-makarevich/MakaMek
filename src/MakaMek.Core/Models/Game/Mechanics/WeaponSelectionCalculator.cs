using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Calculator for determining weapon selection availability and restrictions
/// </summary>
public class WeaponSelectionCalculator : IWeaponSelectionCalculator
{
    public bool IsWeaponAvailable(Weapon weapon, Unit attacker)
    {
        // Basic weapon availability checks
        if (!weapon.IsAvailable)
            return false;
            
        // Check prone-specific restrictions
        if (attacker is Mech { IsProne: true })
        {
            return IsWeaponAvailableWhenProne(weapon, attacker);
        }
        
        return true;
    }
    
    public string? GetWeaponRestrictionReason(Weapon weapon, Unit attacker)
    {
        if (!weapon.IsAvailable)
            return "WeaponRestriction_NotAvailable";
            
        if (attacker is Mech { IsProne: true })
        {
            var location = weapon.MountedOn?.Location;
            if (location == null)
                return "WeaponRestriction_NotMounted";
                
            // Check leg weapon restriction
            if (location.Value.IsLeg())
                return "WeaponRestriction_ProneLegs";
                
            // Check arm weapon restriction
            if (location.Value.IsArm())
            {
                var committedArm = attacker.WeaponAttackState.CommittedArmLocation;
                if (committedArm != null && committedArm != location)
                    return "WeaponRestriction_ProneOtherArm";
            }
        }
        
        return null; // No restriction
    }
    
    public bool IsWeaponAvailableWhenProne(Weapon weapon, Unit attacker)
    {
        var location = weapon.MountedOn?.Location;
        if (location == null)
            return false;
            
        // Leg weapons cannot be used when prone
        if (location.Value.IsLeg())
            return false;
            
        // Arm weapon restrictions - only one arm can be used
        if (location.Value.IsArm())
        {
            var committedArm = attacker.WeaponAttackState.CommittedArmLocation;
            
            // If no arm is committed yet, this weapon can be selected
            if (committedArm == null)
                return true;
                
            // If an arm is committed, only weapons from that arm can be selected
            return committedArm == location;
        }
        
        // Torso and head weapons are always available when prone
        return true;
    }
}
