using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace MakaMek.Avalonia.Tests.TestHelpers;

public class TestWeapon : Weapon
{
    public TestWeapon(
        int minimumRange = 0,
        int shortRange = 6,
        int mediumRange = 12,
        int longRange = 18) 
        : base(
            name: "Test Weapon",
            damage: 1,
            heat: 1,
            minimumRange: minimumRange,
            shortRange: shortRange,
            mediumRange: mediumRange,
            longRange: longRange,
            type: WeaponType.Energy,
            battleValue: 1)
    {
    }

    public override MakaMekComponent ComponentType => MakaMekComponent.MachineGun;
}
