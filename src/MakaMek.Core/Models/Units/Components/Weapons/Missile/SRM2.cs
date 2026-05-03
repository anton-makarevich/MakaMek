using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public sealed class Srm2(ComponentData? componentData = null) : Weapon(Definition, componentData)
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "SRM-2",
        ElementaryDamage: 2,
        Heat: 2,
        Range: new WeaponRange(0, 3, 6, 9),
        Type: WeaponType.Missile,
        BattleValue: 15,
        Clusters: 2,
        ClusterSize: 1,
        FullAmmoRounds: 50,
        WeaponComponentType: MakaMekComponent.SRM2,
        AmmoComponentType: MakaMekComponent.ISAmmoSRM2);
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition);
    }
}