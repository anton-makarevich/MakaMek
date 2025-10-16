using AsyncAwaitBestPractices.MVVM;
using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class TargetSelectionViewModelTests
{
    private readonly Unit _target;
    private Action<Unit>? _onSetPrimaryAction;
    private TargetSelectionViewModel _sut = null!;

    public TargetSelectionViewModelTests()
    {
        // Create a real Mech instance instead of mocking Unit (which is abstract)
        var structureValueProvider = new ClassicBattletechRulesProvider();
        var componentProvider = new ClassicBattletechComponentProvider();
        var mechFactory = new MechFactory(
            structureValueProvider,
            componentProvider,
            Substitute.For<ILocalizationService>());

        var unitData = MechFactoryTests.CreateDummyMechData();
        _target = mechFactory.Create(unitData);
    }

    [Fact]
    public void Constructor_SetsTargetProperty()
    {
        // Arrange & Act
        CreateSut(isPrimary: true);

        // Assert
        _sut.Target.ShouldBe(_target);
    }

    [Fact]
    public void Constructor_SetsNameFromTarget()
    {
        // Arrange & Act
        CreateSut(isPrimary: true);

        // Assert
        _sut.Name.ShouldBe("Locust LCT-1V");
    }

    [Fact]
    public void Constructor_WhenPrimary_SetsPrimaryToTrue()
    {
        // Arrange & Act
        CreateSut(isPrimary: true);

        // Assert
        _sut.IsPrimary.ShouldBeTrue();
        _sut.IsSecondary.ShouldBeFalse();
    }
    
    [Fact]
    public void Constructor_WhenHasWeaponsForTarget_SetsHasWeaponsForTargetToTrue()
    {
        // Arrange & Act
        CreateSut(isPrimary: true, hasWeaponsForTarget: true);

        // Assert
        _sut.HasWeaponsForTarget.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenNotPrimary_SetsPrimaryToFalse()
    {
        // Arrange & Act
        CreateSut(isPrimary: false);

        // Assert
        _sut.IsPrimary.ShouldBeFalse();
        _sut.IsSecondary.ShouldBeTrue();
    }

    [Fact]
    public void IsPrimary_WhenSet_UpdatesIsSecondary()
    {
        // Arrange
        CreateSut(isPrimary: true);
        _sut.IsPrimary.ShouldBeTrue();
        _sut.IsSecondary.ShouldBeFalse();

        // Act
        _sut.IsPrimary = false;

        // Assert
        _sut.IsPrimary.ShouldBeFalse();
        _sut.IsSecondary.ShouldBeTrue();
    }

    [Fact]
    public void IsPrimary_WhenSet_RaisesPropertyChanged()
    {
        // Arrange
        CreateSut(isPrimary: true);
        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TargetSelectionViewModel.IsPrimary))
                propertyChangedRaised = true;
        };

        // Act
        _sut.IsPrimary = false;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
    }
    
    [Fact]
    public void HasWeaponsForTarget_WhenSet_RaisesPropertyChanged()
    {
        // Arrange
        CreateSut(isPrimary: true, hasWeaponsForTarget: false);
        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TargetSelectionViewModel.HasWeaponsForTarget))
                propertyChangedRaised = true;
        };

        // Act
        _sut.HasWeaponsForTarget = true;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
        _sut.HasWeaponsForTarget.ShouldBeTrue();
    }

    [Fact]
    public void IsPrimary_WhenSet_RaisesPropertyChangedForIsSecondary()
    {
        // Arrange
        CreateSut(isPrimary: true);
        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TargetSelectionViewModel.IsSecondary))
                propertyChangedRaised = true;
        };

        // Act
        _sut.IsPrimary = false;

        // Assert
        propertyChangedRaised.ShouldBeTrue();
    }

    [Fact]
    public async Task SetAsPrimary_InvokesCallback()
    {
        // Arrange
        CreateSut(isPrimary: false);
        Unit? capturedTarget = null;
        _onSetPrimaryAction = target => capturedTarget = target;
        CreateSut(isPrimary: false); // Recreate with the action set

        // Act
        var command = _sut.SetAsPrimary as AsyncCommand;
        command.ShouldNotBeNull();
        await command.ExecuteAsync();

        // Assert
        capturedTarget.ShouldBe(_target);
    }

    [Fact]
    public void SetAsPrimary_IsNotNull()
    {
        // Arrange & Act
        CreateSut(isPrimary: false);

        // Assert
        _sut.SetAsPrimary.ShouldNotBeNull();
    }

    private void CreateSut(bool isPrimary, bool hasWeaponsForTarget = false)
    {
        _onSetPrimaryAction ??= Substitute.For<Action<Unit>>();
        _sut = new TargetSelectionViewModel(_target, isPrimary, hasWeaponsForTarget, _onSetPrimaryAction);
    }
}

