using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm10() : Weapon(
    new WeaponDefinition(
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
        ammoComponentType: MakaMekComponent.ISAmmoLRM10)
)
{
}
