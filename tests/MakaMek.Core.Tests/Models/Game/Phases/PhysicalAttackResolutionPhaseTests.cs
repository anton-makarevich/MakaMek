using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class PhysicalAttackResolutionPhaseTests : GamePhaseTestsBase
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);
    private static readonly HexCoordinates TargetHex = AttackerHex.GetNeighbour(HexDirection.Top);
    private static readonly HexCoordinates DestinationHex = TargetHex.GetNeighbour(HexDirection.Top);

    private PhysicalAttackResolutionPhase _sut = null!;
    private IGamePhase _mockNextPhase = null!;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private Mech _player1Mech = null!;
    private Mech _player2Mech = null!;

    protected override void SetupSut()
    {
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.PhysicalAttackResolution, Game).Returns(_mockNextPhase);

        _sut = new PhysicalAttackResolutionPhase(Game);

        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1"));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        var player1 = Game.Players[0];
        var player2 = Game.Players[1];
        _player1Mech = (Mech)player1.Units[0];
        _player2Mech = (Mech)player2.Units[0];

        // Player 2 resolves first. The two Mechs face each other across adjacent hexes.
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });
        var player1Hex = new HexCoordinates(5, 5);
        _player1Mech.Deploy(new HexPosition(player1Hex, HexDirection.Top), null);
        _player2Mech.Deploy(new HexPosition(player1Hex.GetNeighbour(HexDirection.Top), HexDirection.Bottom), null);

        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(), Arg.Any<IGame>(), Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>?>(), Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>())
            .Returns([]);
    }

    [Fact]
    public void Name_ShouldBePhysicalAttackResolution()
    {
        _sut.Name.ShouldBe(PhaseNames.PhysicalAttackResolution);
    }

    [Fact]
    public void Enter_WithNoDeclarations_PublishesOnlyThePhaseChange()
    {
        // Arrange
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().ShouldHaveSingleItem().ShouldBeOfType<ChangePhaseCommand>();
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.PhysicalAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void Enter_TwoArmPunch_ResolvesAndPublishesEachArmSeparately()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.RightArm);
        ResolverReturnsMiss();

        // Act
        _sut.Enter();

        // Assert
        var resolutions = PublishedCommands().OfType<PhysicalAttackResolutionCommand>().ToList();
        resolutions.Select(c => c.ResolutionData.AttackerLimb).ShouldBe([PartLocation.LeftArm, PartLocation.RightArm]);
        resolutions.Select(c => c.ResolutionId).Distinct().Count().ShouldBe(2);
        resolutions.ShouldAllBe(c => c.GameOriginId == Game.Id && c.PlayerId == _player1Id
                                     && c.AttackerId == _player1Mech.Id && c.TargetId == _player2Mech.Id);
        MockPhysicalAttackResolver.Received(2).ResolveAttack(Arg.Is<PhysicalAttackContext>(c =>
            c.Attacker == _player1Mech && c.Target == _player2Mech && c.AttackType == PhysicalAttackType.Punch
            && c.Map == Game.BattleMap));
    }

    [Fact]
    public void Enter_ResolvesInInitiativeOrder()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Kick, PartLocation.RightLeg);
        ResolverReturnsMiss();

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<PhysicalAttackResolutionCommand>()
            .Select(c => c.AttackerId)
            .ShouldBe([_player2Mech.Id, _player1Mech.Id]);
    }

    [Fact]
    public void Enter_AttackerDestroyedByAnEarlierAttackInThePhase_StillAttacks()
    {
        // Arrange: player 2 goes first and destroys player 1's Mech, which also declared a punch
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Punch, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player2Mech))
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.RightArm, PartLocation.Head,
                armorDamage: 50, structureDamage: 50));
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player1Mech))
            .Returns(Miss(PhysicalAttackType.Punch, PartLocation.LeftArm));

        // Act
        _sut.Enter();

        // Assert
        _player1Mech.IsDestroyed.ShouldBeTrue();
        var resolutions = PublishedCommands().OfType<PhysicalAttackResolutionCommand>().ToList();
        resolutions.Select(c => c.AttackerId).ShouldBe([_player2Mech.Id, _player1Mech.Id]);
        resolutions[0].ResolutionData.AttackResolution.UnitDestroyed.ShouldBeTrue();
        resolutions[0].ResolutionData.AttackResolution.DestroyedParts.ShouldNotBeNull()
            .ShouldContain(PartLocation.Head);
    }

    [Fact]
    public void Enter_AttackerDestroyedBeforeThePhase_IsSkippedWithoutDamage()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        _player1Mech.ApplyDamage(
            [new LocationHitData([new LocationDamageData(PartLocation.Head, 50, 50, true)], [], [3, 4],
                PartLocation.Head)],
            HitDirection.Front);
        _player1Mech.IsDestroyed.ShouldBeTrue();
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.DidNotReceiveWithAnyArgs().ResolveAttack(null!);
        PublishedCommands().ShouldHaveSingleItem().ShouldBeOfType<ChangePhaseCommand>();
    }

    [Fact]
    public void Enter_AttackerProneBeforeThePhase_IsSkippedWithoutDamage()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        _player1Mech.SetProne();
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.DidNotReceiveWithAnyArgs().ResolveAttack(null!);
        PublishedCommands().ShouldHaveSingleItem().ShouldBeOfType<ChangePhaseCommand>();
    }

    [Fact]
    public void Enter_LimbThatCanNoLongerPunch_IsSkipped()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.RightArm);
        _player1Mech.Parts[PartLocation.RightArm].GetComponent<ShoulderActuator>()!.Hit();
        ResolverReturnsMiss();

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.Received(1).ResolveAttack(Arg.Any<PhysicalAttackContext>());
        MockPhysicalAttackResolver.Received(1).ResolveAttack(Arg.Is<PhysicalAttackContext>(c =>
            c.AttackerLimb == PartLocation.LeftArm));
    }

    [Fact]
    public void Enter_TargetThatMovedOutOfReach_IsSkipped()
    {
        // Arrange: the target is no longer in front of the attacker
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        var from = _player2Mech.Position!;
        _player2Mech.Move(new MovementPath(
        [
            new PathSegment(from, new HexPosition(new HexCoordinates(5, 2), HexDirection.Bottom),
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }])
        ], MovementType.Walk), null);
        _player2Mech.Position!.Coordinates.ShouldBe(new HexCoordinates(5, 2));
        ResolverReturnsMiss();

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.DidNotReceiveWithAnyArgs().ResolveAttack(null!);
    }

    [Fact]
    public void Enter_Hit_AppliesTheDamageOnTheServer()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.CenterTorso, armorDamage: 4));
        var centerTorso = _player2Mech.Parts[PartLocation.CenterTorso];
        var armorBefore = centerTorso.CurrentArmor;

        // Act
        _sut.Enter();

        // Assert
        centerTorso.CurrentArmor.ShouldBe(armorBefore - 4);
    }

    [Fact]
    public void Enter_KickHit_AsksTheTargetForAKickHitRoll()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Kick, PartLocation.LeftLeg, PartLocation.RightLeg, armorDamage: 2,
                psrRequirements: KickHitRequirement));

        // Act
        _sut.Enter();

        // Assert
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.KickHit));
        MockFallProcessor.DidNotReceive().ProcessPotentialFall(
            _player1Mech, Arg.Any<IGame>(), Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>());
    }

    [Fact]
    public void Enter_KickMiss_AsksTheKickerForAKickMissRoll()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Miss(PhysicalAttackType.Kick, PartLocation.LeftLeg, psrRequirements: KickMissRequirement));

        // Act
        _sut.Enter();

        // Assert
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player1Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.KickMiss));
    }

    [Fact]
    public void Enter_PunchMiss_AsksNobodyForAnAttackRoll()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        ResolverReturnsMiss();

        // Act
        _sut.Enter();

        // Assert
        MockFallProcessor.DidNotReceive().ProcessPotentialFall(
            _player1Mech, Arg.Any<IGame>(), Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>());
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts => contexts != null && contexts.Count == 0));
    }

    [Fact]
    public void Enter_EndOfPhaseRolls_AreMadeInInitiativeOrder()
    {
        // Arrange: player 1 lands a kick on player 2, and player 2's kick at player 1 misses.
        // Player 2 resolves first, so it is the first unit to roll at the end of the phase.
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Kick, PartLocation.RightLeg);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player1Mech))
            .Returns(Hit(PhysicalAttackType.Kick, PartLocation.LeftLeg, PartLocation.RightLeg, armorDamage: 2,
                psrRequirements: KickHitRequirement));
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player2Mech))
            .Returns(Miss(PhysicalAttackType.Kick, PartLocation.RightLeg, psrRequirements: KickMissRequirement));

        // Act
        _sut.Enter();

        // Assert
        Received.InOrder(() =>
        {
            MockFallProcessor.ProcessPotentialFall(
                _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
                Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                    contexts != null && contexts.Select(c => c.RollType).SequenceEqual(new[]
                        { PilotingSkillRollType.KickMiss, PilotingSkillRollType.KickHit })));
            MockFallProcessor.ProcessPotentialFall(
                _player1Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
                Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>());
        });
    }

    [Fact]
    public void Enter_FallFromAKick_IsPublishedAfterTheResolution()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Kick, PartLocation.LeftLeg, PartLocation.RightLeg, armorDamage: 2,
                psrRequirements: KickHitRequirement));
        var fall = new MechFallCommand
        {
            GameOriginId = Game.Id,
            UnitId = _player2Mech.Id,
            DamageData = null
        };
        MockFallProcessor.ProcessPotentialFall(
                _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
                Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>())
            .Returns([fall]);
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().Select(c => c.GetType()).ShouldBe(
            [typeof(PhysicalAttackResolutionCommand), typeof(MechFallCommand), typeof(ChangePhaseCommand)]);
    }

    [Fact]
    public void HandleCommand_ShouldPublishNothing_AndNotChangePhase()
    {
        // Arrange
        CommandPublisher.ClearReceivedCalls();
        var command = new PhysicalAttackCommand
        {
            GameOriginId = Game.Id,
            UnitId = Guid.NewGuid(),
            Declaration = new PhysicalAttackDeclarationData
            {
                AttackType = PhysicalAttackType.Punch,
                AttackerLimbs = [PartLocation.LeftArm],
                TargetId = Guid.NewGuid()
            }
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<IGameCommand>());
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.PhysicalAttackResolution, Game);
    }

    [Fact]
    public void Enter_DeclarationForTargetNotInTheGame_SkipsTheAttack()
    {
        // Arrange
        SetMap();
        _player1Mech.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = Guid.NewGuid()
        });
        ResolverReturnsMiss();
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.DidNotReceiveWithAnyArgs().ResolveAttack(null!);
        PublishedCommands().ShouldHaveSingleItem().ShouldBeOfType<ChangePhaseCommand>();
    }

    [Fact]
    public void Enter_Hit_PublishesHullBreachCommand_WhenTheCalculatorReturnsOne()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.CenterTorso, armorDamage: 4));
        var hullBreachCommand = new HullBreachCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _player2Mech.Id,
            BreachedLocations = []
        };
        Game.HullBreachCalculator.CalculateAndApplyHullBreach(
                Arg.Any<IUnit>(), Arg.Any<List<LocationDamageData>>())
            .Returns(hullBreachCommand);
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        CommandPublisher.Received().PublishCommand(
            Arg.Is<HullBreachCommand>(cmd => cmd.GameOriginId == Game.Id && cmd.UnitId == _player2Mech.Id));
    }

    [Fact]
    public void Enter_HitWithStructureDamage_AppliesAndPublishesCriticalHits()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.CenterTorso,
                armorDamage: 2, structureDamage: 3));
        var componentHit = new ComponentHitData
        {
            Type = MakaMekComponent.ISAmmoLRM10,
            Slot = 1,
            ExplosionDamage = 0
        };
        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            TargetId = _player2Mech.Id,
            CriticalHits =
            [
                new LocationCriticalHitsData(PartLocation.LeftTorso, [6, 6], 1, [componentHit], false),
                new LocationCriticalHitsData(PartLocation.LeftArm, [12], 1, null, true)
            ]
        };
        MockCriticalHitsCalculator.CalculateCriticalHits(
                Arg.Is<IUnit>(u => u.Id == _player2Mech.Id),
                Arg.Any<List<LocationDamageData>>())
            .Returns(criticalHitsCommand);
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        _player2Mech.Parts[PartLocation.LeftArm].IsBlownOff.ShouldBeTrue();
        CommandPublisher.Received().PublishCommand(
            Arg.Is<CriticalHitsResolutionCommand>(cmd =>
                cmd.GameOriginId == Game.Id && cmd.TargetId == _player2Mech.Id));
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game,
            Arg.Is<List<ComponentHitData>>(hits => hits.Contains(componentHit)),
            Arg.Is<List<PartLocation>?>(parts => parts != null && parts.Contains(PartLocation.LeftArm)),
            Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>());
    }

    [Fact]
    public void Enter_UnmappedRollType_ThrowsNotSupportedException()
    {
        // Arrange: only the kick obligations have a typed roll context yet
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.CenterTorso,
                armorDamage: 2,
                psrRequirements:
                [
                    new PhysicalAttackPsrRequirement(
                        PhysicalAttackParticipant.Target,
                        PilotingSkillRollType.HeavyDamage)
                ]));

        // Act + Assert
        Should.Throw<NotSupportedException>(() => _sut.Enter())
            .Message.ShouldContain(nameof(PilotingSkillRollType.HeavyDamage));
    }

    [Fact]
    public void Enter_UnknownRollRecipient_ThrowsNotSupportedException()
    {
        // Arrange: the resolver contract only knows the attacker and the target
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Punch, PartLocation.LeftArm);
        const PhysicalAttackParticipant unknownRecipient = (PhysicalAttackParticipant)99;
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Punch, PartLocation.LeftArm, PartLocation.CenterTorso,
                armorDamage: 2,
                psrRequirements:
                [
                    new PhysicalAttackPsrRequirement(unknownRecipient, PilotingSkillRollType.KickHit)
                ]));

        // Act + Assert
        Should.Throw<NotSupportedException>(() => _sut.Enter())
            .Message.ShouldContain(((int)unknownRecipient).ToString());
    }

    [Fact]
    public void Enter_FallWithDamage_ResolvesCriticalHitsForTheFallDamage_ThenStopsRollingForTheUnit()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Kick, PartLocation.LeftLeg);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(Hit(PhysicalAttackType.Kick, PartLocation.LeftLeg, PartLocation.RightLeg, armorDamage: 2,
                psrRequirements: KickHitRequirement));
        var fallWithDamage = new MechFallCommand
        {
            GameOriginId = Game.Id,
            UnitId = _player2Mech.Id,
            DamageData = new FallingDamageData(
                HexDirection.Bottom,
                new HitLocationsData(
                [
                    new LocationHitData(
                        [new LocationDamageData(PartLocation.LeftTorso, 0, 4, false)], [], [], PartLocation.LeftTorso)
                ], 4),
                new DiceResult(3),
                HitDirection.Front)
        };
        var fallWithoutDamage = new MechFallCommand
        {
            GameOriginId = Game.Id,
            UnitId = _player2Mech.Id,
            DamageData = null
        };
        MockFallProcessor.ProcessPotentialFall(
                _player2Mech, Game, Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>?>(),
                Arg.Any<IReadOnlyList<PilotingSkillRollContext>?>())
            .Returns([fallWithDamage, fallWithoutDamage]);
        MockCriticalHitsCalculator.CalculateCriticalHits(
                Arg.Is<IUnit>(u => u.Id == _player2Mech.Id),
                Arg.Is<List<LocationDamageData>>(damage =>
                    damage.Count == 1 && damage[0].Location == PartLocation.LeftTorso))
            .Returns(new CriticalHitsResolutionCommand
            {
                GameOriginId = Guid.NewGuid(),
                TargetId = _player2Mech.Id,
                CriticalHits =
                [
                    new LocationCriticalHitsData(PartLocation.LeftTorso, [7, 8], 1, null, false)
                ]
            });
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        MockCriticalHitsCalculator.Received(1).CalculateCriticalHits(
            _player2Mech,
            Arg.Is<List<LocationDamageData>>(damage =>
                damage.Count == 1 && damage[0].Location == PartLocation.LeftTorso));
        // The first fall command is applied and published, the second one never is (break)
        PublishedCommands().Select(c => c.GetType()).ShouldBe(
            [typeof(PhysicalAttackResolutionCommand), typeof(MechFallCommand),
                typeof(CriticalHitsResolutionCommand), typeof(ChangePhaseCommand)]);
        PublishedCommands().OfType<CriticalHitsResolutionCommand>()
            .ShouldHaveSingleItem().GameOriginId.ShouldBe(Game.Id);
        // The pilot rolls consciousness after the attack and again after the fall
        MockConsciousnessCalculator.Received(2).MakeConsciousnessRolls(_player2Mech.Pilot!);
    }

    [Fact]
    public void Enter_PairedPush_ResolvesOnceForTheWholeDeclaration()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        MockPhysicalAttackResolver.Received(1).ResolveAttack(Arg.Is<PhysicalAttackContext>(c =>
            c.AttackType == PhysicalAttackType.Push
            && c.AttackerLimb == PartLocation.LeftArm));
        PublishedCommands().OfType<PhysicalAttackResolutionCommand>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Enter_PushHit_DisplacesTheTargetAndAdvancesTheAttacker()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        var displacements = PublishedCommands().OfType<DisplaceUnitCommand>().ToList();
        displacements.Count.ShouldBe(2);
        displacements[0].UnitId.ShouldBe(_player2Mech.Id);
        displacements[0].ToCoordinates.ShouldBe(DestinationHex.ToData());
        displacements[0].DisplacementReason.ShouldBe(DisplacementReason.Push);
        displacements[1].UnitId.ShouldBe(_player1Mech.Id);
        displacements[1].ToCoordinates.ShouldBe(TargetHex.ToData());
        displacements[1].DisplacementReason.ShouldBe(DisplacementReason.PushAdvance);

        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player2Mech.Position.Facing.ShouldBe(HexDirection.Bottom);
        _player1Mech.Position!.Coordinates.ShouldBe(TargetHex);
        _player1Mech.Position.Facing.ShouldBe(HexDirection.Top);
    }

    [Fact]
    public void Enter_PushHit_RollsTheTargetPushHitPsrAfterTheDisplacement()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        Received.InOrder(() =>
        {
            CommandPublisher.Received().PublishCommand(Arg.Is<DisplaceUnitCommand>(c =>
                c.UnitId == _player2Mech.Id && c.DisplacementReason == DisplacementReason.Push));
            MockFallProcessor.Received(1).ProcessPotentialFall(
                _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
                Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                    contexts != null
                    && contexts.Count == 1
                    && contexts[0].RollType == PilotingSkillRollType.PushHit
                    && ((PushHitRollContext)contexts[0]).WaterDepth == 0));
        });
    }

    [Fact]
    public void Enter_PushIntoWater_AddsTheWaterDepthModifierToThePushPsr()
    {
        // Arrange: the destination is Depth 1 water at the same hex level
        SetMap();
        var waterHex = new Hex(DestinationHex);
        waterHex.AddTerrain(new WaterTerrain(-1));
        Game.BattleMap!.AddHex(waterHex);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert: still one push roll, carrying the water depth instead of an extra water-entry roll
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player2Mech.Position.Surface.ShouldBe(HexSurface.Ground);
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null
                && contexts.Count == 1
                && contexts[0].RollType == PilotingSkillRollType.PushHit
                && ((PushHitRollContext)contexts[0]).WaterDepth == 1));
    }

    [Fact]
    public void Enter_MutualPushBothHit_NeitherMovesAndBothRoll()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<DisplaceUnitCommand>().ShouldBeEmpty();
        _player1Mech.Position!.Coordinates.ShouldBe(AttackerHex);
        _player2Mech.Position!.Coordinates.ShouldBe(TargetHex);
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player1Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.PushHit));
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.PushHit));
    }

    [Fact]
    public void Enter_MutualPushOnlyOneHits_IsResolvedAsAStandardPush()
    {
        // Arrange: player 2 resolves first and misses, player 1 hits
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player1Mech))
            .Returns(PushHit());
        MockPhysicalAttackResolver.ResolveAttack(Arg.Is<PhysicalAttackContext>(c => c.Attacker == _player2Mech))
            .Returns(PushMiss());

        // Act
        _sut.Enter();

        // Assert
        var displacements = PublishedCommands().OfType<DisplaceUnitCommand>().ToList();
        displacements.Count.ShouldBe(2);
        displacements.ShouldAllBe(c =>
            c.DisplacementReason == DisplacementReason.Push
            || c.DisplacementReason == DisplacementReason.PushAdvance);
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player1Mech.Position!.Coordinates.ShouldBe(TargetHex);
    }

    [Fact]
    public void Enter_MutualPushBothMiss_NothingHappens()
    {
        // Arrange
        SetMap();
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        Declare(_player2Mech, _player1Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushMiss());

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<DisplaceUnitCommand>().ShouldBeEmpty();
        MockFallProcessor.DidNotReceive().ProcessPotentialFall(
            Arg.Any<Mech>(), Arg.Any<IGame>(), Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts => contexts != null
                && contexts.Any(context => context.RollType == PilotingSkillRollType.PushHit)));
    }

    [Fact]
    public void Enter_BlockedPushDestination_KeepsBothUnitsButStillRollsTheTargetPsr()
    {
        // Arrange: the destination hex is higher than the target's standing level
        SetMap();
        Game.BattleMap!.AddHex(new Hex(DestinationHex, 1));
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<DisplaceUnitCommand>().ShouldBeEmpty();
        _player1Mech.Position!.Coordinates.ShouldBe(AttackerHex);
        _player2Mech.Position!.Coordinates.ShouldBe(TargetHex);
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.PushHit));
    }

    [Fact]
    public void Enter_PushIntoAnOccupiedDestination_MovesTheChainAndAdvancesTheAttacker()
    {
        // Arrange: a third Mech stands in the push destination
        SetMap();
        var player3Id = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(player3Id, "Player 3"));
        var occupant = (Mech)Game.Players[2].Units[0];
        occupant.Deploy(new HexPosition(DestinationHex, HexDirection.Top), null);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());
        StubDominoRoll(occupant);
        StubDominoRoll(_player2Mech);

        // Act
        _sut.Enter();

        // Assert: the chain link is applied first, then the target, then the attacker advance.
        var displacements = PublishedCommands().OfType<DisplaceUnitCommand>().ToList();
        displacements.Count.ShouldBe(3);
        displacements[0].UnitId.ShouldBe(occupant.Id);
        displacements[0].DisplacementReason.ShouldBe(DisplacementReason.DominoEffect);
        displacements[0].ToCoordinates.ShouldBe(DestinationHex.GetNeighbour(HexDirection.Top).ToData());
        displacements[1].UnitId.ShouldBe(_player2Mech.Id);
        displacements[1].DisplacementReason.ShouldBe(DisplacementReason.Push);
        displacements[2].UnitId.ShouldBe(_player1Mech.Id);
        displacements[2].DisplacementReason.ShouldBe(DisplacementReason.PushAdvance);

        occupant.Position!.Coordinates.ShouldBe(DestinationHex.GetNeighbour(HexDirection.Top));
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player1Mech.Position!.Coordinates.ShouldBe(TargetHex);
    }

    [Fact]
    public void Enter_PushIntoAnOccupiedDestination_RollsBothUnitsDominoPsrImmediately()
    {
        // Arrange
        SetMap();
        var player3Id = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(player3Id, "Player 3"));
        var occupant = (Mech)Game.Players[2].Units[0];
        occupant.Deploy(new HexPosition(DestinationHex, HexDirection.Top), null);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());
        StubDominoRoll(occupant);
        StubDominoRoll(_player2Mech);

        // Act
        _sut.Enter();

        // Assert: both units involved in the domino roll immediately, the pushed target included
        // (its PushHit roll is a separate requirement and does not replace the domino roll).
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            occupant,
            Arg.Is<DominoEffectRollContext>(context => context.RollType == PilotingSkillRollType.DominoEffect),
            Game,
            Arg.Any<MovementType>(),
            Arg.Any<HexPosition?>());
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            _player2Mech,
            Arg.Is<DominoEffectRollContext>(context => context.RollType == PilotingSkillRollType.DominoEffect),
            Game,
            Arg.Any<MovementType>(),
            Arg.Any<HexPosition?>());

        // And no domino roll is repeated at the end of the phase.
        MockFallProcessor.DidNotReceive().ProcessPotentialFall(
            Arg.Any<Mech>(), Arg.Any<IGame>(), Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Any(c => c.RollType == PilotingSkillRollType.DominoEffect)));
    }

    [Fact]
    public void Enter_PushChainBlockedAtALaterLink_KeepsTheEarlierDisplacementAndRollsBothUnits()
    {
        // Arrange: the chain runs Top; the hex after the second occupant climbs three levels
        SetMap();
        var blockedHex = DestinationHex.GetNeighbour(HexDirection.Top).GetNeighbour(HexDirection.Top);
        Game.BattleMap!.AddHex(new Hex(blockedHex, 3));
        var player3Id = Guid.NewGuid();
        var player4Id = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(player3Id, "Player 3"));
        Game.HandleCommand(CreateJoinCommand(player4Id, "Player 4"));
        var firstOccupant = (Mech)Game.Players[2].Units[0];
        var secondOccupant = (Mech)Game.Players[3].Units[0];
        firstOccupant.Deploy(new HexPosition(DestinationHex, HexDirection.Top), null);
        secondOccupant.Deploy(new HexPosition(DestinationHex.GetNeighbour(HexDirection.Top), HexDirection.Top), null);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());
        StubDominoRoll(firstOccupant);
        StubDominoRoll(secondOccupant);
        StubDominoRoll(_player2Mech);

        // Act
        _sut.Enter();

        // Assert: no rewind - the first occupant stays in its new hex, the blocked one keeps its own
        // and shares it, so all three domino units roll.
        var displacements = PublishedCommands().OfType<DisplaceUnitCommand>().ToList();
        displacements.Count.ShouldBe(3);
        firstOccupant.Position!.Coordinates.ShouldBe(DestinationHex.GetNeighbour(HexDirection.Top));
        secondOccupant.Position!.Coordinates.ShouldBe(DestinationHex.GetNeighbour(HexDirection.Top));
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player1Mech.Position!.Coordinates.ShouldBe(TargetHex);
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            secondOccupant, Arg.Any<DominoEffectRollContext>(), Game,
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            firstOccupant, Arg.Any<DominoEffectRollContext>(), Game,
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
        MockFallProcessor.Received(1).ProcessMovementAttempt(
            _player2Mech, Arg.Any<DominoEffectRollContext>(), Game,
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
        // The push PSR is still owed to the target as well.
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.PushHit));
    }

    [Fact]
    public void Enter_BlockedDominoChain_MovesNoUnitButStillRollsThePushPsr()
    {
        // Arrange: the chain would climb three levels (above a Mech's two-level climb limit)
        SetMap();
        Game.BattleMap!.AddHex(new Hex(DestinationHex.GetNeighbour(HexDirection.Top), 3));
        var player3Id = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(player3Id, "Player 3"));
        var occupant = (Mech)Game.Players[2].Units[0];
        occupant.Deploy(new HexPosition(DestinationHex, HexDirection.Top), null);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<DisplaceUnitCommand>().ShouldBeEmpty();
        occupant.Position!.Coordinates.ShouldBe(DestinationHex);
        _player2Mech.Position!.Coordinates.ShouldBe(TargetHex);
        _player1Mech.Position!.Coordinates.ShouldBe(AttackerHex);
        MockFallProcessor.Received(1).ProcessPotentialFall(
            _player2Mech, Game, Arg.Any<List<ComponentHitData>>(), Arg.Any<List<PartLocation>?>(),
            Arg.Is<IReadOnlyList<PilotingSkillRollContext>?>(contexts =>
                contexts != null && contexts.Single().RollType == PilotingSkillRollType.PushHit));
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            Arg.Any<Mech>(), Arg.Any<DominoEffectRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
    }

    [Fact]
    public void Enter_PushDominoChainDropsTwoLevels_AppliesTheAutomaticFall()
    {
        // Arrange: the chain pushes the occupant over a two-level drop
        SetMap();
        Game.BattleMap!.AddHex(new Hex(DestinationHex.GetNeighbour(HexDirection.Top), -2));
        var player3Id = Guid.NewGuid();
        Game.HandleCommand(CreateJoinCommand(player3Id, "Player 3"));
        var occupant = (Mech)Game.Players[2].Units[0];
        occupant.Deploy(new HexPosition(DestinationHex, HexDirection.Top), null);
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());
        Game.DiceRoller.RollD6().Returns(new DiceResult(1));
        MockFallProcessor.ProcessMovementAttempt(
                occupant, Arg.Any<CliffFallRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = occupant.Id,
                GameId = Game.Id,
                IsFalling = true,
                LevelsFallen = 2,
                FallingDamageData = new FallingDamageData(
                    HexDirection.Top,
                    new HitLocationsData([], 5),
                    new DiceResult(3),
                    HitDirection.Front)
            });
        MockConsciousnessCalculator.MakeConsciousnessRolls(occupant.Pilot!).Returns([]);
        StubDominoRoll(_player2Mech);

        // Act
        _sut.Enter();

        // Assert: the occupant landed in the lower hex and fell
        occupant.Position!.Coordinates.ShouldBe(DestinationHex.GetNeighbour(HexDirection.Top));
        occupant.IsProne.ShouldBeTrue();
        PublishedCommands().OfType<MechFallCommand>()
            .ShouldContain(command => command.UnitId == occupant.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Enter_PushDropOfOneOrTwoLevels_MovesTheUnitsWithoutAFall(int levelsFallen)
    {
        // Arrange
        SetMap();
        var targetLevel = levelsFallen;
        Game.BattleMap!.AddHex(new Hex(AttackerHex, targetLevel));
        Game.BattleMap.AddHex(new Hex(TargetHex, targetLevel));
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());

        // Act
        _sut.Enter();

        // Assert
        PublishedCommands().OfType<DisplaceUnitCommand>().Count().ShouldBe(2);
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            Arg.Any<Mech>(), Arg.Any<PilotingSkillRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
    }

    [Fact]
    public void Enter_PushDropOfMoreThanTwoLevels_TriggersAnAutomaticFall()
    {
        // Arrange: attacker and target stand at level 3, the destination is at level 0
        SetMap();
        Game.BattleMap!.AddHex(new Hex(AttackerHex, 3));
        Game.BattleMap.AddHex(new Hex(TargetHex, 3));
        Declare(_player1Mech, _player2Mech, PhysicalAttackType.Push, PartLocation.LeftArm, PartLocation.RightArm);
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(PushHit());
        Game.DiceRoller.RollD6().Returns(new DiceResult(1));
        MockFallProcessor.ProcessMovementAttempt(
                _player2Mech, Arg.Any<PilotingSkillRollContext>(), Game, Arg.Any<MovementType>(),
                Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _player2Mech.Id,
                GameId = Game.Id,
                IsFalling = true,
                LevelsFallen = 3,
                FallingDamageData = new FallingDamageData(
                    HexDirection.Bottom,
                    new HitLocationsData([], 0),
                    new DiceResult(1),
                    HitDirection.Front)
            });
        MockConsciousnessCalculator.MakeConsciousnessRolls(_player2Mech.Pilot!).Returns([]);

        // Act
        _sut.Enter();

        // Assert
        _player2Mech.Position!.Coordinates.ShouldBe(DestinationHex);
        _player2Mech.IsProne.ShouldBeTrue();
        PublishedCommands().OfType<MechFallCommand>().ShouldHaveSingleItem()
            .UnitId.ShouldBe(_player2Mech.Id);
    }

    private static readonly IReadOnlyList<PhysicalAttackPsrRequirement> KickHitRequirement =
        [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.KickHit)];

    private static readonly IReadOnlyList<PhysicalAttackPsrRequirement> PushHitRequirement =
        [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Target, PilotingSkillRollType.PushHit)];

    private static PhysicalAttackResolutionData PushHit() =>
        new(PhysicalAttackType.Push, PartLocation.LeftArm, 0,
            new AttackResolutionData(8, [new DiceResult(6), new DiceResult(6)], true, HitDirection.Front, 0),
            PushHitRequirement);

    private static PhysicalAttackResolutionData PushMiss() =>
        new(PhysicalAttackType.Push, PartLocation.LeftArm, 0,
            new AttackResolutionData(8, [new DiceResult(2), new DiceResult(3)], false, HitDirection.Front, 0),
            []);

    private static readonly IReadOnlyList<PhysicalAttackPsrRequirement> KickMissRequirement =
        [new PhysicalAttackPsrRequirement(PhysicalAttackParticipant.Attacker, PilotingSkillRollType.KickMiss)];

    /// <summary>
    /// A domino PSR that succeeds, so the unit stays standing (the phase only publishes falls).
    /// </summary>
    private void StubDominoRoll(Mech mech) =>
        MockFallProcessor.ProcessMovementAttempt(
                mech, Arg.Any<DominoEffectRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = mech.Id,
                GameId = Game.Id,
                IsFalling = false
            });

    private static void Declare(IUnit attacker, IUnit target, PhysicalAttackType attackType, params PartLocation[] limbs) =>
        attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = attackType,
            AttackerLimbs = limbs,
            TargetId = target.Id
        });

    /// <summary>
    /// Every attack misses, keeping the attack type and limb of the context it was asked about.
    /// </summary>
    private void ResolverReturnsMiss() =>
        MockPhysicalAttackResolver.ResolveAttack(Arg.Any<PhysicalAttackContext>())
            .Returns(call =>
            {
                var context = call.Arg<PhysicalAttackContext>();
                return Miss(context.AttackType, context.AttackerLimb);
            });

    private static PhysicalAttackResolutionData Miss(PhysicalAttackType attackType, PartLocation limb,
        IReadOnlyList<PhysicalAttackPsrRequirement>? psrRequirements = null) =>
        new(attackType, limb, 2,
            new AttackResolutionData(8, [new DiceResult(2), new DiceResult(3)], false, HitDirection.Front, 0),
            psrRequirements ?? []);

    private static PhysicalAttackResolutionData Hit(PhysicalAttackType attackType, PartLocation limb,
        PartLocation location, int armorDamage, int structureDamage = 0,
        IReadOnlyList<PhysicalAttackPsrRequirement>? psrRequirements = null) =>
        new(attackType, limb, armorDamage + structureDamage,
            new AttackResolutionData(8, [new DiceResult(6), new DiceResult(6)], true, HitDirection.Front, 0,
                new AttackHitLocationsData(
                    [new LocationHitData(
                        [new LocationDamageData(location, armorDamage, structureDamage, structureDamage > 0)],
                        [], [3, 4], location)],
                    armorDamage + structureDamage, [], 1)),
            psrRequirements ?? []);

    private List<IGameCommand> PublishedCommands() =>
        CommandPublisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(CommandPublisher.PublishCommand))
            .Select(call => (IGameCommand)call.GetArguments()[0]!)
            .ToList();
}
