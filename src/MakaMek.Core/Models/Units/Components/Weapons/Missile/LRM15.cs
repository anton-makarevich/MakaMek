using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm15 : Weapon
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "LRM-15",
        ElementaryDamage: 1,
        Heat: 5,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 136,
        Clusters: 3,
        ClusterSize: 5,
        Size: 3,
        FullAmmoRounds: 8,
        WeaponComponentType: MakaMekComponent.LRM15,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM15);

    // Constructor uses the static definition
    public Lrm15(ComponentData? componentData = null) : base(Definition, componentData)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
