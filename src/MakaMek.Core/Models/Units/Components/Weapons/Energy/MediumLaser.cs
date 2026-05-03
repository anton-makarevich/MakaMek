using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

public sealed class MediumLaser : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Medium Laser",
        ElementaryDamage: 5,
        Heat: 3,
        Range: new WeaponRange(0, 3, 6, 9),
        Type: WeaponType.Energy,
        BattleValue: 46,
        UnderwaterRange: new WeaponRange(0, 2, 4, 6),
        WeaponComponentType: MakaMekComponent.MediumLaser);
        
    // Constructor uses the static definition
    public MediumLaser(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }
}
