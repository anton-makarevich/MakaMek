using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class PhysicalAttackResolutionPhaseTests : GamePhaseTestsBase
{
    private PhysicalAttackResolutionPhase _sut = null!;
    private IGamePhase _mockNextPhase = null!;

    protected override void SetupSut()
    {
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.PhysicalAttackResolution, Game).Returns(_mockNextPhase);

        _sut = new PhysicalAttackResolutionPhase(Game);
    }

    [Fact]
    public void Name_ShouldBePhysicalAttackResolution()
    {
        _sut.Name.ShouldBe(PhaseNames.PhysicalAttackResolution);
    }

    [Fact]
    public void Enter_ShouldTransitionToNextPhase()
    {
        // Act
        _sut.Enter();

        // Assert
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.PhysicalAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
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
    public void HandleCommand_ShouldPublishNothing_AndNotChangePhase()
    {
        // Arrange
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
}
