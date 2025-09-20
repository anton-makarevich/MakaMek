using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class Ac20 : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "AC/20",
        ElementaryDamage: 20,
        Heat: 7,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Ballistic,
        BattleValue: 178,
        Size: 10,
        FullAmmoRounds: 5,
        WeaponComponentType: MakaMekComponent.AC20,
        AmmoComponentType: MakaMekComponent.ISAmmoAC20);

    // Constructor uses the static definition
    public Ac20(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
