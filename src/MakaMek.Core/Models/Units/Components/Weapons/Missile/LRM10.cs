using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm10() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "LRM-10",
        elementaryDamage: 1,
        heat: 4,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 90,
        clusters: 2,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM10,
        ammoComponentType: MakaMekComponent.ISAmmoLRM10);
        
    // Constructor uses the static definition

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, 12);
    }
}
