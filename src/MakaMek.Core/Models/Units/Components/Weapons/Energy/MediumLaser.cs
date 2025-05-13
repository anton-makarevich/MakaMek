namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Data.Community;

public class MediumLaser : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "Medium Laser",
        elementaryDamage: 5,
        heat: 3,
        minimumRange: 0,
        shortRange: 3,
        mediumRange: 6,
        longRange: 9,
        type: WeaponType.Energy,
        battleValue: 46,
        weaponComponentType: MakaMekComponent.MediumLaser);
        
    // Constructor uses the static definition
    public MediumLaser() : base(Definition)
    {
    }
}
