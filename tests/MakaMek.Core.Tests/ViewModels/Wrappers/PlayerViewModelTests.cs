using Shouldly;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.ViewModels.Wrappers;

namespace Sanet.MakaMek.Core.Tests.ViewModels.Wrappers;

public class PlayerViewModelTests
{
    [Fact]
    public void AddUnit_ShouldAddUnitToPlayer_IfSelectedUnitIsNotNull()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        playerViewModel.SelectedUnit = unit;
    
        // Act
        playerViewModel.AddUnitCommand.Execute(null);
    
        // Assert
        playerViewModel.Units.First().Chassis.ShouldBe(unit.Chassis);
    }

    [Fact]
    public void AddUnit_ShouldNotAddUnitToPlayer_IfSelectedUnitIsNull()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        playerViewModel.AddUnitCommand.Execute(null);
    
        // Assert
        playerViewModel.Units.Count.ShouldBe(0);
    }
    
    [Fact]
    public void Name_ShouldReturnPlayerName()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        var name = playerViewModel.Name;
    
        // Assert
        name.ShouldBe("Player1");
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnTrue_IfSelectedUnitIsNotNull()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        playerViewModel.SelectedUnit = unit;
    
        // Act
        var canAddUnit = playerViewModel.CanAddUnit;
    
        // Assert
        canAddUnit.ShouldBeTrue();
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnFalse_IfSelectedUnitIsNull()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        var canAddUnit = playerViewModel.CanAddUnit;
    
        // Assert
        canAddUnit.ShouldBeFalse();
    } 
    
    [Fact]
    public void AvailableUnits_ShouldHaveCorrectValue()
    {
        // Arrange
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[unit]);
    
        // Act
        var availableUnits = playerViewModel.AvailableUnits.ToList();
    
        // Assert
        availableUnits.Contains(unit).ShouldBeTrue();
    }
}