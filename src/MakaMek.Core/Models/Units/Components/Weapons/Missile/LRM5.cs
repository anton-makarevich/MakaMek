using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Lrm5 : Weapon
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
        
    // Constructor uses the static definition
    public Lrm5(ComponentData? componentData = null) : base(Definition, componentData)
    {
        // 1 damage per missile, 5 missiles
    }
    
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
