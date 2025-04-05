using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Client;

public class WeaponConfigurationCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1 = new Player(Guid.NewGuid(), "Player 1");
    private readonly Unit _unit;

    public WeaponConfigurationCommandTests()
    {
        _game.Players.Returns([_player1]);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _unit = new MechFactory(new ClassicBattletechRulesProvider()).Create(unitData);
        _player1.AddUnit(_unit);

        _localizationService.GetString("Command_WeaponConfiguration_TorsoRotation")
            .Returns("{0}'s {1} rotates torso to face {2}");
        _localizationService.GetString("Command_WeaponConfiguration_ArmsFlip")
            .Returns("{0}'s {1} flips arms {2}");
        _localizationService.GetString("Direction_Forward")
            .Returns("forward");
        _localizationService.GetString("Direction_Backward")
            .Returns("backward");
    }

    private WeaponConfigurationCommand CreateCommand()
    {
        return new WeaponConfigurationCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            UnitId = _unit.Id,
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)HexDirection.Bottom
            }
        };
    }

    [Fact]
    public void Format_ReturnsEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ReturnsEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ReturnsEmpty_WhenUnitNotDeployed()
    {
        // Arrange
        var command = CreateCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ReturnsTorsoRotationMessage_WhenConfigurationIsTorsoRotation()
    {
        // Arrange
        var command = CreateCommand();
        _unit.Deploy(new HexPosition(new HexCoordinates(1,1), HexDirection.Top));
        var expectedHex = _unit.Position!.Coordinates.Neighbor(HexDirection.Bottom);

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponConfiguration_TorsoRotation");
        result.ShouldBe($"{_player1.Name}'s {_unit.Name} rotates torso to face {expectedHex}");
    }

    [Fact]
    public void Format_ReturnsArmsFlipMessage_WhenConfigurationIsArmsFlip()
    {
        // Arrange
        var command = CreateCommand();
        _unit.Deploy(new HexPosition(new HexCoordinates(1,1), HexDirection.Top));
        command.Configuration = new WeaponConfiguration
        {
            Type = WeaponConfigurationType.ArmsFlip,
            Value = 1
        };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponConfiguration_ArmsFlip");
        result.ShouldBe($"{_player1.Name}'s {_unit.Name} flips arms forward");
    }

    [Fact]
    public void Format_ReturnsArmsFlipBackwardMessage_WhenConfigurationIsArmsFlipZero()
    {
        // Arrange
        var command = CreateCommand();
        _unit.Deploy(new HexPosition(new HexCoordinates(1,1), HexDirection.Top));
        command.Configuration = new WeaponConfiguration
        {
            Type = WeaponConfigurationType.ArmsFlip,
            Value = 0
        };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        _localizationService.Received(1).GetString("Command_WeaponConfiguration_ArmsFlip");
        result.ShouldBe($"{_player1.Name}'s {_unit.Name} flips arms backward");
    }
}
