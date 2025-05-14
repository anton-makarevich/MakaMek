using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac5() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "AC5",
        ElementaryDamage: 5,
        Heat: 1,
        MinimumRange: 3,
        ShortRange: 6,
        MediumRange: 12,
        LongRange: 18,
        Type: WeaponType.Ballistic,
        BattleValue: 70,
        Size:4,
        FullAmmoRounds: 20,
        WeaponComponentType: MakaMekComponent.AC5,
        AmmoComponentType: MakaMekComponent.ISAmmoAC5);
        
    // Constructor uses the static definition
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
