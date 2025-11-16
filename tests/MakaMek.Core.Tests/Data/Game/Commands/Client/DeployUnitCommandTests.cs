using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class DeployUnitCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1", PlayerControlType.Local);
    private readonly HexCoordinates _position = new(4, 5);
    private readonly DeployUnitCommand _sut;

    public DeployUnitCommandTests()
    {
        _game.Players.Returns([_player1]);
        var unitData = MechFactoryTests.CreateDummyMechData();
        Unit unit = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            _localizationService).Create(unitData);
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
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        _localizationService.GetString("Command_DeployUnit").Returns("formatted deploy command");

        // Act
        var result = _sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("formatted deploy command");
        _localizationService.Received(1).GetString("Command_DeployUnit");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = _sut with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = _sut with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}