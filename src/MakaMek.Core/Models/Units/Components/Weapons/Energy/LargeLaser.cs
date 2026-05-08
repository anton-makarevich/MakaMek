using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public sealed class LargeLaser : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Large Laser",
        ElementaryDamage: 8,
        Heat: 8,
        Range: new WeaponRange(0, 5, 10, 15),
        Type: WeaponType.Energy,
        BattleValue: 123,
        Size: 2,
        UnderwaterRange: new WeaponRange(0, 3, 6, 9),
        WeaponComponentType: MakaMekComponent.LargeLaser);

    // Constructor uses the static definition
    public LargeLaser(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
