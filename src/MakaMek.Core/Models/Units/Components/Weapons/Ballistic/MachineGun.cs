using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class MachineGun : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        Name: "Machine Gun",
        ElementaryDamage: 2,
        Heat: 0,
        MinimumRange: 0,
        ShortRange: 1,
        MediumRange: 2,
        LongRange: 3,
        Type: WeaponType.Ballistic,
        BattleValue: 5,
        FullAmmoRounds:200,
        WeaponComponentType: MakaMekComponent.MachineGun,
        AmmoComponentType: MakaMekComponent.ISAmmoMG);
        
    // Constructor uses the static definition
    public MachineGun() : base(Definition)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, Definition.FullAmmoRounds);
    }
}
