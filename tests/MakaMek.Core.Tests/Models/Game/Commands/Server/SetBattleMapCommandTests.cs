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

    private SetBattleMapCommand CreateCommand()
    {
        return new SetBattleMapCommand
        {
            GameOriginId = _gameId,
            MapData = new List<HexData>()
        };
    }

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var command = CreateCommand();
        _localizationService.GetString("Command_SetBattleMap").Returns("Battle map has been set.");

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBe("Battle map has been set.");
        _localizationService.Received(1).GetString("Command_SetBattleMap");
    }
}
