using Sanet.MakaMek.Bots.Exceptions;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Exceptions;

public class BotDecisionExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        const string message = "Test error message";
        const string engineType = "TestEngine";
        var playerId = Guid.NewGuid();
        
        // Act
        var exception = new BotDecisionException(message, engineType, playerId);
        
        // Assert
        exception.Message.ShouldBe(message);
        exception.DecisionEngineType.ShouldBe(engineType);
        exception.PlayerId.ShouldBe(playerId);
        exception.InnerException.ShouldBeNull();
    }
    
    [Fact]
    public void Constructor_WithInnerException_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        const string message = "Test error message";
        const string engineType = "TestEngine";
        var playerId = Guid.NewGuid();
        var innerException = new InvalidOperationException("Inner error");
        
        // Act
        var exception = new BotDecisionException(message, engineType, playerId, innerException);
        
        // Assert
        exception.Message.ShouldBe(message);
        exception.DecisionEngineType.ShouldBe(engineType);
        exception.PlayerId.ShouldBe(playerId);
        exception.InnerException.ShouldBe(innerException);
    }
    
    [Fact]
    public void Constructor_ShouldInheritFromException()
    {
        // Arrange
        var exception = new BotDecisionException("Test", "TestEngine", Guid.NewGuid());
        
        // Act & Assert
        exception.ShouldBeAssignableTo<Exception>();
    }
    
    [Fact]
    public void DecisionEngineType_ShouldBeReadOnly()
    {
        // Arrange
        const string engineType = "TestEngine";
        var exception = new BotDecisionException("Test", engineType, Guid.NewGuid());
        
        // Act & Assert
        exception.DecisionEngineType.ShouldBe(engineType);
        // Verify property is read-only by checking it has no setter
        typeof(BotDecisionException)
            .GetProperty(nameof(BotDecisionException.DecisionEngineType))!
            .CanWrite.ShouldBeFalse();
    }
    
    [Fact]
    public void PlayerId_ShouldBeReadOnly()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var exception = new BotDecisionException("Test", "TestEngine", playerId);
        
        // Act & Assert
        exception.PlayerId.ShouldBe(playerId);
        // Verify property is read-only by checking it has no setter
        typeof(BotDecisionException)
            .GetProperty(nameof(BotDecisionException.PlayerId))!
            .CanWrite.ShouldBeFalse();
    }
}
