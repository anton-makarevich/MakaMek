using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Data.Community;

public class Ppc() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "PPC",
        ElementaryDamage: 10,
        Heat: 10,
        MinimumRange: 3,
        ShortRange: 6,
        MediumRange: 12,
        LongRange: 18,
        Type: WeaponType.Energy,
        BattleValue: 176,
        Size: 3,
        WeaponComponentType: MakaMekComponent.PPC);
}
