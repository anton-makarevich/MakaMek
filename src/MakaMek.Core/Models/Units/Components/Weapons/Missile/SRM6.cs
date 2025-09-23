using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public sealed class Srm6(ComponentData? componentData = null) : Weapon(Definition, componentData)
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "SRM-6",
        ElementaryDamage: 2,
        Heat: 4,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Missile,
        BattleValue: 59,
        Clusters: 6,
        ClusterSize: 1,
        Size: 2,
        FullAmmoRounds: 15,
        WeaponComponentType: MakaMekComponent.SRM6,
        AmmoComponentType: MakaMekComponent.ISAmmoSRM6);
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition);
    }
}
