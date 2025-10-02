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
    public void Constructor_ShouldInitializeWithAllUnits_WhenShowAllClassesIsTrue()
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
        sut.FilteredAvailableUnits.ShouldBe(units);
        sut.CanAddUnit.ShouldBeFalse(); // No unit selected initially
    }

    [Theory]
    [InlineData(WeightClass.Light, new[] { "Locust LCT-1V", "Commando COM-2D" })]
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
        sut.FilteredAvailableUnits.ShouldBe(units); // All units should be returned
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
