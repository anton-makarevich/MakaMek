using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class MechStandUpCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Unit _unit;

    private PilotingSkillRollData CreateTestPsrData()
    {
        return new PilotingSkillRollData
        {
            RollType = PilotingSkillRollType.StandupAttempt,
            DiceResults = [3,4],
            IsSuccessful = true,
            PsrBreakdown = new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new FallProcessorTests.TestModifier { Name = "Test Modifier", Value = 1 }]
            }
        };
    }

    public MechStandUpCommandTests()
    {
        var player = new Player(Guid.NewGuid(), "Player 1");

        // Create unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        
        _unit = mechFactory.Create(unitData);
        
        // Add unit to player
        player.AddUnit(_unit);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { player });
        
        // Setup localization service
        _localizationService.GetString("Command_MechStandup")
            .Returns("{0} stood up {1}");
        
        // PSR rendering 
        _localizationService.GetString("PilotingSkillRollType_StandUp").Returns("Stand Up");
        _localizationService.GetString("Command_PilotingSkillRoll_Success").Returns("{0} roll succeeded");
        _localizationService.GetString("Command_PilotingSkillRoll_Failure").Returns("{0} roll failed");
        _localizationService.GetString("Command_PilotingSkillRoll_ImpossibleRoll").Returns("{0} roll is impossible");
        _localizationService.GetString("Command_PilotingSkillRoll_BasePilotingSkill")
            .Returns("Base Piloting Skill: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_Modifiers").Returns("Modifiers:");
        _localizationService.GetString("Command_PilotingSkillRoll_TotalTargetNumber")
            .Returns("Total Target Number: {0}");
        _localizationService.GetString("Command_PilotingSkillRoll_RollResult").Returns("Roll Result: {0}");
    }

    private MechStandUpCommand CreateBasicStandUpCommand()
    {
        return new MechStandUpCommand
        {
            GameOriginId = _gameId,
            UnitId = _unit.Id,
            PilotingSkillRoll = CreateTestPsrData(),
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatSuccessfulStandUp_Correctly()
    {
        // Arrange
        var sut = CreateBasicStandUpCommand();
        var psrText = sut.PilotingSkillRoll.Render(_localizationService);

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Locust LCT-1V stood up");
        result.ShouldContain(psrText);
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var sut = CreateBasicStandUpCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
