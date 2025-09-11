using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public class MediumLaser : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Medium Laser",
        ElementaryDamage: 5,
        Heat: 3,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Energy,
        BattleValue: 46,
        WeaponComponentType: MakaMekComponent.MediumLaser);
        
    // Constructor uses the static definition
    public MediumLaser() : base(Definition)
    {
    }
}
