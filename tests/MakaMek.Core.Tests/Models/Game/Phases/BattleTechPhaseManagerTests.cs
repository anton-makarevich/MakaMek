using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class BattleTechPhaseManagerTests
{
    private readonly BattleTechPhaseManager _sut;
    private readonly ServerGame _game;
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();

    public BattleTechPhaseManagerTests()
    {
        _sut = new BattleTechPhaseManager();
        _game = new ServerGame(
            Substitute.For<IRulesProvider>(), // rulesProvider
            _mechFactory,
            Substitute.For<ICommandPublisher>(), // commandPublisher
            Substitute.For<IDiceRoller>(), // diceRoller
            Substitute.For<IToHitCalculator>(), // toHitCalculator
            Substitute.For<ICriticalHitsCalculator>(),
            Substitute.For<IPilotingSkillCalculator>(),
            Substitute.For<IConsciousnessCalculator>(),
            Substitute.For<IFallProcessor>(),
            _sut  // phaseManager
        );
        _game.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(5,5, new SingleTerrainGenerator(5,5, new ClearTerrain())));
    }

    [Fact]
    public void GetNextPhase_ShouldReturnDeploymentPhase_WhenCurrentPhaseIsStart()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.Start, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<DeploymentPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Deployment);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnInitiativePhase_WhenCurrentPhaseIsDeployment()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.Deployment, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<InitiativePhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Initiative);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnMovementPhase_WhenCurrentPhaseIsInitiative()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.Initiative, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<MovementPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Movement);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnWeaponsAttackPhase_WhenCurrentPhaseIsMovement()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.Movement, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<WeaponsAttackPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.WeaponsAttack);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnAttackResolutionPhase_WhenCurrentPhaseIsWeaponsAttack()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.WeaponsAttack, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<WeaponAttackResolutionPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.WeaponAttackResolution);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnHeatPhase_WhenCurrentPhaseIsAttackResolution()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.WeaponAttackResolution, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<HeatPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Heat);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnEndPhase_WhenCurrentPhaseIsHeat()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.Heat, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<EndPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.End);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnInitiativePhase_WhenCurrentPhaseIsEnd()
    {
        // Act
        var nextPhase = _sut.GetNextPhase(PhaseNames.End, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<InitiativePhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Initiative);
    }

    [Fact]
    public void GetNextPhase_ShouldReturnStartPhase_WhenCurrentPhaseIsUnknown()
    {
        // Act - Use a value that's not in the switch statement
        var nextPhase = _sut.GetNextPhase((PhaseNames)999, _game);

        // Assert
        nextPhase.ShouldNotBeNull();
        nextPhase.ShouldBeOfType<StartPhase>();
        nextPhase.Name.ShouldBe(PhaseNames.Start);
    }

    [Fact]
    public void PhaseOrder_ShouldFollowCorrectSequence()
    {
        // This test verifies the entire phase sequence
        
        // Start with the Start phase
        var phase = _sut.GetNextPhase((PhaseNames)999, _game);
        phase.ShouldBeOfType<StartPhase>();
        
        // Follow the sequence
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<DeploymentPhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<InitiativePhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<MovementPhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<WeaponsAttackPhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<WeaponAttackResolutionPhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<HeatPhase>();
        
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<EndPhase>();
        
        // Verify it loops back to Initiative
        phase = _sut.GetNextPhase(phase.Name, _game);
        phase.ShouldBeOfType<InitiativePhase>();
    }
}
