using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Pilots;

public class MechWarriorConsciousnessTests
{
    [Fact]
    public void CurrentConsciousnessNumber_WithNoInjuries_Returns2()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        
        // Act & Assert
        sut.CurrentConsciousnessNumber.ShouldBe(1);
    }
    
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(3, 7)]
    [InlineData(4, 10)]
    [InlineData(5, 11)]
    [InlineData(6, 12)]
    [InlineData(7, 12)]
    public void CurrentConsciousnessNumber_WithInjuries_ReturnsCorrectValue(int injuries, int expectedConsciousnessNumber)
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        
        // Act
        sut.Hit(injuries);
        
        // Assert
        sut.CurrentConsciousnessNumber.ShouldBe(expectedConsciousnessNumber);
    }
    
    [Fact]
    public void Hit_EnqueuesConsciousnessNumbers()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        
        // Act
        sut.Hit(2);
        
        // Assert
        sut.PendingConsciousnessNumbers.Count.ShouldBe(2);
        sut.PendingConsciousnessNumbers.Dequeue().ShouldBe(3); // First injury
        sut.PendingConsciousnessNumbers.Dequeue().ShouldBe(5); // Second injury
    }
    
    [Fact]
    public void KnockUnconscious_SetsUnconsciousState()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, 4, []);
        unit.AssignPilot(sut);
        
        // Act
        sut.KnockUnconscious(5);
        
        // Assert
        sut.IsConscious.ShouldBeFalse();
        sut.UnconsciousInTurn.ShouldBe(5);
        sut.AssignedTo?.Events.ShouldContain(e => e.Type == UiEventType.PilotUnconscious);
    }
    
    [Fact]
    public void KnockUnconscious_WhenDead_DoesNothing()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        sut.Kill();
        
        // Act
        sut.KnockUnconscious(5);
        
        // Assert
        sut.IsConscious.ShouldBeTrue(); // Should remain true since dead
        sut.UnconsciousInTurn.ShouldBeNull();
    }
    
    [Fact]
    public void RecoverConsciousness_RestoresConsciousState()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, 4, []);
        unit.AssignPilot(sut);
        sut.KnockUnconscious(3);
        
        // Act
        sut.RecoverConsciousness();
        
        // Assert
        sut.IsConscious.ShouldBeTrue();
        sut.UnconsciousInTurn.ShouldBeNull();
        sut.AssignedTo?.Events.ShouldContain(e => e.Type == UiEventType.PilotRecovered);
    }
    
    [Fact]
    public void RecoverConsciousness_WhenDead_DoesNothing()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        sut.KnockUnconscious(3);
        sut.Kill();
        
        // Act
        sut.RecoverConsciousness();
        
        // Assert
        sut.IsConscious.ShouldBeFalse(); // Should remain false since dead
        sut.UnconsciousInTurn.ShouldBe(3);
    }
    
    [Fact]
    public void ToData_IncludesConsciousnessState()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        sut.Hit(2);
        sut.KnockUnconscious(5);
        
        // Act
        var data = sut.ToData();
        
        // Assert
        data.IsConscious.ShouldBeFalse();
        data.UnconsciousInTurn.ShouldBe(5);
    }
    
    [Fact]
    public void Constructor_FromPilotData_RestoresConsciousnessState()
    {
        // Arrange
        var pilotData = new Sanet.MakaMek.Core.Data.Units.PilotData
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            Gunnery = 4,
            Piloting = 5,
            Health = 6,
            Injuries = 2,
            IsConscious = false,
            UnconsciousInTurn = 3,
        };
        
        // Act
        var sut = new MechWarrior(pilotData);
        
        // Assert
        sut.IsConscious.ShouldBeFalse();
        sut.UnconsciousInTurn.ShouldBe(3);
    }
}
