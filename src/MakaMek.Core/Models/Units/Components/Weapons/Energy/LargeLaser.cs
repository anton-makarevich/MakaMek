using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public class LargeLaser() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Large Laser",
        ElementaryDamage: 8,
        Heat: 8,
        MinimumRange: 0,
        ShortRange: 5,
        MediumRange: 10,
        LongRange: 15,
        Type: WeaponType.Energy,
        BattleValue: 123,
        Size: 2,
        WeaponComponentType: MakaMekComponent.LargeLaser);
}
