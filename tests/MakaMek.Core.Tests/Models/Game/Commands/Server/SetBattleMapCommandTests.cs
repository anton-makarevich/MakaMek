using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Data.Map;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Server;

public class SetBattleMapCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly DateTime _timestamp = DateTime.UtcNow;

    private SetBattleMapCommand CreateCommand()
    {
        return new SetBattleMapCommand
        {
            GameOriginId = _gameId,
            Timestamp = _timestamp,
            MapData = []
        };
    }

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = CreateCommand();
        _localizationService.GetString("Command_SetBattleMap").Returns("Battle map has been set.");

        // Act
        var result = sut.Format(_localizationService, _game);

        // Assert
        result.ShouldBe("Battle map has been set.");
        _localizationService.Received(1).GetString("Command_SetBattleMap");
    }

    [Fact]
    public void Init_SetsTimestamp()
    {
        var sut = CreateCommand();
        
        sut.Timestamp.ShouldBe(_timestamp);
    }
}
