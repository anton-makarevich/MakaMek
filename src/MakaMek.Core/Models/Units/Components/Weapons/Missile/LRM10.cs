using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm10 : Weapon
{
    // Static definition for this weapon type
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    public static readonly WeaponDefinition Definition = new(
        Name: "LRM-10",
        ElementaryDamage: 1,
        Heat: 4,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 90,
        Clusters: 2,
        ClusterSize: 5,
        FullAmmoRounds:12,
        WeaponComponentType: MakaMekComponent.LRM10,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM10);

    // Constructor uses the static definition
    public Lrm10(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
