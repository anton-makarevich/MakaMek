using Shouldly;
using NSubstitute;
using Sanet.MekForge.Core.Models.Game;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Game.Players;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Services.Localization;
using Sanet.MekForge.Core.Tests.Data.Community;
using Sanet.MekForge.Core.Utils;
using Sanet.MekForge.Core.Utils.TechRules;

namespace Sanet.MekForge.Core.Tests.Models.Game.Commands.Client;

public class DeployUnitCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1");
    private readonly HexCoordinates _position = new(4, 5);
    private readonly DeployUnitCommand _sut;

    public DeployUnitCommandTests()
    {
        _game.Players.Returns([_player1]);
        var unitData = MechFactoryTests.CreateDummyMechData();
        Unit unit = new MechFactory(new ClassicBattletechRulesProvider()).Create(unitData);
        _player1.AddUnit(unit);
        
        _sut = new DeployUnitCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            UnitId = unit.Id,
            Position = _position.ToData(),
            Direction = (int)HexDirection.TopRight
        };
    }

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        _localizationService.GetString("Command_DeployUnit").Returns("formatted deploy command");

        // Act
        var result = _sut.Format(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted deploy command");
        _localizationService.Received(1).GetString("Command_DeployUnit");
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = _sut with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = _sut with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}