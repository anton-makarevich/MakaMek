using Sanet.MakaMek.Core.Data.Community;

namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

public class MachineGun : Weapon
{
    // Static definition for this weapon type
    public static readonly WeaponDefinition Definition = new(
        name: "Machine Gun",
        elementaryDamage: 2,
        heat: 0,
        minimumRange: 0,
        shortRange: 1,
        mediumRange: 2,
        longRange: 3,
        type: WeaponType.Ballistic,
        battleValue: 5,
        weaponComponentType: MakaMekComponent.MachineGun,
        ammoComponentType: MakaMekComponent.ISAmmoMG);
        
    // Constructor uses the static definition
    public MachineGun() : base(Definition)
    {
    }

    public static Ammo CreateAmmo()
    {
        return new Ammo(Definition, 200);
    }
}
