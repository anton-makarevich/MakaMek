using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile
{
    public class Srm2() : Weapon(name: "SRM-2",
        damage: 4, heat: 1,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Missile,
        battleValue: 25,
        clusters: 2,
        clusterSize: 1,
        ammoType: AmmoType.SRM2)
    {
        // 2 damage per missile, 2 missiles

        public override MakaMekComponent ComponentType => MakaMekComponent.SRM2;
    }
}
