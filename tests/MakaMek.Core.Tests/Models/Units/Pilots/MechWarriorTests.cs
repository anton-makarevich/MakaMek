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
}
