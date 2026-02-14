using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
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
        var rulesProvider = new ClassicBattletechRulesProvider();
        _sut = new HeatProjectionViewModel(localizationService, rulesProvider);

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
        _attacker.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData { WeaponName = "Test", HeatPoints = 10 }],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            },
            ExternalHeatSources = []
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
        _sut.ProjectedHeat.ShouldBe( 6); // 3 + 3 = 6 heat
        _sut.HeatProjectionText.ShouldBe($"Heat: 0 → 6");
    }

    [Fact]
    public void HeatDissipation_ReturnsAttackerDissipation()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Act & Assert
        _sut.HeatDissipation.ShouldBe(_attacker.HeatDissipation);
        _sut.HeatDissipationText.ShouldBe($"Dissipation: 4"); // 4 engine heat sinks
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
        var currentHeatChangedCount = 0;
        var dissipationChangedCount = 0;

        _sut.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(HeatProjectionViewModel.CurrentHeat))
                currentHeatChangedCount++;
            if (args.PropertyName == nameof(HeatProjectionViewModel.HeatDissipation))
                dissipationChangedCount++;
        };

        // Act
        _sut.Unit = _attacker;

        // Assert
        currentHeatChangedCount.ShouldBe(1, "CurrentHeat should be notified exactly once");
        dissipationChangedCount.ShouldBe(1, "HeatDissipation should be notified exactly once");
    }

    [Fact]
    public void SettingUnit_ToSameValue_DoesTriggerNotifications()
    {
        // Arrange
        _sut.Unit = _attacker;
        
        var notificationCount = 0;
        _sut.PropertyChanged += (_, args) =>
        {
            notificationCount++;
        };

        // Act - Set to the same value
        _sut.Unit = _attacker;

        // Assert
        notificationCount.ShouldBe(4, "Notifications should be raised even when setting to the same value");
    }

    [Fact]
    public void ProjectedHeat_UsesDeclaredWeaponHeat_WhenAttacksAreDeclared()
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

        // Get the actual slot assignments from the mounted weapons
        var weapon1Slot = weapon1.SlotAssignments[0].FirstSlot;
        var weapon2Slot = weapon2.SlotAssignments[0].FirstSlot;

        // Declare weapon attacks (simulating server-side declaration)
        _attacker.DeclareWeaponAttack([
            new WeaponTargetData
            {
                Weapon = new Core.Data.Units.Components.ComponentData
                {
                    Name = "Medium Laser",
                    Type = Core.Data.Units.Components.MakaMekComponent.MediumLaser,
                    Assignments = [new Core.Data.Units.Components.LocationSlotAssignment(Core.Models.Units.PartLocation.LeftArm, weapon1Slot, 1)]
                },
                TargetId = _target.Id,
                IsPrimaryTarget = true
            },
            new WeaponTargetData
            {
                Weapon = new Core.Data.Units.Components.ComponentData
                {
                    Name = "Medium Laser",
                    Type = Core.Data.Units.Components.MakaMekComponent.MediumLaser,
                    Assignments = [new Core.Data.Units.Components.LocationSlotAssignment(Core.Models.Units.PartLocation.RightArm, weapon2Slot, 1)]
                },
                TargetId = _target.Id,
                IsPrimaryTarget = true
            }
        ]);

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        _sut.ProjectedHeat.ShouldBe(6); // 3 + 3 = 6 heat from declared weapons
        _sut.HeatProjectionText.ShouldBe($"Heat: 0 → 6");
    }

    [Fact]
    public void ProjectedHeat_IncludesEngineHeat_WhenEngineIsDamaged()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Damage the engine (1 hit = 5 heat points)
        var engine = _attacker.GetAllComponents<Core.Models.Units.Components.Engines.Engine>().First();
        engine.Hit();

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        _sut.ProjectedHeat.ShouldBe(5); // 5 heat from engine damage
        _sut.HeatProjectionText.ShouldBe($"Heat: 0 → 5");
    }

    [Fact]
    public void ProjectedHeat_CombinesAllHeatSources_WhenMultipleSourcesPresent()
    {
        // Arrange
        _sut.Unit = _attacker;

        // Add current heat
        _attacker.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData { WeaponName = "Test", HeatPoints = 5 }],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            },
            ExternalHeatSources = []
        });
        _attacker.ResetTurnState();

        // Add weapons
        var weapon = new MediumLaser(); // Heat: 3
        var leftArm = _attacker.Parts[Core.Models.Units.PartLocation.LeftArm];
        leftArm.TryAddComponent(weapon);
        _attacker.WeaponAttackState.SetWeaponTarget(weapon, _target, _attacker);

        // Damage the engine (1 hit = 5 heat points)
        var engine = _attacker.GetAllComponents<Core.Models.Units.Components.Engines.Engine>().First();
        engine.Hit();

        // Act
        _sut.UpdateProjectedHeat();

        // Assert
        // CurrentHeat (5) + SelectedWeaponHeat (3) + EngineHeat (5) = 13
        _sut.ProjectedHeat.ShouldBe(13);
        _sut.HeatProjectionText.ShouldBe($"Heat: 5 → 13");
    }
    
    [Fact]
    public void ProjectedHeat_ShouldBeSameAsCurrentHeat_WhenHeatAlreadyApplied()
    {
        // Arrange
        _sut.Unit = _attacker;
    
        // Add weapons
        var weapon = new MediumLaser(); // Heat: 3
        var leftArm = _attacker.Parts[Core.Models.Units.PartLocation.LeftArm];
        leftArm.TryAddComponent(weapon);
        _attacker.WeaponAttackState.SetWeaponTarget(weapon, _target, _attacker);
    
        // Damage the engine (1 hit = 5 heat points)
        var engine = _attacker.GetAllComponents<Core.Models.Units.Components.Engines.Engine>().First();
        engine.Hit();
        
        // Apply heat
        _attacker.ApplyHeat(new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [new WeaponHeatData { WeaponName = "Test", HeatPoints = 5 }],
            DissipationData = new HeatDissipationData
            {
                HeatSinks = 0,
                EngineHeatSinks = 0,
                DissipationPoints = 0
            },
            ExternalHeatSources = []
        });
        
        // Act
        _sut.UpdateProjectedHeat();
    
        // Assert
        // CurrentHeat (5) only
        _sut.ProjectedHeat.ShouldBe(5);
        _sut.HeatProjectionText.ShouldBe($"Heat: 5");
    }
}

