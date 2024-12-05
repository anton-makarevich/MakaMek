namespace Sanet.MekForge.Core.Models.Units.Components.Weapons.Energy;

public class MediumLaser : Weapon
{
    private static readonly int[] MediumLaserSlots = { 0 };

    public MediumLaser() : base(
        name: "Medium Laser",
        slots: MediumLaserSlots,
        damage: 5,
        heat: 3,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Energy,
        battleValue: 46,
        ammoType: AmmoType.None)
    {
    }
}
