using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PlayerViewModelTests
{
    [Fact]
    public void AddUnit_ShouldAddUnitToPlayer_IfSelectedUnitIsNotNull()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        sut.SelectedUnit = unit;
    
        // Act
        sut.AddUnitCommand.Execute(null);
    
        // Assert
        sut.Units.First().Chassis.ShouldBe(unit.Chassis);
    }

    [Fact]
    public void AddUnit_ShouldNotAddUnitToPlayer_IfSelectedUnitIsNull()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        sut.AddUnitCommand.Execute(null);
    
        // Assert
        sut.Units.Count.ShouldBe(0);
    }
    
    [Fact]
    public void Name_ShouldReturnPlayerName()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        var name = sut.Name;
    
        // Assert
        name.ShouldBe("Player1");
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnTrue_IfSelectedUnitIsNotNull()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        sut.SelectedUnit = unit;
    
        // Act
        var canAddUnit = sut.CanAddUnit;
    
        // Assert
        canAddUnit.ShouldBeTrue();
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnFalse_IfSelectedUnitIsNull()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
    
        // Act
        var canAddUnit = sut.CanAddUnit;
    
        // Assert
        canAddUnit.ShouldBeFalse();
    } 
    
    [Fact]
    public void AvailableUnits_ShouldHaveCorrectValue()
    {
        // Arrange
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[unit]);
    
        // Act
        var availableUnits = sut.AvailableUnits.ToList();
    
        // Assert
        availableUnits.Contains(unit).ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, true)]  // Local player shows join button
    [InlineData(false, false)] // Remote player does not show join button
    public void ShowJoinButton_ShouldReflectLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.SelectedUnit = unit;
        sut.AddUnitCommand.Execute(null); // Add a unit

        // Act & Assert
        sut.CanJoin.ShouldBe(expected);
    }

    [Fact]
    public void CanJoin_ShouldBeTrue_WhenUnitsAreAdded()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.SelectedUnit = unit;
        sut.AddUnitCommand.Execute(null); // Add a unit

        // Act & Assert
        sut.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public void CanJoin_ShouldBeFalse_WhenNoUnitsAreAdded()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        // No units added

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanJoin_ShouldBeFalse_WhenPlayerIsReady()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.SelectedUnit = unit;
        sut.AddUnitCommand.Execute(null); // Add a unit
        sut.Player.Status = PlayerStatus.Ready;

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanJoin_ShouldBeFalse_WhenAlreadyJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true, []);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.SelectedUnit = unit;
        sut.AddUnitCommand.Execute(null); // Add a unit
        sut.Player.Status = PlayerStatus.Joined;

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnFalse_IfPlayerHasJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        sut.SelectedUnit = unit;
        sut.Player.Status = PlayerStatus.Joined;
    
        // Act
        var canAddUnit = sut.CanAddUnit;
    
        // Assert
        canAddUnit.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSetReady_ShouldBeTrue_WhenPlayerIsJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: [])
        {
            Player =
            {
                Status = PlayerStatus.Joined
            }
        };

        // Act
        var canSetReady = sut.CanSetReady;
    
        // Assert
        canSetReady.ShouldBeTrue();
    }
    
    [Fact]
    public void CanSetReady_ShouldBeFalse_WhenPlayerIsNotJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: [])
        {
            Player =
            {
                Status = PlayerStatus.NotJoined
            }
        };

        // Act
        var canSetReady = sut.CanSetReady;
    
        // Assert
        canSetReady.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSetReady_ShouldBeFalse_WhenPlayerIsAlreadyReady()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: [])
        {
            Player =
            {
                Status = PlayerStatus.Ready
            }
        };

        // Act
        var canSetReady = sut.CanSetReady;
    
        // Assert
        canSetReady.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSetReady_ShouldBeFalse_WhenPlayerIsNotLocal()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: false, 
            availableUnits: [])
        {
            Player =
            {
                Status = PlayerStatus.Joined
            }
        };

        // Act
        var canSetReady = sut.CanSetReady;
    
        // Assert
        canSetReady.ShouldBeFalse();
    }
    
    [Fact]
    public void ExecuteSetReady_ShouldInvokeSetReadyAction_WhenCanSetReadyIsTrue()
    {
        // Arrange
        var setReadyActionCalled = false;
        PlayerViewModel? passedViewModel = null;
        
        Action<PlayerViewModel> setReadyAction = (playerVm) => {
            setReadyActionCalled = true;
            passedViewModel = playerVm;
        };
        
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: [],
            setReadyAction: setReadyAction)
        {
            Player =
            {
                Status = PlayerStatus.Joined
            }
        };

        // Act
        sut.SetReadyCommand.Execute(null);
    
        // Assert
        setReadyActionCalled.ShouldBeTrue();
        passedViewModel.ShouldBe(sut);
    }
    
    [Fact]
    public void ExecuteSetReady_ShouldNotInvokeSetReadyAction_WhenCanSetReadyIsFalse()
    {
        // Arrange
        var setReadyActionCalled = false;
        
        Action<PlayerViewModel> setReadyAction = _ => {
            setReadyActionCalled = true;
        };
        
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: [],
            setReadyAction: setReadyAction)
        {
            Player =
            {
                Status = PlayerStatus.NotJoined // Not joined, so can't set ready
            }
        };

        // Act
        sut.SetReadyCommand.Execute(null);
    
        // Assert
        setReadyActionCalled.ShouldBeFalse();
    }
    
    [Fact]
    public void RefreshStatus_ShouldNotifyCanSetReadyPropertyChanged()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true, 
            availableUnits: []);
        
        var propertyChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(PlayerViewModel.CanSetReady))
                propertyChanged = true;
        };
    
        // Act
        sut.RefreshStatus();
    
        // Assert
        propertyChanged.ShouldBeTrue();
    }
    
    [Fact]
    public void CanSelectUnit_ShouldReturnFalse_IfPlayerHasJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[])
        {
            Player =
            {
                Status = PlayerStatus.Joined
            }
        };

        // Act
        var canSelectUnit = sut.CanSelectUnit;
    
        // Assert
        canSelectUnit.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSelectUnit_ShouldReturnFalse_IfPlayerIsNotLocal()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),false,[]);

        // Act
        var canSelectUnit = sut.CanSelectUnit;
    
        // Assert
        canSelectUnit.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSelectUnit_ShouldReturnTrue_IfPlayerIsLocal()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true,[]);

        // Act
        var canSelectUnit = sut.CanSelectUnit;
    
        // Assert
        canSelectUnit.ShouldBeTrue();
    }
}