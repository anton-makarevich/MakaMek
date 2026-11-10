using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class ChargeTargetSelectorTests
{
    private static readonly HexCoordinates ApproachCoordinates = new(5, 5);
    private static readonly HexCoordinates DestinationCoordinates = new(5, 6);
    private static readonly HexCoordinates Elsewhere = new(2, 2);

    private readonly Player _attackerPlayer = new(Guid.NewGuid(), "Attacker", PlayerControlType.Human);
    private readonly Player _targetPlayer = new(Guid.NewGuid(), "Target", PlayerControlType.Human);
    private readonly Mech _attacker;
    private readonly Mech _declaredTarget;

    public ChargeTargetSelectorTests()
    {
        _attacker = CreateMech();
        _declaredTarget = CreateMech();
        _attackerPlayer.AddUnit(_attacker);
        _targetPlayer.AddUnit(_declaredTarget);

        _attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top), null);
        _declaredTarget.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        // The attacker waits in the approach hex, with the destination committed on its path.
        _attacker.Move(
            new MovementPath(
                [new PathSegment(
                    new HexPosition(ApproachCoordinates, HexDirection.Top),
                    new HexPosition(DestinationCoordinates, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true,
            new HexPosition(ApproachCoordinates, HexDirection.Top));
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetIsStillStandingInTheHex_ReturnsIt()
    {
        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBe(_declaredTarget);
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeFalse();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetIsProne_AutoFailsWithoutBlockingEntry()
    {
        _declaredTarget.SetProne();

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
        selection.BlocksAttackerEntry.ShouldBeFalse();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetIsDestroyed_AutoFailsWithoutBlockingEntry()
    {
        _declaredTarget.ApplyDamage(
            [new Sanet.MakaMek.Core.Data.Game.LocationHitData(
                [new Sanet.MakaMek.Core.Data.Game.LocationDamageData(
                    PartLocation.Head, 0, 50, true)],
                [], [3, 4], PartLocation.Head)],
            HitDirection.Front);

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
        selection.BlocksAttackerEntry.ShouldBeFalse();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetLeftTheHex_AutoFails()
    {
        MoveElsewhere(_declaredTarget);

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenAnotherEnemyOccupiesTheHex_ReturnsItAsAReplacement()
    {
        MoveElsewhere(_declaredTarget);
        var replacement = CreateMech();
        _targetPlayer.AddUnit(replacement);
        replacement.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBe(replacement);
        selection.IsReplacementTarget.ShouldBeTrue();
        selection.IsAutoFail.ShouldBeFalse();
    }

    [Fact]
    public void Select_WithSeveralOccupants_ReturnsTheFirstValidOne()
    {
        MoveElsewhere(_declaredTarget);
        var first = CreateMech();
        var second = CreateMech();
        _targetPlayer.AddUnit(first);
        _targetPlayer.AddUnit(second);
        first.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        second.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBe(first);
        selection.IsReplacementTarget.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenOnlyAFriendlyUnitOccupiesTheHex_AutoFailsAndBlocksEntry()
    {
        MoveElsewhere(_declaredTarget);
        var friendly = CreateMech();
        _attackerPlayer.AddUnit(friendly);
        friendly.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        var selection = ChargeTargetSelector.Select(_attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
        selection.BlocksAttackerEntry.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenTheOccupantIsOnAnotherSurface_AutoFails()
    {
        var attacker = CreateMech();
        var target = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        _targetPlayer.AddUnit(target);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top), null);
        target.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        attacker.Move(
            new MovementPath(
                [new PathSegment(
                    new HexPosition(ApproachCoordinates, HexDirection.Top),
                    new HexPosition(DestinationCoordinates, HexDirection.Top, HexSurface.Bridge),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true,
            new HexPosition(ApproachCoordinates, HexDirection.Top));

        var selection = ChargeTargetSelector.Select(attacker, target.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WithoutACommittedDestination_AutoFails()
    {
        var attacker = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top), null);

        var selection = ChargeTargetSelector.Select(attacker, _declaredTarget.Id, AllUnits());

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    private void MoveElsewhere(IUnit unit)
    {
        unit.Move(
            new MovementPath(
                [new PathSegment(
                    unit.Position!,
                    new HexPosition(Elsewhere, HexDirection.Bottom),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true);
    }

    private IEnumerable<IUnit> AllUnits() =>
        _attackerPlayer.Units.Concat(_targetPlayer.Units);

    private static Mech CreateMech()
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        var data = MechFactoryTests.CreateDummyMechData();
        data.Id = Guid.NewGuid();
        var mech = factory.Create(data);
        mech.AssignPilot(new MechWarrior("Test", "Pilot", gunnery: 4, piloting: 5));
        return mech;
    }
}
