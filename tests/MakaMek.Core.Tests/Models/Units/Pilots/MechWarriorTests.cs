using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Pilots;

public class MechWarriorTests
{
    [Fact]
    public void Constructor_WithDefaultValues_SetsDefaultSkills()
    {
        // Arrange & Act
        var sut = new MechWarrior("John", "Doe");

        // Assert
        sut.FirstName.ShouldBe("John");
        sut.LastName.ShouldBe("Doe");
        sut.Gunnery.ShouldBe(MechWarrior.DefaultGunnery);
        sut.Piloting.ShouldBe(MechWarrior.DefaultPiloting);
        sut.Health.ShouldBe(MechWarrior.DefaultHealth);
        sut.Injuries.ShouldBe(0);
        sut.IsConscious.ShouldBe(true);
    }
    
    [Fact]
    public void Name_ShouldReturnFullName_WhenCreatedWithoutCallSign()
    {
        // Arrange & Act
        var sut = new MechWarrior("John", "Doe");

        // Assert
        sut.Name.ShouldBe("John Doe");
    }
    
    [Fact]
    public void Name_ShouldReturnFullNameWithCallSign_WithCallSign()
    {
        // Arrange & Act
        var sut = new MechWarrior("John", "Doe", "JD");

        // Assert
        sut.Name.ShouldBe("John \"JD\" Doe");
    }

    [Fact]
    public void Constructor_WithCustomSkills_SetsProvidedValues()
    {
        // Arrange & Act
        var sut = new MechWarrior("John", "Doe", gunnery: 3, piloting: 4);

        // Assert
        sut.FirstName.ShouldBe("John");
        sut.LastName.ShouldBe("Doe");
        sut.Gunnery.ShouldBe(3);
        sut.Piloting.ShouldBe(4);
        sut.Health.ShouldBe(MechWarrior.DefaultHealth);
    }
    
    [Fact]
    public void Hit_IncrementsInjuries()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var initialInjuries = sut.Injuries;

        // Act
        sut.Hit();

        // Assert
        sut.Injuries.ShouldBe(initialInjuries + 1);
        sut.PendingConsciousnessNumbers.Count.ShouldBe(1);
    }
    
    [Fact]
    public void Hit_IncrementsInjuriesByProvidedAmount()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var initialInjuries = sut.Injuries;

        // Act
        sut.Hit(2);

        // Assert
        sut.Injuries.ShouldBe(initialInjuries + 2);
        sut.PendingConsciousnessNumbers.Count.ShouldBe(2);
    }
    
    [Fact]
    public void Hit_MultipleTimesIncrementsInjuriesCorrectly()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");

        // Act
        sut.Hit();
        sut.Hit();
        sut.Hit();

        // Assert
        sut.Injuries.ShouldBe(3);
        sut.PendingConsciousnessNumbers.Count.ShouldBe(3);
    }
    
    [Fact]
    public void Hit_AddsEvent()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);
        unit.AssignPilot(sut);
        var initialEventsCount = sut.AssignedTo?.Events.Count ?? 0;
        
        // Act
        sut.Hit();
        
        // Assert
        sut.AssignedTo?.Events.Count.ShouldBe(initialEventsCount + 1);
    }
    
    [Fact]
    public void IsDead_IsFalse_ByDefault()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        
        // Assert
        sut.IsDead.ShouldBeFalse();
    }

    [Fact]
    public void IsDead_WhenInjuriesLessThanHealth_ReturnsFalse()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");

        // Act
        sut.Hit();

        // Assert
        sut.IsDead.ShouldBeFalse();
    }
    
        
    [Fact]
    public void IsDead_WhenInjuriesEqualToHealth_ReturnsTrue()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");

        // Act
        do
        {
            sut.Hit();
        } while (sut.Injuries < sut.Health);

        // Assert
        sut.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void IsDead_WhenInjuriesGreaterThanHealth_ReturnsTrue()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");

        // Act
        do
        {
            sut.Hit();
        } while (sut.Injuries < sut.Health);
        sut.Hit();

        // Assert
        sut.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void Kill_SetsInjuriesToHealth()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");

        // Act
        sut.Kill();

        // Assert
        sut.Injuries.ShouldBe(sut.Health);
        sut.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void Kill_AddsEvent()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);
        unit.AssignPilot(sut);
        var initialEventsCount = sut.AssignedTo?.Events.Count ?? 0;
        
        // Act
        sut.Kill();
        
        // Assert
        sut.AssignedTo?.Events.Count.ShouldBe(initialEventsCount + 1);
    }

    [Fact]
    public void AssignedTo_InitiallyNull()
    {
        // Arrange & Act
        var sut = new MechWarrior("John", "Doe");

        // Assert
        sut.AssignedTo.ShouldBeNull();
    }

    [Fact]
    public void SetAssignedUnit_WithUnit_SetsAssignedTo()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);

        // Act
        sut.AssignedTo = unit;

        // Assert
        sut.AssignedTo.ShouldBe(unit);
    }

    [Fact]
    public void SetAssignedUnit_WithNull_ClearsAssignedTo()
    {
        // Arrange
        var sut = new MechWarrior("John", "Doe");
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);
        sut.AssignedTo = unit;

        // Act
        sut.AssignedTo =null;

        // Assert
        sut.AssignedTo.ShouldBeNull();
    }
    
    [Fact]
    public void CurrentConsciousnessNumber_WithNoInjuries_Returns1()
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
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);
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
        var unit = new UnitTests.TestUnit("Test", "Unit", 50, []);
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
