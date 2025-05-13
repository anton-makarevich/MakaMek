using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac5() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "AC5",
        elementaryDamage: 5,
        heat: 1,
        minimumRange: 3,
        shortRange: 6,
        mediumRange: 12,
        longRange: 18,
        type: WeaponType.Ballistic,
        battleValue: 70,
        size:4,
        fullAmmoRounds: 20,
        weaponComponentType: MakaMekComponent.AC5,
        ammoComponentType: MakaMekComponent.ISAmmoAC5);
        
    // Constructor uses the static definition
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
