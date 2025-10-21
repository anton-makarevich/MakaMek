using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class ErrorCommandTest
{
    [Theory]
    [InlineData(ErrorCode.DuplicateCommand)]
    [InlineData(ErrorCode.ValidationFailed)]
    [InlineData(ErrorCode.InvalidGameState)]
    public void Render_ShouldFormatCorrectly(ErrorCode errorCode)
    {
        // Arrange
        var sut = new ErrorCommand
        {
            GameOriginId = Guid.NewGuid(),
            ErrorCode = errorCode,
            Timestamp = DateTime.UtcNow,
            IdempotencyKey = null
        };
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString($"Command_Error_{errorCode}").Returns($"formatted error command {errorCode}");
        
        // Act
        var result = sut.Render(localizationService, Substitute.For<IGame>());

        // Assert
        result.ShouldBe($"formatted error command {errorCode}");
    }
}