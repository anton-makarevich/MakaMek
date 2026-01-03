using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace MakaMek.Avalonia.Tests.TestHelpers;

public class TestWeapon : Weapon
{
    public TestWeapon(
        int minimumRange = 0,
        int shortRange = 6,
        int mediumRange = 12,
        int longRange = 18) 
        : base( new WeaponDefinition(
            Name: "Test Weapon",
            ElementaryDamage: 1,
            Heat: 1,
            MinimumRange: minimumRange,
            ShortRange: shortRange,
            MediumRange: mediumRange,
            LongRange: longRange,
            Type: WeaponType.Energy,
            BattleValue: 1,
            WeaponComponentType: MakaMekComponent.MachineGun))
    {
    }
}
