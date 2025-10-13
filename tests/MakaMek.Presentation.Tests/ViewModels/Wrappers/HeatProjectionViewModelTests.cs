using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class HeatProjectionViewModelTests
{
    private readonly HeatProjectionViewModel _sut;
    private readonly Mech _attacker;
    private readonly Mech _target;

    public HeatProjectionViewModelTests()
    {
        var localizationService = new FakeLocalizationService();
        _sut = new HeatProjectionViewModel(localizationService);

        var mechFactory = new MechFactory(
            new ClassicBattletechRulesProvider(),
            new ClassicBattletechComponentProvider(),
            localizationService);
        
        var data = MechFactoryTests.CreateDummyMechData();
        _attacker = mechFactory.Create(data);
        _target = mechFactory.Create(data);
        
        // Deploy units
        _attacker.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        _target.Deploy(new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom));
    }

    [Fact]
    public void CurrentHeat_ReturnsZero_WhenNoAttacker()
    {
        // Act & Assert
        _sut.CurrentHeat.ShouldBe(0);
    }

    [Fact]
    public void CurrentHeat_ReturnsAttackerHeat_WhenAttackerSet()
    {
        // Arrange
        _attacker.ApplyHeat(new Core.Data.Game.HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new Core.Data.Game.WeaponHeatData { WeaponName = "Test", HeatPoints = 10 }],
            DissipationData = new Core.Data.Game.HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            }
        });

        // Act
        _sut.Unit = _attacker;

        // Assert
        _sut.CurrentHeat.ShouldBe(10);
    }

    [Fact]
    public void ProjectedHeat_EqualsCurrentHeat_WhenNoWeaponsSelected()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        _sut.ProjectedHeat.ShouldBe(_sut.CurrentHeat);
    }

    [Fact]
    public void ProjectedHeat_IncludesSelectedWeaponsHeat()
    {
        // Arrange
        _sut.Unit = _attacker;
        
        // Add weapons to attacker
        var weapon1 = new MediumLaser(); // Heat: 3
        var weapon2 = new MediumLaser(); // Heat: 3
        var leftArm = _attacker.Parts[Core.Models.Units.PartLocation.LeftArm];
        var rightArm = _attacker.Parts[Core.Models.Units.PartLocation.RightArm];
        leftArm.TryAddComponent(weapon1);
        rightArm.TryAddComponent(weapon2);

        // Select weapons
        _attacker.WeaponAttackState.SetWeaponTarget(weapon1, _target, _attacker);
        _attacker.WeaponAttackState.SetWeaponTarget(weapon2, _target, _attacker);

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        _sut.ProjectedHeat.ShouldBe(_sut.CurrentHeat + 6); // 3 + 3 = 6 heat
    }

    [Fact]
    public void HeatDissipation_ReturnsAttackerDissipation()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Act & Assert
        _sut.HeatDissipation.ShouldBe(_attacker.HeatDissipation);
    }

    [Fact]
    public void UpdateProjectedHeat_NotifiesPropertyChanged_WhenValueChanges()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Add a weapon and select it to change the projected heat
        var weapon = new MediumLaser();
        var leftArm = _attacker.Parts[Core.Models.Units.PartLocation.LeftArm];
        leftArm.TryAddComponent(weapon);
        _attacker.WeaponAttackState.SetWeaponTarget(weapon, _target, _attacker);

        var propertyChangedRaised = false;
        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HeatProjectionViewModel.ProjectedHeat))
                propertyChangedRaised = true;
        };

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        propertyChangedRaised.ShouldBeTrue();
    }

    [Fact]
    public void SettingAttacker_UpdatesAllProperties()
    {
        // Arrange
        var currentHeatChanged = false;
        var dissipationChanged = false;

        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HeatProjectionViewModel.CurrentHeat))
                currentHeatChanged = true;
            if (args.PropertyName == nameof(HeatProjectionViewModel.HeatDissipation))
                dissipationChanged = true;
        };

        // Act
        _sut.Unit = _attacker;

        // Assert
        currentHeatChanged.ShouldBeTrue();
        dissipationChanged.ShouldBeTrue();
    }
}

