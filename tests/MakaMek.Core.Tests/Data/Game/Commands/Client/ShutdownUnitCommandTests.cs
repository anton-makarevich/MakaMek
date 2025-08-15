using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class ShutdownUnitCommandTests
{
    private readonly ILocalizationService _localizationService;
    private readonly IGame _game;
    private readonly Guid _gameId;
    private readonly Guid _playerId;
    private readonly Guid _unitId;
    
    private readonly DateTime _timestamp = new DateTime(2021, 1, 1);

    public ShutdownUnitCommandTests()
    {
        _gameId = Guid.NewGuid();
        _playerId = Guid.NewGuid();
        _unitId = Guid.NewGuid();

        _localizationService = Substitute.For<ILocalizationService>();
        _game = Substitute.For<IGame>();

        // Create real instances instead of mocks
        var player = new Player(_playerId, "Test Player");
        var mechData = MechFactoryTests.CreateDummyMechData();
        mechData.Id = _unitId;
        var rulesProvider = Substitute.For<IRulesProvider>();
        rulesProvider.GetStructureValues(20).Returns(new Dictionary<PartLocation, int>
        {
            { PartLocation.Head, 8 },
            { PartLocation.CenterTorso, 10 },
            { PartLocation.LeftTorso, 8 },
            { PartLocation.RightTorso, 8 },
            { PartLocation.LeftArm, 4 },
            { PartLocation.RightArm, 4 },
            { PartLocation.LeftLeg, 8 },
            { PartLocation.RightLeg, 8 }
        });
        var mechFactory = new MechFactory(rulesProvider, _localizationService);
        var unit = mechFactory.Create(mechData);
        player.AddUnit(unit);

        // Setup game structure
        _game.Players.Returns(new List<IPlayer> { player });

        // Setup localization
        _localizationService.GetString("Command_ShutdownUnit")
            .Returns("{0} requests to shut down {1}.");
    }

    private ShutdownUnitCommand CreateCommand() => new()
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
        result.ShouldBe("Test Player requests to shut down Locust LCT-1V.");
        _localizationService.Received(1).GetString("Command_ShutdownUnit");
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
    public void Properties_ShouldBeSetCorrectly()
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
