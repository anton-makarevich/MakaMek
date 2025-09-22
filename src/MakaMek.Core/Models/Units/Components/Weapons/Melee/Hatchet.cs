using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;

public sealed class Hatchet : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Hatchet",
        ElementaryDamage: 0, // Damage is calculated based on mech tonnage
        Heat: 0,
        MinimumRange: 0,
        ShortRange: 0,
        MediumRange: 0,
        LongRange: 0,
        Type: WeaponType.Melee,
        BattleValue: 5,
        WeaponComponentType: MakaMekComponent.Hatchet);

    // Constructor uses the static definition
    public Hatchet(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    /// <summary>
    /// Hatchets cannot make aimed shots
    /// </summary>
    public override bool IsAimShotCapable => false;
}
