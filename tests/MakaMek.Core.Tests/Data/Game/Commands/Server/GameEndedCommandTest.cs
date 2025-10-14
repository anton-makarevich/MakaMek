using JetBrains.Annotations;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

[TestSubject(typeof(GameEndedCommand))]
public class GameEndedCommandTest
{

    [Theory]
    [InlineData(GameEndReason.Unknown)]
    [InlineData(GameEndReason.Victory)]
    [InlineData(GameEndReason.PlayersLeft)]
    public void Render_ShouldFormatCorrectly(GameEndReason reason)
    {
        // Arrange
        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString($"Command_GameEnded_{reason}").Returns($"formatted game ended command {reason}");

        // Act
        var result = command.Render(localizationService, Substitute.For<IGame>());

        // Assert
        result.ShouldBe($"formatted game ended command {reason}");
    }
}