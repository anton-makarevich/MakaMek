using JetBrains.Annotations;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Logging;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Logging;

[TestSubject(typeof(ConsoleCommandLogger))]
public class ConsoleCommandLoggerTest
{
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly ConsoleCommandLogger _sut;
    private readonly IGameCommand _command = Substitute.For<IGameCommand>();
    private readonly Guid _gameId = Guid.NewGuid();
    
    public ConsoleCommandLoggerTest()
    {
        _command.Render(_localizationService, _game).Returns("Rendered command");
        _game.Id.Returns(_gameId);
        _sut= new ConsoleCommandLogger(_localizationService, _game);
    }
    
    [Fact]
    public void Log_ShouldWriteToConsole_WhenGameOriginIdMatches()
    {
        // Arrange
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        
        _command.GameOriginId.Returns(_gameId);
        
        try
        {
            // Act
            _sut.Log(_command);
        
            // Assert
            var output = stringWriter.ToString();
            output.ShouldContain("Rendered command");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Log_ShouldNotWriteToConsole_WhenGameOriginIdDoesNotMatch()
    {
        // Arrange
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        
        _command.GameOriginId.Returns(Guid.NewGuid());
    
        try
        {
            // Act
            _sut.Log(_command);
        
            // Assert
            var output = stringWriter.ToString();
            output.ShouldBeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
    
    [Fact]
    public void Log_ShouldNotThrow_WhenRenderThrows()
    {
        // Arrange
        var originalOut = Console.Out;
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        
        _command.Render(_localizationService, _game).Returns(_ => throw new Exception("Render failed"));
        _command.GameOriginId.Returns(_gameId);
        
        try
        {
            // Act
            _sut.Log(_command);
        
            // Assert
            var output = stringWriter.ToString();
            output.ShouldContain("<Render() failed>");
        }
        finally
        {
            // Restore original console output
            Console.SetOut(originalOut);
        }
    }
    
    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => _sut.Dispose());
    }
}