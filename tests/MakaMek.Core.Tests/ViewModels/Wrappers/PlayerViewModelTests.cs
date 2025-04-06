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

    [Theory]
    [InlineData(true, true)]  // Local player can select units
    [InlineData(false, false)] // Remote player cannot select units
    public void CanSelectUnits_ShouldReflectLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal, []);

        // Act & Assert
        playerViewModel.CanSelectUnits.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true)]  // Local player shows add unit controls
    [InlineData(false, false)] // Remote player does not show add unit controls
    public void ShowAddUnitControls_ShouldReflectLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal, []);

        // Act & Assert
        playerViewModel.ShowAddUnitControls.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, false)] // Local player does not show read-only list
    [InlineData(false, true)]  // Remote player shows read-only list
    public void ShowUnitListReadOnly_ShouldBeInverseOfLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal, []);

        // Act & Assert
        playerViewModel.ShowUnitListReadOnly.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, true)]  // Local player shows join button
    [InlineData(false, false)] // Remote player does not show join button
    public void ShowJoinButton_ShouldReflectLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        playerViewModel.SelectedUnit = unit;
        playerViewModel.AddUnitCommand.Execute(null); // Add a unit

        // Act & Assert
        playerViewModel.CanJoin.ShouldBe(expected);
    }

    [Fact]
    public void CanJoin_ShouldBeTrue_WhenUnitsAreAdded()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        playerViewModel.SelectedUnit = unit;
        playerViewModel.AddUnitCommand.Execute(null); // Add a unit

        // Act & Assert
        playerViewModel.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public void CanJoin_ShouldBeFalse_WhenNoUnitsAreAdded()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        // No units added

        // Act & Assert
        playerViewModel.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanJoin_ShouldBeFalse_WhenAlreadyJoined()
    {
        // Arrange
        var playerViewModel = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        playerViewModel.SelectedUnit = unit;
        playerViewModel.AddUnitCommand.Execute(null); // Add a unit
        playerViewModel.Player.Status = PlayerStatus.Joined;

        // Act & Assert
        playerViewModel.CanJoin.ShouldBeFalse();
    }
}