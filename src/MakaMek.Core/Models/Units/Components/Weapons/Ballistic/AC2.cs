using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac2() : Weapon(Definition)
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "AC/2",
        ElementaryDamage: 2,
        Heat: 1,
        MinimumRange: 4,
        ShortRange: 8,
        MediumRange: 16,
        LongRange: 24,
        Type: WeaponType.Ballistic,
        BattleValue: 37,
        FullAmmoRounds: 45,
        WeaponComponentType: MakaMekComponent.AC2,
        AmmoComponentType: MakaMekComponent.ISAmmoAC2);
        
    // Constructor uses the static definition
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
