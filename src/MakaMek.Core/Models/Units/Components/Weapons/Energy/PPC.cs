using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public sealed class Ppc : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "PPC",
        ElementaryDamage: 10,
        Heat: 10,
        Range: new WeaponRange(3, 6, 12, 18),
        Type: WeaponType.Energy,
        BattleValue: 176,
        Size: 3,
        UnderwaterRange: new WeaponRange(3, 4, 7, 10),
        WeaponComponentType: MakaMekComponent.PPC);

    // Constructor uses the static definition
    public Ppc(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
