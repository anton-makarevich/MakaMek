using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PlayerViewModelTests
{
    [Fact]
    public void AddUnit_ShouldAddUnitToPlayer()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
    
        // Act
        sut.AddUnit(unit);
    
        // Assert
        sut.Units.First().Chassis.ShouldBe(unit.Chassis);
    }
    
    [Fact]
    public void Name_ShouldReturnPlayerName()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true);
    
        // Act
        var name = sut.Name;
    
        // Assert
        name.ShouldBe("Player1");
    }

    [Theory]
    [InlineData(true, true)]  // Local player shows join button
    [InlineData(false, false)] // Remote player does not show join button
    public void ShowJoinButton_ShouldReflectLocalPlayerStatus(bool isLocal, bool expected)
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), isLocal);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit); // Add a unit

        // Act & Assert
        sut.CanJoin.ShouldBe(expected);
    }

    [Fact]
    public void CanJoin_ShouldBeTrue_WhenUnitsAreAdded()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit); // Add a unit

        // Act & Assert
        sut.CanJoin.ShouldBeTrue();
    }

    [Fact]
    public void CanJoin_ShouldBeFalse_WhenNoUnitsAreAdded()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true);
        // No units added

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanJoin_ShouldBeFalse_WhenPlayerIsReady()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit); // Add a unit
        sut.Player.Status = PlayerStatus.Ready;

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanJoin_ShouldBeFalse_WhenAlreadyJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit); // Add a unit
        sut.Player.Status = PlayerStatus.Joined;

        // Act & Assert
        sut.CanJoin.ShouldBeFalse();
    }
    
    [Fact]
    public void CanAddUnit_ShouldReturnFalse_IfPlayerHasJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true);
        var unit = MechFactoryTests.CreateDummyMechData(); // Create a new unit
        sut.AddUnit(unit);
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
            isLocalPlayer: true)
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
            isLocalPlayer: true)
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
            isLocalPlayer: true)
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
            isLocalPlayer: false)
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

        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true,
            setReadyAction: SetReadyAction)
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
        return;

        void SetReadyAction(PlayerViewModel playerVm)
        {
            setReadyActionCalled = true;
            passedViewModel = playerVm;
        }
    }
    
    [Fact]
    public void ExecuteSetReady_ShouldNotInvokeSetReadyAction_WhenCanSetReadyIsFalse()
    {
        // Arrange
        var setReadyActionCalled = false;

        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true,
            setReadyAction: SetReadyAction)
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
        return;

        void SetReadyAction(PlayerViewModel _)
        {
            setReadyActionCalled = true;
        }
    }
    
    [Fact]
    public void RefreshStatus_ShouldNotifyCanSetReadyPropertyChanged()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true);
        
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
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true)
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
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),false);

        // Act
        var canSelectUnit = sut.CanSelectUnit;
    
        // Assert
        canSelectUnit.ShouldBeFalse();
    }
    
    [Fact]
    public void CanSelectUnit_ShouldReturnTrue_IfPlayerIsLocal()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"),true);

        // Act
        var canSelectUnit = sut.CanSelectUnit;
    
        // Assert
        canSelectUnit.ShouldBeTrue();
    }
    
        [Fact]
    public void AddUnits_ShouldAddUnitsWithPilotAssignments_WhenProvided()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit1 = MechFactoryTests.CreateDummyMechData();
        var unit2 = MechFactoryTests.CreateDummyMechData();
        unit1.Id = Guid.NewGuid();
        unit2.Id = Guid.NewGuid();
        
        var pilot1 = new PilotData { Id = Guid.NewGuid(), FirstName = "Test", LastName = "Pilot1" };
        var pilot2 = new PilotData { Id = Guid.NewGuid(), FirstName = "Test", LastName = "Pilot2" };
        
        List<PilotAssignmentData> pilotAssignments = 
        [
            new() { UnitId = unit1.Id.Value, PilotData = pilot1 },
            new() { UnitId = unit2.Id.Value, PilotData = pilot2 }
        ];

        // Act
        sut.AddUnits([unit1, unit2], pilotAssignments);

        // Assert
        sut.Units.Count.ShouldBe(2);
        sut.GetPilotDataForUnit(unit1.Id.Value).ShouldBe(pilot1);
        sut.GetPilotDataForUnit(unit2.Id.Value).ShouldBe(pilot2);
    }
    
    [Fact]
    public void AddUnits_ShouldCreateDefaultPilot_WhenNoAssignmentProvided()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        unit.Id = Guid.NewGuid();

        // Act
        sut.AddUnits([unit], []);

        // Assert
        var pilot = sut.GetPilotDataForUnit(unit.Id.Value);
        pilot.ShouldNotBeNull();
        pilot.Value.FirstName.ShouldBe("MechWarrior");
        pilot.Value.Gunnery.ShouldBe(4); // Default gunnery value
        pilot.Value.Piloting.ShouldBe(5); // Default piloting value
    }
    
    [Fact]
    public void GetPilotDataForUnit_ShouldReturnNull_ForUnknownUnit()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unknownUnitId = Guid.NewGuid();

        // Act
        var result = sut.GetPilotDataForUnit(unknownUnitId);

        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void UpdatePilotForUnit_ShouldUpdatePilotData()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        unit.Id = Guid.NewGuid();
        sut.AddUnits([unit], []);
        
        var updatedPilot = new PilotData 
        { 
            Id = Guid.NewGuid(),
            FirstName = "Updated",
            LastName = "Pilot",
            Gunnery = 3,
            Piloting = 4
        };

        // Act
        sut.UpdatePilotForUnit(unit.Id.Value, updatedPilot);
        var result = sut.GetPilotDataForUnit(unit.Id.Value);

        // Assert
        result.ShouldBe(updatedPilot);
    }
    
    [Fact]
    public void AddUnit_ShouldCreateDefaultPilotForUnit()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
    
        // Act
        sut.AddUnit(unit);
    
        // Assert
        var addedUnit = sut.Units.First();
        var pilot = sut.GetPilotDataForUnit(addedUnit.Id!.Value);
        pilot.ShouldNotBeNull();
        pilot.Value.FirstName.ShouldBe("MechWarrior");
    }
    
    [Theory]
    [InlineData(true, PlayerStatus.NotJoined, true)]
    [InlineData(true, PlayerStatus.Joined, false)]
    [InlineData(true, PlayerStatus.Ready, false)]
    [InlineData(false, PlayerStatus.NotJoined, false)]
    public void CanEditName_ShouldReturnCorrectValue_ForDifferentStates(
        bool isLocalPlayer, 
        PlayerStatus status, 
        bool expected)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1")
        {
            Status = status
        };
        var sut = new PlayerViewModel(player, isLocalPlayer);
    
        // Act
        var canEdit = sut.CanEditName;
    
        // Assert
        canEdit.ShouldBe(expected);
    }

    [Fact]
    public void CanEditName_ShouldBeFalse_WhenEditingName()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        sut.StartEditingName();
        
        // Act
        var canEdit = sut.CanEditName;
        
        // Assert
        canEdit.ShouldBeFalse();
    }
    
    [Fact]
    public void StartEditingName_ShouldSetIsEditingToTrue_WhenCanEditIsTrue()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        
        // Act
        sut.StartEditingName();
        
        // Assert
        sut.IsEditingName.ShouldBeTrue();
        sut.EditableName.ShouldBe("Player1");
    }
    
    [Fact]
    public void StartEditingName_ShouldNotSetIsEditing_WhenCanEditIsFalse()
    {
        // Arrange - Player is not local
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), false);
        
        // Act
        sut.StartEditingName();
        
        // Assert
        sut.IsEditingName.ShouldBeFalse();
    }
    
    [Fact]
    public void SaveName_ShouldUpdatePlayerName_WhenNameIsValid()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var sut = new PlayerViewModel(player, true);
        sut.StartEditingName();
        sut.EditableName = "New Player Name";
        
        // Act
        sut.SaveName();
        
        // Assert
        sut.Name.ShouldBe("New Player Name");
        player.Name.ShouldBe("New Player Name");
        sut.IsEditingName.ShouldBeFalse();
    }
    
    [Fact]
    public void SaveName_ShouldNotUpdateName_WhenNameIsEmpty()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Original Name");
        var sut = new PlayerViewModel(player, true);
        sut.StartEditingName();
        sut.EditableName = "  "; // Whitespace name
        
        // Act
        sut.SaveName();
        
        // Assert
        player.Name.ShouldBe("Original Name");
        sut.Name.ShouldBe("Original Name");
        sut.IsEditingName.ShouldBeFalse();
    }
    
    [Fact]
    public void SaveName_ShouldInvokeOnPlayerNameChanged_WhenNameIsUpdated()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1");
        var nameChangedCalled = false;
        Player? playerPassed = null;

        var sut = new PlayerViewModel(player, true, onPlayerNameChanged: OnNameChanged);
        sut.StartEditingName();
        sut.EditableName = "New Name";
        
        // Act
        sut.SaveName();
        
        // Assert
        nameChangedCalled.ShouldBeTrue();
        playerPassed.ShouldBe(player);
        playerPassed?.Name.ShouldBe("New Name");
        return;

        Task OnNameChanged(Player p)
        {
            nameChangedCalled = true;
            playerPassed = p;
            return Task.CompletedTask;
        }
    }
    
    [Fact]
    public void CancelEditName_ShouldDiscardChangesAndStopEditing()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Original Name"), true);
        sut.StartEditingName();
        sut.EditableName = "Modified Name";
        
        // Act
        sut.CancelEditName();
        
        // Assert
        sut.IsEditingName.ShouldBeFalse();
        sut.Name.ShouldBe("Original Name");
        sut.EditableName.ShouldBe("Original Name");
    }
    
    [Fact]
    public void RefreshStatus_ShouldNotifyCanEditNamePropertyChanged()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true);
        
        var propertyChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(PlayerViewModel.CanEditName))
                propertyChanged = true;
        };
    
        // Act
        sut.RefreshStatus();
    
        // Assert
        propertyChanged.ShouldBeTrue();
    }
    
    [Fact]
    public void ExecuteShowUnits_ShouldInvokeShowAvailableUnitsAction_WhenCanAddUnitIsTrue()
    {
        // Arrange
        var showAvailableUnitsCalled = false;
        PlayerViewModel? passedViewModel = null;

        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true,
            showAvailableUnits: ShowAvailableUnitsAction);

        // Act
        sut.ShowAvailableUnitsCommand.Execute(null);
    
        // Assert
        showAvailableUnitsCalled.ShouldBeTrue();
        passedViewModel.ShouldBe(sut);
        return;

        void ShowAvailableUnitsAction(PlayerViewModel playerVm)
        {
            showAvailableUnitsCalled = true;
            passedViewModel = playerVm;
        }
    }
    
    [Fact]
    public void ExecuteShowUnits_ShouldNotInvokeShowAvailableUnitsAction_WhenCanAddUnitIsFalse()
    {
        // Arrange
        var showAvailableUnitsCalled = false;

        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true,
            showAvailableUnits: _ => showAvailableUnitsCalled = true)
        {
            Player =
            {
                // Set status to something that makes CanAddUnit false
                Status = PlayerStatus.Joined
            }
        };

        // Act
        sut.ShowAvailableUnitsCommand.Execute(null);

        // Assert
        showAvailableUnitsCalled.ShouldBeFalse();
    }

    [Fact]
    public void RemoveUnit_ShouldRemoveUnitFromPlayer_WhenCanRemoveUnitIsTrue()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit);
        var initialCount = sut.Units.Count;

        // Act
        sut.RemoveUnitCommand.Execute(sut.Units.First().Id);

        // Assert
        sut.Units.Count.ShouldBe(initialCount - 1);
    }

    [Fact]
    public void RemoveUnit_ShouldNotRemoveUnit_WhenPlayerHasJoined()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit);
        sut.Player.Status = PlayerStatus.Joined;
        sut.RefreshStatus();
        var initialCount = sut.Units.Count;

        // Act
        sut.RemoveUnitCommand.Execute(sut.Units.First().Id);

        // Assert
        sut.Units.Count.ShouldBe(initialCount); // Should not change
    }

    [Fact]
    public void RemoveUnit_ShouldRemovePilotData_WhenUnitIsRemoved()
    {
        // Arrange
        var sut = new PlayerViewModel(new Player(Guid.NewGuid(), "Player1"), true);
        var unit = MechFactoryTests.CreateDummyMechData();
        sut.AddUnit(unit);
        var unitId = sut.Units.First().Id!.Value;

        // Verify pilot data exists
        sut.GetPilotDataForUnit(unitId).ShouldNotBeNull();

        // Act
        sut.RemoveUnitCommand.Execute(sut.Units.First().Id);

        // Assert
        sut.GetPilotDataForUnit(unitId).ShouldBeNull();
    }

    [Theory]
    [InlineData(true, PlayerStatus.NotJoined, true)]
    [InlineData(true, PlayerStatus.Joined, false)]
    [InlineData(true, PlayerStatus.Ready, false)]
    [InlineData(false, PlayerStatus.NotJoined, false)]
    public void CanRemoveUnit_ShouldReturnCorrectValue_ForDifferentStates(
        bool isLocalPlayer,
        PlayerStatus status,
        bool expected)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1")
        {
            Status = status
        };
        var sut = new PlayerViewModel(player, isLocalPlayer);

        // Act
        var canRemove = sut.CanRemoveUnit;

        // Assert
        canRemove.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, PlayerStatus.NotJoined, false)] // Default player, not joined
    [InlineData(true, PlayerStatus.Joined, false)]    // Default player, joined
    [InlineData(false, PlayerStatus.NotJoined, true)]   // Non-default player, not joined
    [InlineData(false, PlayerStatus.Joined, false)]     // Non-default player, joined
    public void IsRemovable_ShouldReturnCorrectValue_ForDifferentStates(
        bool isDefaultPlayer,
        PlayerStatus status,
        bool expected)
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Player1")
        {
            Status = status
        };
        var sut = new PlayerViewModel(player, true, isDefaultPlayer: isDefaultPlayer);

        // Act
        var isRemovable = sut.IsRemovable;

        // Assert
        isRemovable.ShouldBe(expected);
    }

    [Fact]
    public void RefreshStatus_ShouldNotifyCanRemoveUnitPropertyChanged()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true);

        var propertyChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(PlayerViewModel.CanRemoveUnit))
                propertyChanged = true;
        };

        // Act
        sut.RefreshStatus();

        // Assert
        propertyChanged.ShouldBeTrue();
    }

    [Fact]
    public void RefreshStatus_ShouldNotifyIsRemovablePropertyChanged()
    {
        // Arrange
        var sut = new PlayerViewModel(
            new Player(Guid.NewGuid(), "Player1"),
            isLocalPlayer: true);

        var propertyChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(PlayerViewModel.IsRemovable))
                propertyChanged = true;
        };

        // Act
        sut.RefreshStatus();

        // Assert
        propertyChanged.ShouldBeTrue();
    }
}