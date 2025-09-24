using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public sealed class Srm4(ComponentData? componentData = null) : Weapon(Definition, componentData)
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "SRM-4",
        ElementaryDamage: 2,
        Heat: 3,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Missile,
        BattleValue: 39,
        Clusters: 4,
        ClusterSize: 1,
        FullAmmoRounds: 25,
        WeaponComponentType: MakaMekComponent.SRM4,
        AmmoComponentType: MakaMekComponent.ISAmmoSRM4);
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition);
    }
}
