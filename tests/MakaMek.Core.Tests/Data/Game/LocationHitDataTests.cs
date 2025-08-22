using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
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
            .Returns("Aimed Shot targeting {0} succeeded, Roll: {1}");
        _localizationService.GetString("Command_WeaponAttackResolution_AimedShotFailed")
            .Returns("Aimed Shot targeting {0} failed, Roll: {1}");
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
    
    private LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }

    [Fact]
    public void Render_BasicHitLocation_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = CreateHitDataForLocation(
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
        
        var sut = CreateHitDataForLocation(
            PartLocation.CenterTorso,
            5,
            [], // No aimed shot roll
            [6]) with { InitialLocation = PartLocation.RightTorso };

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("RightTorso → CenterTorso: 5 damage (Roll: 6)");
    }

    [Fact]
    public void Render_SuccessfulAimedShot_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = CreateHitDataForLocation(
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
        var sut = CreateHitDataForLocation(
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

        var sut = CreateHitDataForLocation(
            PartLocation.RightArm,
            8,
            aimedShotRoll,
            locationRoll
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldContain(shouldSucceed
            ? $"Aimed Shot targeting RightArm succeeded, Roll: {rollTotal}"
            : $"Aimed Shot targeting RightArm failed, Roll: {rollTotal}");
    }
}