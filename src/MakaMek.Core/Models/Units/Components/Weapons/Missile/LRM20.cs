using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public sealed class Lrm20(ComponentData? componentData = null) : Weapon(Definition, componentData)
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "LRM-20",
        ElementaryDamage: 1,
        Heat: 6,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 181,
        Clusters: 4,
        ClusterSize: 5,
        Size: 5,
        FullAmmoRounds: 6,
        WeaponComponentType: MakaMekComponent.LRM20,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM20);
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition);
    }
}
