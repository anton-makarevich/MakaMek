using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm10() : Weapon(name: "LRM-10",
    damage: 10, heat: 4,
    minimumRange: 6,
    shortRange: 7,
    mediumRange: 14,
    longRange: 21,
    type: WeaponType.Missile,
    battleValue: 90,
    clusters: 2,
    clusterSize: 5,
    ammoType: AmmoType.LRM10)
{
    // 1 damage per missile, 10 missiles

    public override MakaMekComponent ComponentType => MakaMekComponent.LRM10;
}
