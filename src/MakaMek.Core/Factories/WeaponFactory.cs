using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Factories;

/// <summary>
/// Factory for creating weapons and ammo from weapon definitions.
/// This allows for a data-driven approach to weapon creation without needing individual classes.
/// </summary>
public static class WeaponFactory
{
    /// <summary>
    /// Creates a weapon from a weapon definition
    /// </summary>
    /// <param name="definition">The weapon definition</param>
    /// <returns>A new weapon instance</returns>
    public static Weapon CreateWeapon(WeaponDefinition definition)
    {
        return new Weapon(definition);
    }
    
    /// <summary>
    /// Creates ammo for a weapon definition
    /// </summary>
    /// <param name="definition">The weapon definition</param>
    /// <returns>A new ammo instance</returns>
    /// <exception cref="ArgumentException">Thrown if the definition doesn't require ammo</exception>
    public static Ammo CreateAmmo(WeaponDefinition definition)
    {
        return new Ammo(definition);
    }
    
    /// <summary>
    /// Helper method to create a weapon by its predefined type
    /// </summary>
    /// <param name="componentType">The MakaMekComponent type</param>
    /// <returns>A new weapon instance, or null if the component type isn't a weapon</returns>
    public static Weapon? CreateWeaponByType(MakaMekComponent componentType)
    {
        // Find the definition that matches the component type
        var definition = WeaponDefinitions.GetDefinitionByWeaponType(componentType);
        return definition != null ? CreateWeapon(definition) : null;
    }
    
    /// <summary>
    /// Helper method to create ammo by its predefined type
    /// </summary>
    /// <param name="componentType">The MakaMekComponent type</param>
    /// <returns>A new ammo instance, or null if the component type isn't ammo</returns>
    public static Ammo? CreateAmmoByType(MakaMekComponent componentType)
    {
        // Find the definition that matches the ammo component type
        var definition = WeaponDefinitions.GetDefinitionByAmmoType(componentType);
        return definition != null ? CreateAmmo(definition) : null;
    }
}
