using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm5 : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "LRM-5",
        elementaryDamage: 1,
        heat: 2,
        minimumRange: 6,
        shortRange: 7,
        mediumRange: 14,
        longRange: 21,
        type: WeaponType.Missile,
        battleValue: 45,
        clusters: 1,
        clusterSize: 5,
        weaponComponentType: MakaMekComponent.LRM5,
        ammoComponentType: MakaMekComponent.ISAmmoLRM5);
        
    // Constructor uses the static definition
    public Lrm5() : base(Definition)
    {
        // 1 damage per missile, 5 missiles
    }
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, 24);
    }
}
