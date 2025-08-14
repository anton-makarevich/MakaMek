using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating weapon selection availability and restrictions
/// </summary>
public interface IWeaponSelectionCalculator
{
    /// <summary>
    /// Determines if a weapon is available for selection based on unit state and current weapon selections
    /// </summary>
    /// <param name="weapon">The weapon to check</param>
    /// <param name="attacker">The attacking unit</param>
    /// <returns>True if the weapon can be selected, false otherwise</returns>
    bool IsWeaponAvailable(Weapon weapon, Unit attacker);
    
    /// <summary>
    /// Gets the reason why a weapon is not available (for UI display)
    /// </summary>
    /// <param name="weapon">The weapon to check</param>
    /// <param name="attacker">The attacking unit</param>
    /// <returns>A localization key for the restriction reason, or null if weapon is available</returns>
    string? GetWeaponRestrictionReason(Weapon weapon, Unit attacker);
    
    /// <summary>
    /// Determines if a weapon can be selected when the unit is prone
    /// </summary>
    /// <param name="weapon">The weapon to check</param>
    /// <param name="attacker">The attacking unit (must be prone)</param>
    /// <returns>True if the weapon can be used while prone, false otherwise</returns>
    bool IsWeaponAvailableWhenProne(Weapon weapon, Unit attacker);
}
