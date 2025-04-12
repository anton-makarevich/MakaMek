using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class MachineGun() : Weapon(name: "Machine Gun",
    damage: 2,
    heat: 0,
    minimumRange: 0,
    shortRange: 1,
    mediumRange: 2,
    longRange: 3,
    type: WeaponType.Ballistic,
    battleValue: 5,
    ammoType: AmmoType.MachineGun)
{
    public override MakaMekComponent ComponentType => MakaMekComponent.MachineGun;
}
