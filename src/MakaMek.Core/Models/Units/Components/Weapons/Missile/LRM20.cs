using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm20 : Weapon
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
        
    // Constructor uses the static definition
    public Lrm20(ComponentData? componentData = null) : base(Definition, componentData)
    {
        // 1 damage per missile, 20 missiles
    }
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
