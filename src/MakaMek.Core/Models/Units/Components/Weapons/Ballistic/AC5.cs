using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac5() : Weapon(
    new WeaponDefinition(
        name: "AC/5",
        elementaryDamage: 5,
        heat: 1,
        minimumRange: 3,
        shortRange: 6,
        mediumRange: 12,
        longRange: 18,
        type: WeaponType.Ballistic,
        battleValue: 70,
        weaponComponentType: MakaMekComponent.AC5,
        ammoComponentType: MakaMekComponent.ISAmmoAC5)
)
{
}
