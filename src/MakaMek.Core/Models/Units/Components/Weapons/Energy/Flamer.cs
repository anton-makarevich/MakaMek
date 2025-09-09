using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public class Flamer() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Flamer",
        ElementaryDamage: 2,
        Heat: 3,
        MinimumRange: 0,
        ShortRange: 1,
        MediumRange: 2,
        LongRange: 3,
        Type: WeaponType.Energy,
        BattleValue: 6,
        WeaponComponentType: MakaMekComponent.Flamer);
}
