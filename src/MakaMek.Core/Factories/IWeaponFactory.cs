using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Factories;

/// <summary>
/// Interface for creating weapons and ammo components
/// </summary>
public interface IWeaponFactory
{
    /// <summary>
    /// Creates a weapon by its predefined component type
    /// </summary>
    /// <param name="componentType">The MakaMekComponent type</param>
    /// <returns>A new weapon instance, or null if the component type isn't a weapon</returns>
    Weapon? CreateWeaponByType(MakaMekComponent componentType);
    
    /// <summary>
    /// Creates ammo by its predefined component type
    /// </summary>
    /// <param name="componentType">The MakaMekComponent type</param>
    /// <returns>A new ammo instance, or null if the component type isn't ammo</returns>
    Ammo? CreateAmmoByType(MakaMekComponent componentType);
}
