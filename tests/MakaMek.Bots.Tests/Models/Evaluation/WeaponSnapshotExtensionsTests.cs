using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Evaluation;

public class WeaponSnapshotExtensionsTests
{
    [Fact]
    public void IsInFiringArc_WhenAFacingIsSupplied_ShouldUseItInsteadOfTheMountFacing()
    {
        // Arrange - the target is in front when the torso is rotated, but behind the weapon as mounted
        var origin = new HexCoordinates(1, 1);
        var target = origin.GetNeighbour(HexDirection.Top);
        var weapon = CreateSnapshot(mountFacing: HexDirection.Bottom);

        // Act & Assert
        weapon.IsInFiringArc(origin, target, HexDirection.Top).ShouldBeTrue();
        weapon.IsInFiringArc(origin, target).ShouldBeFalse();
    }

    [Fact]
    public void CanReach_WhenTheWeaponHasNoLongRange_ShouldReturnFalse()
    {
        // Arrange - a weapon without a range bracket (for example a submerged weapon that cannot
        // fire underwater) cannot threaten any hex
        var weapon = CreateSnapshot(longRange: null);

        // Act & Assert
        weapon.CanReach(1).ShouldBeFalse();
    }

    private static WeaponSnapshot CreateSnapshot(
        HexDirection? mountFacing = HexDirection.Top,
        int? longRange = 9) =>
        new()
        {
            Weapon = new TestWeapon(new WeaponDefinition(
                "TestLaser", 5, 1, new WeaponRange(0, 3, 6, 9), WeaponType.Energy, 100)),
            Damage = 5,
            LongRange = longRange,
            MountLocation = PartLocation.RightArm,
            MountFacing = mountFacing,
            FiringArcs = [FiringArc.Front],
            AttackModifiers = []
        };

    private sealed class TestWeapon(WeaponDefinition definition) : Weapon(definition);
}
