using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game;
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
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class MovementAttackValidatorTests
{
    private static readonly HexCoordinates ApproachCoordinates = new(5, 5);
    private static readonly HexCoordinates DestinationCoordinates = new(5, 6);
    private static readonly HexCoordinates Elsewhere = new(2, 2);

    private static readonly PhysicalAttackDeclarationData ChargeDeclaration = new()
    {
        AttackType = PhysicalAttackType.Charge,
        AttackerLimbs = [],
        TargetId = Guid.Empty
    };

    private static readonly PhysicalAttackDeclarationData DfaDeclaration = new()
    {
        AttackType = PhysicalAttackType.DFA,
        AttackerLimbs = [],
        TargetId = Guid.Empty
    };

    private readonly MovementAttackValidator _sut = new();
    private readonly Player _attackerPlayer = new(Guid.NewGuid(), "Attacker", PlayerControlType.Human);
    private readonly Player _targetPlayer = new(Guid.NewGuid(), "Target", PlayerControlType.Human);

    [Fact]
    public void GetAttackerState_ForAChargeAtItsCommittedApproach_ReturnsCanAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitCharge(attacker);

        _sut.GetAttackerState(attacker, ChargeDeclaration)
            .ShouldBe(MovementAttackAttackerState.CanAttack);
    }

    [Fact]
    public void GetAttackerState_ForAChargePushedOutOfItsApproach_ReturnsCannotAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitCharge(attacker);
        attacker.Displace(new HexPosition(DestinationCoordinates, HexDirection.Top), null);

        _sut.GetAttackerState(attacker, ChargeDeclaration)
            .ShouldBe(MovementAttackAttackerState.CannotAttack);
    }

    [Fact]
    public void GetAttackerState_ForAProneChargeAttacker_ReturnsCannotAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitCharge(attacker);
        attacker.SetProne();

        _sut.GetAttackerState(attacker, ChargeDeclaration)
            .ShouldBe(MovementAttackAttackerState.CannotAttack);
    }

    /// <summary>
    /// A movement attack waits in the last translated hex of its move. A move that never changes
    /// hex (only turns in place) has no approach position, so the pending attack cannot resolve
    /// even though the unit itself is still standing.
    /// </summary>
    [Fact]
    public void GetAttackerState_ForAChargeWhoseMoveHasNoTranslation_ReturnsCannotAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var position = new HexPosition(ApproachCoordinates, HexDirection.Top);
        attacker.Move(MovementPath.CreateSingleSegmentPath(position, MovementType.Walk),
            null,
            true,
            position);
        attacker.MovementTaken!.GetLastTranslationIndex().ShouldBe(-1);

        _sut.GetAttackerState(attacker, ChargeDeclaration)
            .ShouldBe(MovementAttackAttackerState.CannotAttack);
    }

    [Fact]
    public void GetAttackerState_ForADfaAboveItsApproach_ReturnsCanAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitDfa(attacker);

        _sut.GetAttackerState(attacker, DfaDeclaration)
            .ShouldBe(MovementAttackAttackerState.CanAttack);
    }

    /// <summary>
    /// A prone, shut-down or unconscious DFA attacker still falls into the target hex, so the attack
    /// resolves as a no-roll miss instead of an automatic failure (docs/rules/physical-attacks.md
    /// section 3.1).
    /// </summary>
    [Fact]
    public void GetAttackerState_ForAProneDfaAttacker_ReturnsMustMiss()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitDfa(attacker);
        attacker.SetProne();

        _sut.GetAttackerState(attacker, DfaDeclaration)
            .ShouldBe(MovementAttackAttackerState.MustMiss);
    }

    [Fact]
    public void GetAttackerState_ForADfaPushedOutOfItsApproach_ReturnsCannotAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        CommitDfa(attacker);
        attacker.Displace(new HexPosition(DestinationCoordinates, HexDirection.Top), null);

        _sut.GetAttackerState(attacker, DfaDeclaration)
            .ShouldBe(MovementAttackAttackerState.CannotAttack);
    }

    [Fact]
    public void GetPotentialTargets_ReturnsMovedEnemyMechsThatCouldStillBeAttacked()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var movedEnemy = CreateDeployedMech(_targetPlayer, DestinationCoordinates);
        MoveElsewhere(movedEnemy);
        var unmovedEnemy = CreateDeployedMech(_targetPlayer, DestinationCoordinates.GetNeighbour(HexDirection.Bottom));
        var friendly = CreateDeployedMech(_attackerPlayer, DestinationCoordinates.GetNeighbour(HexDirection.Top));
        MoveElsewhere(friendly);

        var targets = _sut.GetPotentialTargets(attacker, MovementType.Run, AllUnits());

        targets.ShouldBe([movedEnemy]);
        targets.ShouldNotContain(unmovedEnemy);
    }

    [Fact]
    public void GetPotentialTargets_ExcludesATargetThatIsPerformingAMovementAttack()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var enemy = CreateDeployedMech(_targetPlayer, DestinationCoordinates);
        MoveElsewhere(enemy);
        enemy.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.DFA,
            AttackerLimbs = [],
            TargetId = attacker.Id
        });

        _sut.GetPotentialTargets(attacker, MovementType.Run, AllUnits()).ShouldBeEmpty();
    }

    [Fact]
    public void GetPotentialTargets_ExcludesAnEnemyPerformingACharge()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var enemy = CreateDeployedMech(_targetPlayer, DestinationCoordinates);
        MoveElsewhere(enemy);
        enemy.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Charge,
            AttackerLimbs = [],
            TargetId = attacker.Id
        });

        _sut.GetPotentialTargets(attacker, MovementType.Run, AllUnits()).ShouldBeEmpty();
    }

    [Fact]
    public void GetPotentialTargets_ForAMovementTypeWithoutAnAttack_ReturnsEmpty()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var enemy = CreateDeployedMech(_targetPlayer, DestinationCoordinates);
        MoveElsewhere(enemy);

        _sut.GetPotentialTargets(attacker, MovementType.StandingStill, AllUnits()).ShouldBeEmpty();
    }

    /// <summary>
    /// A target that has not moved could still move away, so it cannot be attacked yet. A unit that
    /// began the turn shut down counts as having taken its movement action first.
    /// </summary>
    [Fact]
    public void GetPotentialTargets_AcceptsAUnitThatStartedTheTurnShutdown()
    {
        var attacker = CreateDeployedMech(_attackerPlayer, ApproachCoordinates);
        var shutdown = CreateDeployedMech(_targetPlayer, DestinationCoordinates);
        shutdown.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        _sut.GetPotentialTargets(attacker, MovementType.Run, AllUnits()).ShouldBe([shutdown]);
    }

    private IEnumerable<IUnit> AllUnits() =>
        _attackerPlayer.Units.Concat(_targetPlayer.Units);

    private Mech CreateDeployedMech(Player player, HexCoordinates coordinates)
    {
        var mech = CreateMech();
        player.AddUnit(mech);
        mech.Deploy(new HexPosition(coordinates, HexDirection.Top), null);
        return mech;
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

    private static void MoveElsewhere(IUnit unit) =>
        unit.Move(
            new MovementPath(
                [new PathSegment(
                    unit.Position!,
                    new HexPosition(Elsewhere, HexDirection.Bottom),
                    [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])],
                MovementType.Walk),
            null,
            true);

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
