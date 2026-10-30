using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement.Actions;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Models.Game.Phases;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Displacement;

public class DominoEffectRollActionTests : GamePhaseTestsBase
{
    private Mech _deployedMech = null!;
    private Mech _proneMech = null!;
    private Mech _destroyedMech = null!;
    private Mech _undeployedMech = null!;

    protected override void SetupSut()
    {
        Game.HandleCommand(CreateJoinCommand(Guid.NewGuid(), "Player 1", unitsCount: 4));
        var units = Game.Players[0].Units;
        _deployedMech = (Mech)units[0];
        _proneMech = (Mech)units[1];
        _destroyedMech = (Mech)units[2];
        _undeployedMech = (Mech)units[3];

        _deployedMech.Deploy(new HexPosition(1, 2, HexDirection.Top), null);
        _proneMech.Deploy(new HexPosition(2, 2, HexDirection.Top), null);
        _destroyedMech.Deploy(new HexPosition(3, 2, HexDirection.Top), null);

        // A fall during the same event makes a Mech prone.
        Game.OnMechFalling(new MechFallCommand
        {
            UnitId = _proneMech.Id,
            DamageData = new FallingDamageData(
                HexDirection.Top,
                new HitLocationsData([], 5),
                new DiceResult(3),
                HitDirection.Front),
            GameOriginId = Game.Id
        });
        _proneMech.IsProne.ShouldBeTrue();

        _destroyedMech.Pilot!.Kill();
        _destroyedMech.UpdateDestroyedStatus();
        _destroyedMech.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void Process_SkipsDestroyedProneAndUndeployedMechs()
    {
        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Any<DominoEffectRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = false
            });

        var sut = new DominoEffectRollAction(
            [_deployedMech, _proneMech, _destroyedMech, _undeployedMech], MockFallProcessor);

        sut.Process(Game);

        MockFallProcessor.Received(1).ProcessMovementAttempt(
            _deployedMech,
            Arg.Any<DominoEffectRollContext>(),
            Game,
            Arg.Any<MovementType>(),
            Arg.Any<HexPosition?>());
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            _proneMech, Arg.Any<PilotingSkillRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            _destroyedMech, Arg.Any<PilotingSkillRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            _undeployedMech, Arg.Any<PilotingSkillRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
    }

    [Fact]
    public void Process_SuccessfulPsr_ReturnsTheReportCommand()
    {
        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Any<DominoEffectRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = false,
                PilotingSkillRoll = new PilotingSkillRollData
                {
                    RollContext = new DominoEffectRollContext(),
                    DiceResults = [3, 4],
                    IsSuccessful = true,
                    PsrBreakdown = new PsrBreakdown
                    {
                        BasePilotingSkill = 5,
                        Modifiers = []
                    }
                }
            });

        var sut = new DominoEffectRollAction([_deployedMech], MockFallProcessor);

        var commands = sut.Process(Game);

        var command = commands.ShouldHaveSingleItem().ShouldBeOfType<MechFallCommand>();
        command.UnitId.ShouldBe(_deployedMech.Id);
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll!.RollContext.ShouldBeOfType<DominoEffectRollContext>();
        command.DamageData.ShouldBeNull();
        _deployedMech.IsProne.ShouldBeFalse();
    }

    [Fact]
    public void Process_FailedPsr_AppliesTheFallThroughApplyFallAction()
    {
        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Any<DominoEffectRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = true,
                LevelsFallen = 0,
                FallingDamageData = new FallingDamageData(
                    HexDirection.Top,
                    new HitLocationsData([], 5),
                    new DiceResult(3),
                    HitDirection.Front),
                PilotingSkillRoll = new PilotingSkillRollData
                {
                    RollContext = new DominoEffectRollContext(),
                    DiceResults = [1, 1],
                    IsSuccessful = false,
                    PsrBreakdown = new PsrBreakdown
                    {
                        BasePilotingSkill = 5,
                        Modifiers = []
                    }
                }
            });
        MockConsciousnessCalculator.MakeConsciousnessRolls(_deployedMech.Pilot!).Returns([]);

        var sut = new DominoEffectRollAction([_deployedMech], MockFallProcessor);

        var commands = sut.Process(Game);

        commands.OfType<MechFallCommand>().ShouldHaveSingleItem().UnitId.ShouldBe(_deployedMech.Id);
        _deployedMech.IsProne.ShouldBeTrue();
    }

    [Fact]
    public void Process_IgnoresTheMechsLastMovementType()
    {
        // MovementTaken is turn-scoped: a mech that jumped during the Movement Phase still carries
        // the Jump movement type when the domino PSR is rolled in another phase.
        _deployedMech.Move(
            MovementPath.CreateSingleSegmentPath(_deployedMech.Position!, MovementType.Jump), null);
        _deployedMech.MovementTaken!.MovementType.ShouldBe(MovementType.Jump);

        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Any<DominoEffectRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = false
            });

        var sut = new DominoEffectRollAction([_deployedMech], MockFallProcessor);

        sut.Process(Game);

        MockFallProcessor.Received(1).ProcessMovementAttempt(
            _deployedMech,
            Arg.Is<DominoEffectRollContext>(context => context.RollType == PilotingSkillRollType.DominoEffect),
            Game,
            MovementType.StandingStill,
            Arg.Any<HexPosition?>());
    }

    [Fact]
    public void Process_AutomaticFall_AppliesTheFallBeforeThePsrRolls()
    {
        SetMap();
        DiceRoller.RollD6().Returns(new DiceResult(1));
        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Any<CliffFallRollContext>(), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = true,
                LevelsFallen = 2,
                FallingDamageData = new FallingDamageData(
                    HexDirection.Top,
                    new HitLocationsData([], 5),
                    new DiceResult(3),
                    HitDirection.Front)
            });
        MockConsciousnessCalculator.MakeConsciousnessRolls(_deployedMech.Pilot!).Returns([]);

        var sut = new DominoEffectRollAction(
            [_deployedMech], MockFallProcessor, [new DominoFall(_deployedMech, 2)]);

        var commands = sut.Process(Game);

        commands.OfType<MechFallCommand>().ShouldHaveSingleItem().UnitId.ShouldBe(_deployedMech.Id);
        _deployedMech.IsProne.ShouldBeTrue();
        // The fallen Mech is prone, so it makes no further domino roll.
        MockFallProcessor.DidNotReceive().ProcessMovementAttempt(
            _deployedMech, Arg.Any<DominoEffectRollContext>(), Arg.Any<IGame>(),
            Arg.Any<MovementType>(), Arg.Any<HexPosition?>());
    }

    [Fact]
    public void Process_DisplacedIntoWater_PassesTheWaterDepthToThePsr()
    {
        SetMap();
        var waterHex = new Hex(new HexCoordinates(1, 2));
        waterHex.AddTerrain(new WaterTerrain(-1));
        Game.BattleMap!.AddHex(waterHex);
        MockFallProcessor.ProcessMovementAttempt(
                _deployedMech, Arg.Is<DominoEffectRollContext>(context => context.WaterDepth == 1), Game,
                Arg.Any<MovementType>(), Arg.Any<HexPosition?>())
            .Returns(new FallContextData
            {
                UnitId = _deployedMech.Id,
                GameId = Game.Id,
                IsFalling = false
            });

        var sut = new DominoEffectRollAction([_deployedMech], MockFallProcessor);

        sut.Process(Game);

        MockFallProcessor.Received(1).ProcessMovementAttempt(
            _deployedMech,
            Arg.Is<DominoEffectRollContext>(context =>
                context.RollType == PilotingSkillRollType.DominoEffect && context.WaterDepth == 1),
            Game,
            Arg.Any<MovementType>(),
            Arg.Any<HexPosition?>());
    }
}
