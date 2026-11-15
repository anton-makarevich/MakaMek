using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
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

public class MovementAttackTargetSelectorTests
{
    private static readonly HexCoordinates ApproachCoordinates = new(5, 5);
    private static readonly HexCoordinates DestinationCoordinates = new(5, 6);
    private static readonly HexCoordinates Elsewhere = new(2, 2);

    private readonly Player _attackerPlayer = new(Guid.NewGuid(), "Attacker", PlayerControlType.Human);
    private readonly Player _targetPlayer = new(Guid.NewGuid(), "Target", PlayerControlType.Human);
    private readonly BattleMap _map = CreateFlatMap();
    private readonly Mech _attacker;
    private readonly Mech _declaredTarget;

    public MovementAttackTargetSelectorTests()
    {
        _attacker = CreateMech();
        _declaredTarget = CreateMech();
        _attackerPlayer.AddUnit(_attacker);
        _targetPlayer.AddUnit(_declaredTarget);

        _attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top), null);
        _declaredTarget.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        // The attacker waits in the approach hex, with the destination committed on its path.
        CommitCharge(_attacker);
    }

    private static void CommitCharge(IUnit attacker) =>
        attacker.Move(
            new MovementPath(
                [new PathSegment(
                    new HexPosition(ApproachCoordinates, HexDirection.Top),
                    new HexPosition(DestinationCoordinates, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true,
            new HexPosition(ApproachCoordinates, HexDirection.Top));

    [Fact]
    public void Select_WhenTheDeclaredTargetIsStillStandingInTheHex_ReturnsIt()
    {
        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBe(_declaredTarget);
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeFalse();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetIsProne_AutoFails()
    {
        _declaredTarget.SetProne();

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetIsDestroyed_AutoFails()
    {
        _declaredTarget.ApplyDamage(
            [new Sanet.MakaMek.Core.Data.Game.LocationHitData(
                [new Sanet.MakaMek.Core.Data.Game.LocationDamageData(
                    PartLocation.Head, 0, 50, true)],
                [], [3, 4], PartLocation.Head)],
            HitDirection.Front);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenTheDeclaredTargetLeftTheHex_AutoFails()
    {
        MoveElsewhere(_declaredTarget);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

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

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

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

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBe(first);
        selection.IsReplacementTarget.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenOnlyAFriendlyUnitOccupiesTheHex_AutoFails()
    {
        MoveElsewhere(_declaredTarget);
        var friendly = CreateMech();
        _attackerPlayer.AddUnit(friendly);
        friendly.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// An airborne DFA performer hovers over the committed hex, so it cannot be the target of a
    /// charge declared against that hex (Total Warfare p.149).
    /// </summary>
    [Fact]
    public void Select_WhenTheDeclaredTargetIsAnAirborneDfaPerformer_AutoFails()
    {
        MoveElsewhere(_declaredTarget);
        var dfaPerformer = CreateMech();
        _targetPlayer.AddUnit(dfaPerformer);
        dfaPerformer.Deploy(
            new HexPosition(DestinationCoordinates, HexDirection.Bottom, AirborneLevel: 4), null);
        DeclareDfa(dfaPerformer, _attacker);

        var selection = MovementAttackTargetSelector.Select(_attacker, dfaPerformer.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// The immunity lasts only until the performer lands (Total Warfare p.149), so a grounded DFA
    /// performer standing in the committed hex is an ordinary occupant and a legal target.
    /// </summary>
    [Fact]
    public void Select_WhenTheDeclaredTargetIsALandedDfaPerformer_ReturnsIt()
    {
        DeclareDfa(_declaredTarget, _attacker);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBe(_declaredTarget);
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeFalse();
    }

    /// <summary>
    /// The replacement target is subject to the same p.149 immunity, so an airborne DFA performer
    /// never becomes the target.
    /// </summary>
    [Fact]
    public void Select_WhenOnlyADfaPerformerOccupiesTheHex_AutoFails()
    {
        MoveElsewhere(_declaredTarget);
        var dfaPerformer = CreateMech();
        _targetPlayer.AddUnit(dfaPerformer);
        dfaPerformer.Deploy(
            new HexPosition(DestinationCoordinates, HexDirection.Bottom, AirborneLevel: 4), null);
        DeclareDfa(dfaPerformer, _attacker);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WhenADfaPerformerAndAnotherEnemyOccupyTheHex_ReturnsTheOtherEnemy()
    {
        MoveElsewhere(_declaredTarget);
        var dfaPerformer = CreateMech();
        var replacement = CreateMech();
        _targetPlayer.AddUnit(dfaPerformer);
        _targetPlayer.AddUnit(replacement);
        dfaPerformer.Deploy(
            new HexPosition(DestinationCoordinates, HexDirection.Bottom, AirborneLevel: 4), null);
        replacement.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        DeclareDfa(dfaPerformer, _attacker);

        var selection = MovementAttackTargetSelector.Select(_attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBe(replacement);
        selection.IsReplacementTarget.ShouldBeTrue();
        selection.IsAutoFail.ShouldBeFalse();
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

        var selection = MovementAttackTargetSelector.Select(attacker, target.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    [Fact]
    public void Select_WithoutACommittedDestination_AutoFails()
    {
        var attacker = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top), null);

        var selection = MovementAttackTargetSelector.Select(attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// A DFA resolves against a prone target, unlike a charge (Total Warfare p.151).
    /// </summary>
    [Fact]
    public void Select_Dfa_WhenTheDeclaredTargetIsProne_ReturnsIt()
    {
        var attacker = CreateMech();
        var target = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        _targetPlayer.AddUnit(target);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4), null);
        target.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        target.SetProne();
        CommitDfa(attacker);

        var selection = MovementAttackTargetSelector.Select(
            attacker, target.Id, PhysicalAttackType.DFA, [attacker, target], _map);

        selection.Target.ShouldBe(target);
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeFalse();
    }

    /// <summary>
    /// A target destroyed in place still occupies the hex, so the DFA resolves against it and lands;
    /// the impact becomes a miss instead of an automatic failure.
    /// </summary>
    [Fact]
    public void Select_Dfa_WhenTheDeclaredTargetIsDestroyed_ReturnsIt()
    {
        var attacker = CreateMech();
        var target = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        _targetPlayer.AddUnit(target);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4), null);
        target.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        target.ApplyDamage(
            [new Sanet.MakaMek.Core.Data.Game.LocationHitData(
                [new Sanet.MakaMek.Core.Data.Game.LocationDamageData(
                    PartLocation.Head, 0, 50, true)],
                [], [3, 4], PartLocation.Head)],
            HitDirection.Front);
        CommitDfa(attacker);

        var selection = MovementAttackTargetSelector.Select(
            attacker, target.Id, PhysicalAttackType.DFA, [attacker, target], _map);

        selection.Target.ShouldBe(target);
        selection.IsReplacementTarget.ShouldBeFalse();
        selection.IsAutoFail.ShouldBeFalse();
    }

    /// <summary>
    /// A DFA replacement has to be a Mech: a non-Mech enemy in the hex does not become the target,
    /// unlike a charge, which accepts any unit type.
    /// </summary>
    [Fact]
    public void Select_Dfa_WhenTheOnlyReplacementIsNotAMech_AutoFails()
    {
        var attacker = CreateMech();
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4), null);
        _attackerPlayer.AddUnit(attacker);
        CommitDfa(attacker);
        var vehicle = Substitute.For<IUnit>();
        vehicle.Id.Returns(Guid.NewGuid());
        vehicle.Owner.Returns(_targetPlayer);
        vehicle.IsDeployed.Returns(true);
        vehicle.Position.Returns(new HexPosition(DestinationCoordinates, HexDirection.Bottom));

        var selection = MovementAttackTargetSelector.Select(
            attacker, Guid.NewGuid(), PhysicalAttackType.DFA, [attacker, vehicle], _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// The p.149 immunity applies to a DFA replacement too: an airborne performer is skipped and the
    /// grounded enemy in the same hex becomes the target.
    /// </summary>
    [Fact]
    public void Select_Dfa_WhenTheReplacementSearchFindsAnAirbornePerformer_SkipsIt()
    {
        var attacker = CreateMech();
        var airborne = CreateMech();
        var grounded = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        _targetPlayer.AddUnit(airborne);
        _targetPlayer.AddUnit(grounded);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4), null);
        airborne.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom, AirborneLevel: 4), null);
        grounded.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        DeclareDfa(airborne, attacker);
        CommitDfa(attacker);

        var selection = MovementAttackTargetSelector.Select(
            attacker, Guid.NewGuid(), PhysicalAttackType.DFA, [attacker, airborne, grounded], _map);

        selection.Target.ShouldBe(grounded);
        selection.IsReplacementTarget.ShouldBeTrue();
    }

    /// <summary>
    /// A charge cannot cross the water line: a submerged target is only a legal charge target when
    /// the charging unit is submerged too (Total Warfare p.151).
    /// </summary>
    [Fact]
    public void Select_Charge_WhenTheDeclaredTargetIsSubmerged_AutoFails()
    {
        SetWaterHex(DestinationCoordinates, 0, 2);

        var selection = MovementAttackTargetSelector.Select(
            _attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// The same water rule applies to a replacement target, so a charge does not silently resolve
    /// against an occupant the declaration would have rejected.
    /// </summary>
    [Fact]
    public void Select_Charge_WhenTheReplacementTargetIsSubmerged_AutoFails()
    {
        MoveElsewhere(_declaredTarget);
        var replacement = CreateMech();
        _targetPlayer.AddUnit(replacement);
        replacement.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        SetWaterHex(DestinationCoordinates, 0, 2);

        var selection = MovementAttackTargetSelector.Select(
            _attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// Both endpoints on the same side of the water line satisfy the charge rule.
    /// </summary>
    [Fact]
    public void Select_Charge_WhenBothEndpointsAreSubmerged_ReturnsTheTarget()
    {
        SetWaterHex(DestinationCoordinates, 0, 2);
        SetWaterHex(ApproachCoordinates, 0, 2);

        var selection = MovementAttackTargetSelector.Select(
            _attacker, _declaredTarget.Id, PhysicalAttackType.Charge, AllUnits(), _map);

        selection.Target.ShouldBe(_declaredTarget);
        selection.IsAutoFail.ShouldBeFalse();
    }

    /// <summary>
    /// A DFA comes from above the surface, so a submerged target is never a valid target even when
    /// it still occupies the committed hex (Total Warfare p.149-151).
    /// </summary>
    [Fact]
    public void Select_Dfa_WhenTheDeclaredTargetIsSubmerged_AutoFails()
    {
        var attacker = CreateMech();
        var target = CreateMech();
        _attackerPlayer.AddUnit(attacker);
        _targetPlayer.AddUnit(target);
        attacker.Deploy(new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4), null);
        target.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);
        CommitDfa(attacker);
        SetWaterHex(DestinationCoordinates, 0, 2);

        var selection = MovementAttackTargetSelector.Select(
            attacker, target.Id, PhysicalAttackType.DFA, [attacker, target], _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    /// <summary>
    /// The selector only backs Charge and DFA. A declaration carrying any other attack type — a
    /// programming error rather than a legal game state — must never silently resolve against an
    /// occupant of the committed hex.
    /// </summary>
    [Fact]
    public void Select_ForAnAttackTypeThatIsNotAMovementAttack_AutoFails()
    {
        var otherOccupant = CreateMech();
        _targetPlayer.AddUnit(otherOccupant);
        otherOccupant.Deploy(new HexPosition(DestinationCoordinates, HexDirection.Bottom), null);

        var selection = MovementAttackTargetSelector.Select(
            _attacker, _declaredTarget.Id, PhysicalAttackType.Punch, AllUnits(), _map);

        selection.Target.ShouldBeNull();
        selection.IsAutoFail.ShouldBeTrue();
    }

    private static BattleMap CreateFlatMap()
    {
        var map = new BattleMap(10, 10);
        for (var q = 1; q <= 10; q++)
        for (var r = 1; r <= 10; r++)
        {
            var hex = new Hex(new HexCoordinates(q, r));
            hex.AddTerrain(new ClearTerrain());
            map.AddHex(hex);
        }

        return map;
    }

    private void SetWaterHex(HexCoordinates coordinates, int level, int depth)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new WaterTerrain(-depth));
        _map.AddHex(hex);
    }

    private static void CommitDfa(IUnit attacker) =>
        attacker.Move(
            new MovementPath(
                [new PathSegment(
                    new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4),
                    new HexPosition(DestinationCoordinates, HexDirection.Top),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Jump),
            null,
            true,
            new HexPosition(ApproachCoordinates, HexDirection.Top, AirborneLevel: 4));

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

    private static void DeclareDfa(Mech mech, Mech target) =>
        mech.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.DFA,
            AttackerLimbs = [],
            TargetId = target.Id
        });

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
