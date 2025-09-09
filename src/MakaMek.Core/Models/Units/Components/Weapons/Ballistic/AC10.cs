using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac10() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "AC/10",
        ElementaryDamage: 10,
        Heat: 3,
        MinimumRange: 0,
        ShortRange: 5,
        MediumRange: 10,
        LongRange: 15,
        Type: WeaponType.Ballistic,
        BattleValue: 123,
        Size: 7,
        FullAmmoRounds: 10,
        WeaponComponentType: MakaMekComponent.AC10,
        AmmoComponentType: MakaMekComponent.ISAmmoAC10);
        
    // Constructor uses the static definition
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
