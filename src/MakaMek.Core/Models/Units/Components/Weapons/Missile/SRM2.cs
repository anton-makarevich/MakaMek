using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile
{
    public class Srm2() : Weapon(
        new WeaponDefinition(
            name: "SRM-2",
            elementaryDamage: 2,
            heat: 2,
            minimumRange: 0,
            shortRange: 3,
            mediumRange: 6,
            longRange: 9,
            type: WeaponType.Missile,
            battleValue: 15,
            clusters: 1,
            clusterSize: 2,
            weaponComponentType: MakaMekComponent.SRM2,
            ammoComponentType: MakaMekComponent.ISAmmoSRM2)
    )
    {
        // 2 damage per missile, 2 missiles
    }
}
