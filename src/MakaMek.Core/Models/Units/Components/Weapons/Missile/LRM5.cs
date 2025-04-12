using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm5() : Weapon(name: "LRM-5",
    damage: 5, heat: 2,
    minimumRange: 6,
    shortRange: 7,
    mediumRange: 14,
    longRange: 21,
    type: WeaponType.Missile,
    battleValue: 45,
    clusters: 1,
    clusterSize: 5,
    ammoType: AmmoType.LRM5)
{
    // 1 damage per missile, 5 missiles

    public override MakaMekComponent ComponentType => MakaMekComponent.LRM5;
}
