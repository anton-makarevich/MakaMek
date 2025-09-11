using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class PilotConsciousnessRollCommandTests
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;
    private readonly Guid _unitId;
    private readonly Guid _pilotId;

    public PilotConsciousnessRollCommandTests()
    {
        _localizationService = new FakeLocalizationService();
        
        _game = Substitute.For<IGame>();
        var pilot = Substitute.For<IPilot>();
        _unitId = Guid.NewGuid();
        _pilotId = Guid.NewGuid();

        // Setup pilot
        pilot.Id.Returns(_pilotId);
        pilot.Name.Returns("John Doe");

        // Setup unit
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = _unitId;
        var unit = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService).Create(unitData);
        unit.AssignPilot(pilot);

        // Setup player
        var player = Substitute.For<IPlayer>();
        player.Units.Returns(new List<Sanet.MakaMek.Core.Models.Units.Unit> { unit });

        // Setup game
        _game.Players.Returns(new List<IPlayer> { player });
    }

    [Fact]
    public void Render_WithSuccessfulConsciousnessRoll_ReturnsFormattedString()
    {
        // Arrange
        var sut = new PilotConsciousnessRollCommand
        {
            UnitId = _unitId,
            PilotId = _pilotId,
            ConsciousnessNumber = 7,
            DiceResults = [4, 5],
            IsSuccessful = true,
            IsRecoveryAttempt = false,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("John Doe consciousness roll succeeded");
        result.ShouldContain("Consciousness Number: 7");
        result.ShouldContain("Roll Result: 9");
    }

    [Fact]
    public void Render_WithFailedConsciousnessRoll_ReturnsFormattedString()
    {
        // Arrange
        var sut = new PilotConsciousnessRollCommand
        {
            UnitId = _unitId,
            PilotId = _pilotId,
            ConsciousnessNumber = 10,
            DiceResults = [2, 3],
            IsSuccessful = false,
            IsRecoveryAttempt = false,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("John Doe consciousness roll failed");
        result.ShouldContain("Consciousness Number: 10");
        result.ShouldContain("Roll Result: 5");
    }

    [Fact]
    public void Render_WithSuccessfulRecoveryRoll_ReturnsFormattedString()
    {
        // Arrange
        var sut = new PilotConsciousnessRollCommand
        {
            UnitId = _unitId,
            PilotId = _pilotId,
            ConsciousnessNumber = 5,
            DiceResults = [3, 4],
            IsSuccessful = true,
            IsRecoveryAttempt = true,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("John Doe consciousness recovery roll succeeded");
        result.ShouldContain("Consciousness Number: 5");
        result.ShouldContain("Roll Result: 7");
    }

    [Fact]
    public void Render_WithFailedRecoveryRoll_ReturnsFormattedString()
    {
        // Arrange
        var sut = new PilotConsciousnessRollCommand
        {
            UnitId = _unitId,
            PilotId = _pilotId,
            ConsciousnessNumber = 11,
            DiceResults = [1, 6],
            IsSuccessful = false,
            IsRecoveryAttempt = true,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
        
        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("John Doe consciousness recovery roll failed");
        result.ShouldContain("Consciousness Number: 11");
        result.ShouldContain("Roll Result: 7");
    }

    [Fact]
    public void Render_WithNonExistentUnit_ReturnsEmptyString()
    {
        // Arrange
        var sut = new PilotConsciousnessRollCommand
        {
            UnitId = Guid.NewGuid(), // Different unit ID
            PilotId = _pilotId,
            ConsciousnessNumber = 7,
            DiceResults = [4, 5],
            IsSuccessful = true,
            IsRecoveryAttempt = false,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe(string.Empty);
    }
}
