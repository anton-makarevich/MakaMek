using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Data.Community;

public class SmallLaser() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Small Laser",
        ElementaryDamage: 3,
        Heat: 1,
        MinimumRange: 0,
        ShortRange: 1,
        MediumRange: 2,
        LongRange: 3,
        Type: WeaponType.Energy,
        BattleValue: 9,
        WeaponComponentType: MakaMekComponent.SmallLaser);
}
