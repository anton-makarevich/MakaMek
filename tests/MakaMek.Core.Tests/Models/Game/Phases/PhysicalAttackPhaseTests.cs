using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class PhysicalAttackPhaseTests : GamePhaseTestsBase
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);

    private PhysicalAttackPhase _sut = null!;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private IPlayer _player1 = null!;
    private IPlayer _player2 = null!;
    private IUnit _attacker = null!;
    private IUnit _secondAttacker = null!;
    private IUnit _target = null!;
    private IGamePhase _mockNextPhase = null!;

    protected override void SetupSut()
    {
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.PhysicalAttack, Game).Returns(_mockNextPhase);

        _sut = new PhysicalAttackPhase(Game);

        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1", 2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));
        SetMap();

        _player1 = Game.Players[0];
        _player2 = Game.Players[1];
        _attacker = _player1.Units[0];
        _secondAttacker = _player1.Units[1];
        _target = _player2.Units[0];

        // Player 2 won the initiative, so player 1 declares first
        Game.SetInitiativeOrder(new List<IPlayer> { _player2, _player1 });
    }

    /// <summary>
    /// By default the attacker and the target face each other and the second attacker is far away.
    /// </summary>
    private void DeployUnits(HexPosition? targetPosition = null, HexPosition? secondAttackerPosition = null)
    {
        _attacker.Deploy(new HexPosition(AttackerHex, HexDirection.Top), null);
        _target.Deploy(targetPosition
                       ?? new HexPosition(AttackerHex.GetNeighbour(HexDirection.Top), HexDirection.Bottom), null);
        _secondAttacker.Deploy(secondAttackerPosition
                               ?? new HexPosition(new HexCoordinates(1, 1), HexDirection.Top), null);
    }

    private PhysicalAttackPhase CreateEnabledSut(HexPosition? targetPosition = null,
        HexPosition? secondAttackerPosition = null)
    {
        DeployUnits(targetPosition, secondAttackerPosition);
        return new PhysicalAttackPhase(Game) { IsDeclarationEnabled = true };
    }

    [Fact]
    public void Enter_ShouldTransitionToNextPhaseImmediately()
    {
        // Act
        _sut.Enter();

        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.PhysicalAttack, Game);
        _mockNextPhase.Received(1).Enter();
        Game.PhaseStepState.ShouldBeNull();
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
    }

    [Fact]
    public void Enter_ShouldPublishOnlyChangePhaseCommand()
    {
        // Arrange
        CommandPublisher.ClearReceivedCalls();

        // Act
        _sut.Enter();

        // Assert
        var publishedCommands = CommandPublisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(CommandPublisher.PublishCommand))
            .Select(call => call.GetArguments()[0])
            .ToList();

        publishedCommands.Count.ShouldBe(1);
        publishedCommands[0].ShouldBeOfType<ChangePhaseCommand>();
    }

    [Fact]
    public void Enter_WhenDeclarationEnabled_ActivatesTheInitiativeLoserForItsUnitsWithOptions()
    {
        // Arrange
        var sut = CreateEnabledSut();

        // Act
        sut.Enter();

        // Assert: player 1 has two units but only one of them can attack
        Game.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(_player1Id);
        Game.PhaseStepState!.Value.UnitsToPlay.ShouldBe(1);
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ChangeActivePlayerCommand>(c =>
            c.PlayerId == _player1Id && c.UnitsToPlay == 1));
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.PhysicalAttack, Game);
    }

    [Fact]
    public void Enter_PlayerWithNoOption_IsNeverActivatedAndIsLogged()
    {
        // Arrange: the target turns its back, so it can neither punch nor kick the attacker
        var sut = CreateEnabledSut(new HexPosition(AttackerHex.GetNeighbour(HexDirection.Top), HexDirection.Top));

        // Act
        sut.Enter();
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id));

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Is<ChangeActivePlayerCommand>(c =>
            c.PlayerId == _player2Id));
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.PhysicalAttack, Game);
        Game.Logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Skipping Player 2 in the physical attack phase")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void Enter_WhenNoPlayerHasAnOption_GoesStraightToTheNextPhase()
    {
        // Arrange
        var sut = CreateEnabledSut(new HexPosition(new HexCoordinates(9, 9), HexDirection.Top));

        // Act
        sut.Enter();

        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.PhysicalAttack, Game);
        _mockNextPhase.Received(1).Enter();
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ChangeActivePlayerCommand>());
    }

    [Fact]
    public void HandleCommand_ValidPunch_StoresTheDeclarationBroadcastsItAndMovesOn()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();
        var declaration = Punch(PartLocation.LeftArm, PartLocation.RightArm);

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, declaration));

        // Assert
        _attacker.DeclaredPhysicalAttack!.AttackerLimbs.ShouldBe(declaration.AttackerLimbs);
        _attacker.DeclaredPhysicalAttack.TargetId.ShouldBe(_target.Id);
        CommandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(c =>
            c.UnitId == _attacker.Id && c.GameOriginId == Game.Id && c.Declaration == declaration));
        Game.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(_player2Id);
    }

    [Fact]
    public void HandleCommand_KickWithTheChosenLeg_StoresThatLeg()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Kick(PartLocation.RightLeg)));

        // Assert
        _attacker.DeclaredPhysicalAttack!.AttackType.ShouldBe(PhysicalAttackType.Kick);
        _attacker.DeclaredPhysicalAttack.AttackerLimbs.ShouldBe([PartLocation.RightLeg]);
    }

    [Fact]
    public void HandleCommand_NoDeclaration_IsBroadcastAndMovesOnWithoutStoringAnything()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id));

        // Assert
        _attacker.HasDeclaredPhysicalAttack.ShouldBeFalse();
        CommandPublisher.Received(1).PublishCommand(Arg.Is<PhysicalAttackCommand>(c =>
            c.UnitId == _attacker.Id && c.Declaration == null));
        Game.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(_player2Id);
    }

    [Fact]
    public void HandleCommand_WhenWrongPlayer_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player2Id, _target.Id, Punch(PartLocation.LeftArm)));

        // Assert
        VerifyRejected(ErrorCode.InvalidGameState);
        _target.HasDeclaredPhysicalAttack.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_ForAUnitWithNoOption_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _secondAttacker.Id));

        // Assert
        VerifyRejected(ErrorCode.InvalidGameState);
    }

    [Fact]
    public void HandleCommand_SecondDeclarationForTheSameUnit_ShouldReturnAnError()
    {
        // Arrange: both of player 1's units can attack, so player 1 stays active after the first
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var sut = CreateEnabledSut(secondAttackerPosition:
            new HexPosition(targetHex.GetNeighbour(HexDirection.TopLeft), HexDirection.BottomRight));
        sut.Enter();
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.LeftArm)));
        CommandPublisher.ClearReceivedCalls();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.RightArm)));

        // Assert
        VerifyRejected(ErrorCode.InvalidGameState);
        _attacker.DeclaredPhysicalAttack!.AttackerLimbs.ShouldBe([PartLocation.LeftArm]);
        Game.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(_player1Id);
        Game.PhaseStepState!.Value.UnitsToPlay.ShouldBe(1);
    }

    [Fact]
    public void HandleCommand_KickWithBothLegs_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Kick(PartLocation.LeftLeg, PartLocation.RightLeg)));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
        _attacker.HasDeclaredPhysicalAttack.ShouldBeFalse();
        Game.PhaseStepState!.Value.ActivePlayer.Id.ShouldBe(_player1Id);
    }

    [Fact]
    public void HandleCommand_PunchWithAnArmThatCannotReach_ShouldReturnAnError()
    {
        // Arrange: the target is in the right side arc, so only the right arm reaches it
        var sut = CreateEnabledSut(
            new HexPosition(AttackerHex.GetNeighbour(HexDirection.BottomRight), HexDirection.TopLeft));
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.LeftArm, PartLocation.RightArm)));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
        _attacker.HasDeclaredPhysicalAttack.ShouldBeFalse();
    }

    [Fact]
    public void HandleCommand_SameArmTwice_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.LeftArm, PartLocation.LeftArm)));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void HandleCommand_NoLimbs_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch()));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void HandleCommand_UnknownTarget_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id,
            Punch(PartLocation.LeftArm) with { TargetId = Guid.NewGuid() }));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void HandleCommand_PushNotYetSupported_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id,
            Punch(PartLocation.LeftArm, PartLocation.RightArm) with { AttackType = PhysicalAttackType.Push }));

        // Assert
        VerifyRejected(ErrorCode.ValidationFailed);
    }

    [Fact]
    public void HandleCommand_Rejected_EchoesTheIdempotencyKey()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();
        var key = Guid.NewGuid();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch()) with { IdempotencyKey = key });

        // Assert
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(c => c.IdempotencyKey == key));
    }

    [Fact]
    public void HandleCommand_WhileDeclarationIsDisabled_ShouldReturnAnError()
    {
        // Arrange
        DeployUnits();
        Game.SetActivePlayer(_player1, 2);

        // Act
        _sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.LeftArm)));

        // Assert
        VerifyRejected(ErrorCode.InvalidGameState);
    }

    [Fact]
    public void HandleCommand_WhenInvalidCommand_ShouldIgnoreCommand()
    {
        // Arrange
        Game.SetActivePlayer(Game.Players[0], 2);

        // Act
        _sut.HandleCommand(new WeaponAttackDeclarationCommand // Wrong command type
        {
            GameOriginId = Game.Id,
            PlayerId = Game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = _attacker.Id,
            WeaponTargets = []
        });

        // Assert
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackDeclarationCommand>());
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<ErrorCommand>());
        MockPhaseManager.DidNotReceive().GetNextPhase(PhaseNames.PhysicalAttack, Game);
    }

    [Fact]
    public void HandleCommand_AfterExit_ShouldReturnAnError()
    {
        // Arrange
        var sut = CreateEnabledSut();
        sut.Enter();
        sut.Exit();

        // Act
        sut.HandleCommand(CreateCommand(_player1Id, _attacker.Id, Punch(PartLocation.LeftArm)));

        // Assert
        VerifyRejected(ErrorCode.InvalidGameState);
        _attacker.DeclaredPhysicalAttack.ShouldBeNull();
    }

    private void VerifyRejected(ErrorCode errorCode)
    {
        CommandPublisher.Received(1).PublishCommand(Arg.Is<ErrorCommand>(c =>
            c.ErrorCode == errorCode && c.GameOriginId == Game.Id));
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<PhysicalAttackCommand>());
    }

    private PhysicalAttackCommand CreateCommand(Guid playerId, Guid unitId,
        PhysicalAttackDeclarationData? declaration = null) => new()
    {
        GameOriginId = Guid.NewGuid(),
        PlayerId = playerId,
        UnitId = unitId,
        Declaration = declaration
    };

    private PhysicalAttackDeclarationData Punch(params PartLocation[] arms) => new()
    {
        AttackType = PhysicalAttackType.Punch,
        AttackerLimbs = arms,
        TargetId = _target.Id
    };

    private PhysicalAttackDeclarationData Kick(params PartLocation[] legs) => new()
    {
        AttackType = PhysicalAttackType.Kick,
        AttackerLimbs = legs,
        TargetId = _target.Id
    };
}
