using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class LocationHitDataTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly Unit _unit;

    public LocationHitDataTests()
    {
        // Initialize localization service with test values
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocation")
            .Returns("{0}: {1} damage (Roll: {2})");
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocationTransfer")
            .Returns("{0} → {1}: {2} damage (Roll: {3})");
        _localizationService.GetString("Command_WeaponAttackResolution_AimedShotSuccessful")
            .Returns("{0}: {1} damage (Aimed Shot: {2}, successful)");
        _localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed")
            .Returns("{0}: {1} damage (Aimed Shot: {2}, failed, Roll: {3})");
        _localizationService.GetString("Command_WeaponAttackResolution_AimedShotTransferSuccessful")
            .Returns("{0} → {1}: {2} damage (Aimed Shot: {3}, successful)");
        _localizationService.GetString("Command_WeaponAttackResolution_AimedShotTransferFailed")
            .Returns("{0} → {1}: {2} damage (Aimed Shot: {3}, failed, Roll: {4})");
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
        var sut = new LocationHitData(
            PartLocation.CenterTorso,
            5,
            [], // No aimed shot roll
            [6] // Location roll
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
        var sut = new LocationHitData(
            PartLocation.CenterTorso,
            5,
            [], // No aimed shot roll
            [6], // Location roll
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

        var sut = new LocationHitData(
            PartLocation.CenterTorso,
            5,
            [], // No aimed shot roll
            [6], // Location roll
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

        var sut = new LocationHitData(
            PartLocation.LeftArm,
            5,
            [], // No aimed shot roll
            [6], // Location roll
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

        var sut = new LocationHitData(
            PartLocation.CenterTorso, // Hit was in center torso
            5,
            [], // No aimed shot roll
            [6], // Location roll
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

        var sut = new LocationHitData(
            PartLocation.CenterTorso,
            5,
            [], // No aimed shot roll
            [6], // Location roll
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

    [Fact]
    public void Render_SuccessfulAimedShot_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = new LocationHitData(
            PartLocation.Head,
            5,
            [3, 4], // Aimed shot roll: 7 (successful)
            [] // No location roll since aimed shot was successful
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("Head: 5 damage (Aimed Shot: 7, successful)");
    }

    [Fact]
    public void Render_FailedAimedShot_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = new LocationHitData(
            PartLocation.CenterTorso,
            5,
            [2, 3], // Aimed shot roll: 5 (failed)
            [4, 3] // Location roll: 7 (used for normal hit location)
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("CenterTorso: 5 damage (Aimed Shot: 5, failed, Roll: 7)");
    }



    [Theory]
    [InlineData(6, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(5, false)]
    [InlineData(9, false)]
    [InlineData(2, false)]
    [InlineData(12, false)]
    public void Render_AimedShotRollBoundaryConditions_ReturnsCorrectOutput(int rollTotal, bool shouldSucceed)
    {
        // Arrange
        var aimedShotRoll = rollTotal == 6 ? [3, 3] :
                           rollTotal == 7 ? [3, 4] :
                           rollTotal == 8 ? [4, 4] :
                           rollTotal == 5 ? [2, 3] :
                           rollTotal == 9 ? [4, 5] :
                           rollTotal != 2 ? new[] { 6, 6 } :
                           new[] { 1, 1 }; // 12

        int[] locationRoll = shouldSucceed ? [] : [3, 4]; // Only present if aimed shot failed

        var sut = new LocationHitData(
            PartLocation.RightArm,
            8,
            aimedShotRoll,
            locationRoll
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe(shouldSucceed
            ? $"RightArm: 8 damage (Aimed Shot: {rollTotal}, successful)"
            : $"RightArm: 8 damage (Aimed Shot: {rollTotal}, failed, Roll: 7)");
    }

    [Fact]
    public void Render_AimedShotWithCriticals_ReturnsCorrectOutput()
    {
        // Arrange
        // Add component to right arm for critical hit
        var rightArm = _unit.Parts.First(p => p.Location == PartLocation.RightArm);
        var weapon = new MachineGun();
        rightArm.TryAddComponent(weapon, [1]);

        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(
                PartLocation.RightArm,
                8, // Critical roll result
                1, // Number of critical hits
                [
                    WeaponAttackResolutionCommandTests.CreateComponentHitData(2) // Hit in slot 2
                ]
            )
        };

        var sut = new LocationHitData(
            PartLocation.RightArm,
            5,
            [4, 4], // Aimed shot roll: 8 (successful)
            [], // No location roll since aimed shot was successful
            criticalHits
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldContain("RightArm: 5 damage (Aimed Shot: 8, successful)");
        result.ShouldContain("Critical roll: 8");
        result.ShouldContain("Criticals: 1");
        result.ShouldContain("Critical hit in RightArm slot 3: Medium Laser"); // Slot is 0-indexed in code but 1-indexed in display
    }
}