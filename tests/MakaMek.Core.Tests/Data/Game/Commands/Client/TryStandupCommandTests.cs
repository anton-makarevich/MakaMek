using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client;

public class TryStandupCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player = new Player(Guid.NewGuid(), "Player 1");
    private readonly Mech _mech;

    public TryStandupCommandTests()
    {
        _game.Players.Returns([_player]);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _mech = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService).Create(unitData);
        _player.AddUnit(_mech);
        
        // Set the mech as prone for testing
        _mech.SetProne();
    }

    private TryStandupCommand CreateCommand()
    {
        return new TryStandupCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player.Id,
            UnitId = _mech.Id,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = CreateCommand();
        _localizationService.GetString("Command_TryStandup")
            .Returns("{0} attempts to stand up {1}.");

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBe("Player 1 attempts to stand up Locust LCT-1V.");
        _localizationService.Received(1).GetString("Command_TryStandup");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var sut = CreateCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var sut = CreateCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }
}
