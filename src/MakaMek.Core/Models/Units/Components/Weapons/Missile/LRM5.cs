using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public sealed class Lrm5(ComponentData? componentData = null) : Weapon(Definition, componentData)
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "LRM-5",
        ElementaryDamage: 1,
        Heat: 2,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 45,
        Clusters: 1,
        ClusterSize: 5,
        FullAmmoRounds:24,
        WeaponComponentType: MakaMekComponent.LRM5,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM5);
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition);
    }
}
