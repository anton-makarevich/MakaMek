using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Logging;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Logging.Factories;

public class FileCommandLoggerFactoryTests
{
    private readonly FileCommandLoggerFactory _sut = new();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();

    [Fact]
    public void CreateLogger_ShouldReturnFileCommandLogger()
    {
        // Arrange & Act
        var logger = _sut.CreateLogger(_localizationService, _game);

        // Assert
        logger.ShouldBeOfType<FileCommandLogger>();
    }

    [Fact]
    public void CreateLogger_ShouldReturnNewInstance_EachTime()
    {
        // Act
        var logger1 = _sut.CreateLogger(_localizationService, _game);
        var logger2 = _sut.CreateLogger(_localizationService, _game);

        // Assert
        logger1.ShouldBeOfType<FileCommandLogger>();
        logger2.ShouldBeOfType<FileCommandLogger>();
        logger1.ShouldNotBeSameAs(logger2);
    }
}
