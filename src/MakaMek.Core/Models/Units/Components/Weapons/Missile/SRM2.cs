using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Srm2 : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "SRM-2",
        elementaryDamage: 2,
        heat: 2,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Missile,
        battleValue: 15,
        clusters: 2,
        clusterSize: 1,
        weaponComponentType: MakaMekComponent.SRM2,
        ammoComponentType: MakaMekComponent.ISAmmoSRM2);
            
    // Constructor uses the static definition
    public Srm2() : base(Definition)
    {
        // 2 damage per missile, 2 missiles
    }
        
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, 50);
    }
}