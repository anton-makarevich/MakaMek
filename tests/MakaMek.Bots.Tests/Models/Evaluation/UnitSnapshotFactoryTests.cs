using NSubstitute;
using Sanet.MakaMek.Bots.Models.Evaluation;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Evaluation;

public class UnitSnapshotFactoryTests
{
    private readonly IUnit _unit = Substitute.For<IUnit>();
    private readonly UnitSnapshotFactory _sut = new();

    [Fact]
    public void CreateForTarget_ShouldProjectOnlyTheFieldsOffensiveEvaluationReads()
    {
        // Arrange
        var unitId = Guid.NewGuid();
        var position = new HexPosition(new HexCoordinates(3, 4), HexDirection.BottomRight);
        _unit.Id.Returns(unitId);
        _unit.Position.Returns(position);
        _unit.Height.Returns(2);
        _unit.IsProne.Returns(true);

        // Act
        var snapshot = _sut.CreateForTarget(_unit);

        // Assert
        snapshot.Id.ShouldBe(unitId);
        snapshot.Position.ShouldBe(position);
        snapshot.Height.ShouldBe(2);
        snapshot.IsProne.ShouldBeTrue();
        snapshot.MovementPath.ShouldNotBeNull();
        snapshot.MovementPath.Destination.ShouldBe(position);
        snapshot.Weapons.ShouldBeEmpty();
        snapshot.Configurations.ShouldBeEmpty();
        snapshot.ConfigurationTypesByPart.ShouldBeEmpty();

        // Weapon projection is the expensive part of the projection and is unused here
        _unit.DidNotReceive().GetAvailableWeapons();
        _unit.DidNotReceive().GetAttackModifiers(Arg.Any<PartLocation>());
    }
}
