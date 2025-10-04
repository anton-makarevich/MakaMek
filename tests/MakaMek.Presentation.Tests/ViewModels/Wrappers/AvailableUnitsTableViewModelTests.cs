using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using System.Windows.Input;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class AvailableUnitsTableViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithAllUnitsSortedByName_WhenShowAllClassesIsTrue()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();

        // Act
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Assert
        sut.WeightClassFilters.ShouldBe([
            "All",
            "Light",
            "Medium",
            "Heavy",
            "Assault"
        ]);
        sut.SelectedWeightClassFilterString.ShouldBe("All");
        // Units should be sorted by name (Chassis then Model) ascending by default
        sut.FilteredAvailableUnits.Select(u => u.Chassis).ShouldBe([
            "Atlas",
            "Centurion",
            "Commando",
            "Locust",
            "Warhammer"
        ]);
        sut.CanAddUnit.ShouldBeFalse(); // No unit selected initially
    }

    [Theory]
    [InlineData(WeightClass.Light, new[] { "Commando COM-2D", "Locust LCT-1V" })] // Sorted by name
    [InlineData(WeightClass.Medium, new[] { "Centurion CN9-A" })]
    [InlineData(WeightClass.Heavy, new[] { "Warhammer WHM-6R" })]
    [InlineData(WeightClass.Assault, new[] { "Atlas AS7-D" })]
    public void FilteredAvailableUnits_ShouldReturnCorrectUnits_ForWeightClassFilter(
        WeightClass weightClass,
        string[] expectedUnitNames)
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand)
        {
            // Act
            SelectedWeightClassFilterString = weightClass.ToString()
        };

        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        result.Count.ShouldBe(expectedUnitNames.Length);
        result.Select(u => $"{u.Chassis} {u.Model}").ShouldBe(expectedUnitNames);
    }

    [Fact]
    public void SelectedWeightClassFilterString_ShouldSetShowAllClassesToTrue_WhenSetToAll()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand)
        {
            SelectedWeightClassFilterString = nameof(WeightClass.Light)
        };

        // Act
        sut.SelectedWeightClassFilterString = "All";

        // Assert
        sut.SelectedWeightClassFilterString.ShouldBe("All");
        // All units should be returned, sorted by name
        sut.FilteredAvailableUnits.Select(u => u.Chassis).ShouldBe([
            "Atlas",
            "Centurion",
            "Commando",
            "Locust",
            "Warhammer"
        ]);
    }

    [Fact]
    public void SelectedWeightClassFilterString_ShouldNotifyPropertyChanged_WhenChanged()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        var propertyChanged = false;
        var filteredUnitsChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.SelectedWeightClassFilterString))
                propertyChanged = true;
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.FilteredAvailableUnits))
                filteredUnitsChanged = true;
        };

        // Act
        sut.SelectedWeightClassFilterString = "Light";

        // Assert
        propertyChanged.ShouldBeTrue();
        filteredUnitsChanged.ShouldBeTrue();
    }

    [Fact]
    public void SelectedUnit_ShouldUpdateCanAddUnit_WhenSet()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);
        var unit = units.First();
        sut.CanAddUnit.ShouldBeFalse();

        // Act
        sut.SelectedUnit = unit;

        // Assert
        sut.SelectedUnit.ShouldBe(unit);
        sut.CanAddUnit.ShouldBeTrue();
    }

    [Fact]
    public void SelectedUnit_ShouldUpdateCanAddUnitToFalse_WhenSetToNull()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);
        var unit = units.First();
        sut.SelectedUnit = unit; // First set to a unit
        sut.CanAddUnit.ShouldBeTrue();

        // Act
        sut.SelectedUnit = null;

        // Assert
        sut.SelectedUnit.ShouldBeNull();
        sut.CanAddUnit.ShouldBeFalse();
    }

    [Fact]
    public void SelectedUnit_ShouldNotifyPropertyChanged_WhenChanged()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);
        var unit = units.First();

        var selectedUnitChanged = false;
        var canAddUnitChanged = false;
        sut.PropertyChanged += (_, args) => {
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.SelectedUnit))
                selectedUnitChanged = true;
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.CanAddUnit))
                canAddUnitChanged = true;
        };

        // Act
        sut.SelectedUnit = unit;

        // Assert
        selectedUnitChanged.ShouldBeTrue();
        canAddUnitChanged.ShouldBeTrue();
    }

    [Fact]
    public void CanAddUnit_ShouldBeFalse_WhenNoUnitSelected()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act & Assert
        sut.CanAddUnit.ShouldBeFalse();
    }

    [Fact]
    public void FilteredAvailableUnits_ShouldReturnEmpty_WhenNoUnitsMatchFilter()
    {
        // Arrange
        var unit = MechFactoryTests.CreateDummyMechData() with { Mass = 20 }; // Light class unit
        var units = new List<UnitData> { unit };
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand)
        {
            // Act - Set filter to Heavy class, but unit is Light class
            SelectedWeightClassFilterString = nameof(WeightClass.Heavy)
        };

        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void SelectedWeightClassFilterString_ShouldIgnoreInvalidValues()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand)
        {
            // Act
            SelectedWeightClassFilterString = "InvalidClass"
        };

        // Assert
        sut.SelectedWeightClassFilterString.ShouldBe("All"); // Should remain as All
    }

    [Fact]
    public void AddUnitCommand_ShouldBeSetFromConstructor()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();

        // Act
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Assert
        sut.AddUnitCommand.ShouldBe(addCommand);
    }

    [Fact]
    public async Task SortByNameCommand_WhenExecutedOnce_ShouldToggleToDescending()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - First click toggles to descending
        await (sut.SortByNameCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.NameSortIndicator.ShouldBe("↑"); // Up arrow for descending
        sut.TonnageSortIndicator.ShouldBe(string.Empty);

        // Verify units are sorted by name descending
        result.Select(u => $"{u.Chassis} {u.Model}").ShouldBe([
            "Warhammer WHM-6R",
            "Locust LCT-1V",
            "Commando COM-2D",
            "Centurion CN9-A",
            "Atlas AS7-D"
        ]);
    }

    [Fact]
    public async Task SortByNameCommand_WhenExecutedTwice_ShouldToggleBackToAscending()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - First click to descending, second click back to ascending
        await (sut.SortByNameCommand as IAsyncCommand)!.ExecuteAsync();
        await (sut.SortByNameCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.NameSortIndicator.ShouldBe("↓"); // Down arrow for ascending
        sut.TonnageSortIndicator.ShouldBe(string.Empty);

        // Verify units are sorted by name ascending
        result.Select(u => $"{u.Chassis} {u.Model}").ShouldBe([
            "Atlas AS7-D",
            "Centurion CN9-A",
            "Commando COM-2D",
            "Locust LCT-1V",
            "Warhammer WHM-6R"
        ]);
    }

    [Fact]
    public async Task SortByTonnageCommand_WhenExecuted_ShouldSortByTonnageAscending()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.NameSortIndicator.ShouldBe(string.Empty); // No indicator for Name
        sut.TonnageSortIndicator.ShouldBe("↓"); // Down arrow for ascending

        // Verify units are sorted by tonnage ascending (then by name as secondary sort)
        result.Select(u => $"{u.Chassis} {u.Model} ({u.Mass})").ShouldBe([
            "Locust LCT-1V (20)",
            "Commando COM-2D (25)",
            "Centurion CN9-A (50)",
            "Warhammer WHM-6R (70)",
            "Atlas AS7-D (100)"
        ]);
    }

    [Fact]
    public async Task SortByTonnageCommand_WhenExecutedTwice_ShouldToggleToDescending()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - First click to ascending, second click to descending
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.NameSortIndicator.ShouldBe(string.Empty);
        sut.TonnageSortIndicator.ShouldBe("↑"); // Up arrow for descending

        // Verify units are sorted by tonnage descending
        result.Select(u => $"{u.Chassis} {u.Model} ({u.Mass})").ShouldBe([
            "Atlas AS7-D (100)",
            "Warhammer WHM-6R (70)",
            "Centurion CN9-A (50)",
            "Commando COM-2D (25)",
            "Locust LCT-1V (20)"
        ]);
    }

    [Fact]
    public async Task SortByTonnageCommand_ThenSortByName_ShouldSwitchSortColumn()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - Sort by tonnage, then switch to name
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        await (sut.SortByNameCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.NameSortIndicator.ShouldBe("↓"); // Name column now active with ascending
        sut.TonnageSortIndicator.ShouldBe(string.Empty); // Tonnage indicator removed

        // Verify units are sorted by name ascending
        result.Select(u => $"{u.Chassis} {u.Model}").ShouldBe([
            "Atlas AS7-D",
            "Centurion CN9-A",
            "Commando COM-2D",
            "Locust LCT-1V",
            "Warhammer WHM-6R"
        ]);
    }

    [Fact]
    public async Task FilteredAvailableUnits_ShouldMaintainSorting_WhenFilterChanges()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - Sort by tonnage descending, then apply filter
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync(); // Toggle to descending
        sut.SelectedWeightClassFilterString = "Light";
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        sut.TonnageSortIndicator.ShouldBe("↑"); // Still descending

        // Verify filtered units are still sorted by tonnage descending
        result.Select(u => $"{u.Chassis} {u.Model} ({u.Mass})").ShouldBe([
            "Commando COM-2D (25)",
            "Locust LCT-1V (20)"
        ]);
    }

    [Fact]
    public void SortByName_WithUnitsHavingSameChassis_ShouldSortByModel()
    {
        // Arrange
        var units = new List<UnitData>
        {
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Atlas",
                Model = "AS7-K",
                Mass = 100
            },
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Atlas",
                Model = "AS7-D",
                Mass = 100
            },
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Atlas",
                Model = "AS7-S",
                Mass = 100
            }
        };
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - Default is ascending by name
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        result.Select(u => u.Model).ShouldBe(["AS7-D", "AS7-K", "AS7-S"]);
    }

    [Fact]
    public async Task SortByTonnage_WithUnitsHavingSameMass_ShouldSortByNameAsSecondary()
    {
        // Arrange
        var units = new List<UnitData>
        {
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Warhammer",
                Model = "WHM-6R",
                Mass = 70
            },
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Archer",
                Model = "ARC-2R",
                Mass = 70
            },
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Marauder",
                Model = "MAD-3R",
                Mass = 70
            }
        };
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        // Act - Sort by tonnage
        await (sut.SortByTonnageCommand as IAsyncCommand)!.ExecuteAsync();
        var result = sut.FilteredAvailableUnits.ToList();

        // Assert
        // All have same mass, so should be sorted by Chassis
        result.Select(u => u.Chassis).ShouldBe(["Archer", "Marauder", "Warhammer"]);
    }

    [Fact]
    public async Task SortByNameCommand_ShouldNotifyPropertyChanged()
    {
        // Arrange
        var units = CreateTestUnits();
        var addCommand = Substitute.For<ICommand>();
        var sut = new AvailableUnitsTableViewModel(units, addCommand);

        var filteredUnitsChanged = false;
        var nameSortIndicatorChanged = false;
        var tonnageSortIndicatorChanged = false;

        sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.FilteredAvailableUnits))
                filteredUnitsChanged = true;
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.NameSortIndicator))
                nameSortIndicatorChanged = true;
            if (args.PropertyName == nameof(AvailableUnitsTableViewModel.TonnageSortIndicator))
                tonnageSortIndicatorChanged = true;
        };

        // Act
        await (sut.SortByNameCommand as IAsyncCommand)!.ExecuteAsync();

        // Assert
        filteredUnitsChanged.ShouldBeTrue();
        nameSortIndicatorChanged.ShouldBeTrue();
        tonnageSortIndicatorChanged.ShouldBeTrue();
    }
    
    private static List<UnitData> CreateTestUnits()
    {
        return
        [
            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Locust",
                Model = "LCT-1V",
                Mass = 20 // Light
            },

            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Commando",
                Model = "COM-2D",
                Mass = 25 // Light
            },

            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Centurion",
                Model = "CN9-A",
                Mass = 50 // Medium
            },

            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Warhammer",
                Model = "WHM-6R",
                Mass = 70 // Heavy
            },

            MechFactoryTests.CreateDummyMechData() with
            {
                Chassis = "Atlas",
                Model = "AS7-D",
                Mass = 100 // Assault
            }
        ];
    }
}
