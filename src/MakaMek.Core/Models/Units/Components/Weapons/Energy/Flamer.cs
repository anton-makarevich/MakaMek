using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public sealed class Flamer : Weapon
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

    // Constructor uses the static definition
    public Flamer(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
