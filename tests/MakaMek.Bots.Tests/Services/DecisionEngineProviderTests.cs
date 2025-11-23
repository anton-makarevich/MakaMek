using NSubstitute;
using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Services;

public class DecisionEngineProviderTests
{
    private readonly IClientGame _clientGame;
    private readonly DecisionEngineProvider _sut;

    public DecisionEngineProviderTests()
    {
        _clientGame = Substitute.For<IClientGame>();
        _clientGame.Id.Returns(Guid.NewGuid());
        
        _sut = new DecisionEngineProvider(_clientGame, BotDifficulty.Easy);
    }

    [Fact]
    public void Constructor_ShouldInitializeAllPhaseEngines()
    {
        // Act - Constructor already called in setup
        
        // Assert - Verify all phase engines are available
        _sut.GetEngineForPhase(PhaseNames.Deployment).ShouldNotBeNull();
        _sut.GetEngineForPhase(PhaseNames.Movement).ShouldNotBeNull();
        _sut.GetEngineForPhase(PhaseNames.WeaponsAttack).ShouldNotBeNull();
        _sut.GetEngineForPhase(PhaseNames.End).ShouldNotBeNull();
    }

    [Fact]
    public void GetEngineForPhase_WhenDeploymentPhase_ShouldReturnDeploymentEngine()
    {
        // Act
        var engine = _sut.GetEngineForPhase(PhaseNames.Deployment);
        
        // Assert
        engine.ShouldNotBeNull();
        engine.ShouldBeOfType<DeploymentEngine>();
    }

    [Fact]
    public void GetEngineForPhase_WhenMovementPhase_ShouldReturnMovementEngine()
    {
        // Act
        var engine = _sut.GetEngineForPhase(PhaseNames.Movement);
        
        // Assert
        engine.ShouldNotBeNull();
        engine.ShouldBeOfType<MovementEngine>();
    }

    [Fact]
    public void GetEngineForPhase_WhenWeaponsAttackPhase_ShouldReturnWeaponsEngine()
    {
        // Act
        var engine = _sut.GetEngineForPhase(PhaseNames.WeaponsAttack);
        
        // Assert
        engine.ShouldNotBeNull();
        engine.ShouldBeOfType<WeaponsEngine>();
    }

    [Fact]
    public void GetEngineForPhase_WhenEndPhase_ShouldReturnEndPhaseEngine()
    {
        // Act
        var engine = _sut.GetEngineForPhase(PhaseNames.End);
        
        // Assert
        engine.ShouldNotBeNull();
        engine.ShouldBeOfType<EndPhaseEngine>();
    }

    [Fact]
    public void GetEngineForPhase_WhenUnknownPhase_ShouldReturnNull()
    {
        // Act
        var engine = _sut.GetEngineForPhase((PhaseNames)999);
        
        // Assert
        engine.ShouldBeNull();
    }

    [Fact]
    public void GetEngineForPhase_WhenCalledMultipleTimes_ShouldReturnSameInstance()
    {
        // Act
        var engine1 = _sut.GetEngineForPhase(PhaseNames.Deployment);
        var engine2 = _sut.GetEngineForPhase(PhaseNames.Deployment);
        
        // Assert
        engine1.ShouldBeSameAs(engine2);
    }

    [Fact]
    public void Constructor_WithDifferentDifficulties_ShouldCreateProviders()
    {
        // Arrange & Act
        var easyProvider = new DecisionEngineProvider(_clientGame, BotDifficulty.Easy);
        var mediumProvider = new DecisionEngineProvider(_clientGame, BotDifficulty.Medium);
        var hardProvider = new DecisionEngineProvider(_clientGame, BotDifficulty.Hard);
        
        // Assert
        easyProvider.GetEngineForPhase(PhaseNames.Deployment).ShouldNotBeNull();
        mediumProvider.GetEngineForPhase(PhaseNames.Deployment).ShouldNotBeNull();
        hardProvider.GetEngineForPhase(PhaseNames.Deployment).ShouldNotBeNull();
    }
}
