using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public sealed class Ac10 : Weapon
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
    public Ac10(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
