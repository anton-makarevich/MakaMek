using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class StartupUnitCommandTests
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;
    private readonly Guid _gameId;
    private readonly Guid _playerId;
    private readonly Guid _unitId;
    
    private readonly DateTime _timestamp = new DateTime(2021, 1, 1);

    public StartupUnitCommandTests()
    {
        _gameId = Guid.NewGuid();
        _playerId = Guid.NewGuid();
        _unitId = Guid.NewGuid();

        _localizationService = Substitute.For<ILocalizationService>();
        _game = Substitute.For<IGame>();

        // Create real instances instead of mocks
        var player = new Player(_playerId, "Test Player", PlayerControlType.Human);
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = _unitId;

        var mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService);
        var unit = mechFactory.Create(mechData);
        player.AddUnit(unit);

        // Setup game structure
        _game.Players.Returns(new List<IPlayer> { player });

        // Setup localization
        _localizationService.GetString("Command_StartupUnit")
            .Returns("{0} requests to start up {1}.");
    }

    private StartupUnitCommand CreateCommand() => new()
    {
        GameOriginId = _gameId,
        PlayerId = _playerId,
        UnitId = _unitId,
        Timestamp = _timestamp
    };

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Test Player requests to start up LCT-1V.");
        _localizationService.Received(1).GetString("Command_StartupUnit");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Command_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var command = CreateCommand();

        // Assert
        command.GameOriginId.ShouldBe(_gameId);
        command.PlayerId.ShouldBe(_playerId);
        command.UnitId.ShouldBe(_unitId);
        command.Timestamp.ShouldBe(_timestamp);
    }
}
