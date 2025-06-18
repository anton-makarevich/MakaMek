using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class HitLocationDataTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly Unit _unit;

    public HitLocationDataTests()
    {
        // Initialize localization service with test values
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocation")
            .Returns("{0}: {1} damage (Roll: {2})");
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer")
            .Returns("{0} → {1}: {2} damage (Roll: {3})");
        _localizationService.GetString("Command_WeaponAttackResolution_CriticalHit")
            .Returns("Critical hit in {0} slot {1}: {2}");
        _localizationService.GetString("Command_WeaponAttackResolution_CritRoll")
            .Returns("Critical roll: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_NumCrits")
            .Returns("Criticals: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_BlownOff")
            .Returns("LOCATION BLOWN OFF: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_LocationCriticals")
            .Returns("Critical hits in {0}:");
        _localizationService.GetString("Command_WeaponAttackResolution_Explosion")
            .Returns("{0} EXPLODES! Damage: {1}");
            
        // Create unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _unit = mechFactory.Create(unitData);
    }

    [Fact]
    public void Render_BasicHitLocation_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            [new DiceResult(6)]
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("CenterTorso: 5 damage (Roll: 6)");
    }

    [Fact]
    public void Render_HitLocationWithTransfer_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            [new DiceResult(6)],
            null,
            PartLocation.RightTorso // Initial location before transfer
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("RightTorso → CenterTorso: 5 damage (Roll: 6)");
    }

    [Fact]
    public void Render_HitLocationWithCriticals_ReturnsCorrectOutput()
    {
        // Arrange
        // Add component to center torso for critical hit
        var centerTorso = _unit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var weapon = new MachineGun();
        centerTorso.TryAddComponent(weapon, [2]);

        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(
                PartLocation.CenterTorso,
                8, // Critical roll result
                1, // Number of critical hits
                [
                    WeaponAttackResolutionCommandTests.CreateComponentHitData(2) // Hit in slot 2
                ]
            )
        };

        var sut = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            [new DiceResult(6)],
            criticalHits
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("CenterTorso: 5 damage (Roll: 6)");
        result.ShouldContain("Critical roll: 8");
        result.ShouldContain("Criticals: 1");
        result.ShouldContain("Critical hit in CenterTorso slot 3: "); // Slot is 0-indexed in code but 1-indexed in display
    }

    [Fact]
    public void Render_HitLocationWithBlownOffLocation_ReturnsCorrectOutput()
    {
        // Arrange
        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(
                PartLocation.LeftArm,
                12, // Critical roll result
                0, // No criticals when blown off
                null, // No components
                true // Location blown off
            )
        };

        var sut = new HitLocationData(
            PartLocation.LeftArm,
            5,
            [new DiceResult(6)],
            criticalHits
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LeftArm: 5 damage (Roll: 6)");
        result.ShouldContain("Critical roll: 12");
        result.ShouldContain("LOCATION BLOWN OFF: LeftArm");
        result.ShouldNotContain("Criticals: "); // No criticals when blown off
    }

    [Fact]
    public void Render_HitLocationWithDifferentLocationCriticals_ReturnsCorrectOutput()
    {
        // Arrange
        // Add component to left arm for critical hit
        var leftArm = _unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var weapon = new MachineGun();
        leftArm.TryAddComponent(weapon, [1]);

        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(
                PartLocation.LeftArm, // Different from hit location
                8, // Critical roll
                1, // Number of crits
                [
                    WeaponAttackResolutionCommandTests.CreateComponentHitData(1) // Hit in slot 1
                ]
            )
        };

        var sut = new HitLocationData(
            PartLocation.CenterTorso, // Hit was in center torso
            5,
            [new DiceResult(6)],
            criticalHits
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("CenterTorso: 5 damage (Roll: 6)");
        result.ShouldContain("Critical hits in LeftArm:"); // Shows different location
        result.ShouldContain("Critical roll: 8");
        result.ShouldContain("Criticals: 1");
        result.ShouldContain("Critical hit in LeftArm slot 2: Machine Gun"); // Slot is 0-indexed in code but 1-indexed in display
    }

    [Fact]
    public void Render_HitLocationWithExplosiveComponent_ReturnsCorrectOutput()
    {
        // Arrange
        // Add an explosive component to the center torso
        var part = _unit.Parts.First(p => p.Location == PartLocation.LeftArm);
        var explodingComponent = new Ammo(Ac5.Definition, 10); // 50 damage
        part.TryAddComponent(explodingComponent, [3]);

        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(
                PartLocation.LeftArm,
                8,
                1,
                [
                    WeaponAttackResolutionCommandTests.CreateComponentHitData(3) // Hit in slot 3
                ]
            )
        };

        var sut = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            [new DiceResult(6)],
            criticalHits
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("CenterTorso: 5 damage (Roll: 6)");
        result.ShouldContain("Critical roll: 8");
        result.ShouldContain("Critical hit in LeftArm slot 4: AC5 Ammo"); // Slot is 0-indexed in code but 1-indexed in display
        result.ShouldContain("Ammo EXPLODES! Damage: 50");
    }
}