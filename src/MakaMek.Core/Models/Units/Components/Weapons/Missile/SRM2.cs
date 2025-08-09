using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

public class Srm2 : Weapon
{
    /// <inheritdoc />
    public override bool IsAimShotCapable => false;

    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "SRM-2",
        ElementaryDamage: 2,
        Heat: 2,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Missile,
        BattleValue: 15,
        Clusters: 2,
        ClusterSize: 1,
        FullAmmoRounds: 50,
        WeaponComponentType: MakaMekComponent.SRM2,
        AmmoComponentType: MakaMekComponent.ISAmmoSRM2);
            
    // Constructor uses the static definition
    public Srm2() : base(Definition)
    {
        // 2 damage per missile, 2 missiles
    }
        
    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}