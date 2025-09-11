using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class LocationHitDataTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly Unit _unit;

    public LocationHitDataTests()
    {
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
        result.Trim().ShouldBe("CT (Roll: 6): 4 armor, 1 structure damage");
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
        result.Trim().ShouldBe("RT (Roll: 6) → CT: 4 armor, 1 structure damage");
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
        result.Trim().ShouldContain("Aimed Shot targeting H succeeded, Roll: 7");
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
        result.Trim().ShouldContain("Aimed Shot targeting CT failed, Roll: 5");
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
            ? $"Aimed Shot targeting RA succeeded, Roll: {rollTotal}"
            : $"Aimed Shot targeting RA failed, Roll: {rollTotal}");
    }

    [Fact]
    public void Render_ShouldReturnEmptyString_WhenNoDamageExists()
    {
        // Arrange 
        var sut = new LocationHitData(
            [], // Empty damage list
            [], 
            [], 
            PartLocation.RightArm
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldShowExcessDamage_WhenMultipleDamageLocations()
    {
        // Arrange - This tests lines 77-85 (excess damage handling)
        var sut = new LocationHitData(
            [
                new LocationDamageData(PartLocation.LeftArm, 5, 2, false), // Primary damage
                new LocationDamageData(PartLocation.LeftTorso, 3, 0, false), // Excess damage location 1
                new LocationDamageData(PartLocation.CenterTorso, 2, 1, false) // Excess damage location 2
            ],
            [], // No aimed shot
            [4, 4], // Location roll
            PartLocation.LeftArm
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LA (Roll: 8): 5 armor, 2 structure damage");
        result.ShouldContain("Excess damage 3 armor transferred to LT");
        result.ShouldContain("Excess damage 2 armor, 1 structure transferred to CT");
    }

    [Fact]
    public void Render_StructureOnlyDamage_ReturnsCorrectOutput()
    {
        // Arrange - Create a hit with only structure damage (no armor damage)
        var sut = new LocationHitData(
        [
            new LocationDamageData(PartLocation.LeftTorso, 0, 3, false) // 0 armor, 3 structure damage
        ],
        [], // No aimed shot
        [5], // Location roll
        PartLocation.LeftTorso
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("LT (Roll: 5): 3 structure damage");
    }
    
    [Fact]
    public void Render_ArmorOnlyDamage_ReturnsCorrectOutput()
    {
        // Arrange - Create a hit with only armor damage (no structure damage)
        var sut = new LocationHitData(
            [
                new LocationDamageData(PartLocation.LeftTorso, 3, 0, false) // 3 armor, 0 structure damage
            ],
            [], // No aimed shot
            [5], // Location roll
            PartLocation.LeftTorso
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("LT (Roll: 5): 3 armor damage");
    }

    [Fact]
    public void Render_StructureOnlyDamageWithTransfer_ReturnsCorrectOutput()
    {
        // Arrange - Create a hit with only structure damage that transfers
        var sut = new LocationHitData(
        [
            new LocationDamageData(PartLocation.LeftTorso, 0, 3, false) // 0 armor, 3 structure damage
        ],
        [], // No aimed shot
        [5], // Location roll
        PartLocation.RightTorso // Different from damage location to trigger transfer
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.Trim().ShouldBe("RT (Roll: 5) → LT: 3 structure damage");
    }

    [Fact]
    public void Render_ExcessDamageWithStructureOnly_ReturnsCorrectOutput()
    {
        // Arrange - Multiple damage locations with structure-only damage
        var sut = new LocationHitData(
        [
            new LocationDamageData(PartLocation.LeftArm, 0, 5, false), // Structure damage to LA
            new LocationDamageData(PartLocation.LeftTorso, 0, 3, false), // Excess damage to LT
            new LocationDamageData(PartLocation.CenterTorso, 0, 2, false) // More excess to CT
        ],
        [], // No aimed shot
        [4, 4], // Location roll
        PartLocation.LeftArm
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LA (Roll: 8): 5 structure damage");
        result.ShouldContain("Excess damage 3 structure transferred to LT");
        result.ShouldContain("Excess damage 2 structure transferred to CT");
    }

    [Fact]
    public void Render_ShouldNotShowExcessDamage_WhenOnlyOneDamageLocation()
    {
        // Arrange - This verifies lines 76-85 are not executed when Damage.Count <= 1
        var sut = new LocationHitData(
            [
                new LocationDamageData(PartLocation.LeftArm, 5, 2, false) // Only one damage location
            ],
            [], // No aimed shot
            [4, 4], // Location roll
            PartLocation.LeftArm
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LA (Roll: 8): 5 armor, 2 structure damage");
        result.ShouldNotContain("Excess damage");
        result.ShouldNotContain("transferred");
    }

    [Fact]
    public void Render_ShouldHandleComplexScenario_WithAimedShotAndMultipleTransfers()
    {
        // Arrange - This tests all code paths: aimed shot, location transfer, and excess damage
        var sut = new LocationHitData(
            [
                new LocationDamageData(PartLocation.LeftTorso, 6, 2, false), // Transferred damage
                new LocationDamageData(PartLocation.CenterTorso, 3, 1, false), // First excess
                new LocationDamageData(PartLocation.RightTorso, 2, 0, false) // Second excess
            ],
            [2, 2], // Failed aimed shot
            [6, 6], // Location roll
            PartLocation.LeftArm // Initial target
        );

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Aimed Shot targeting LA failed, Roll: 4");
        result.ShouldContain("LA (Roll: 12) → LT: 6 armor, 2 structure damage");
        result.ShouldContain("Excess damage 3 armor, 1 structure transferred to CT");
        result.ShouldContain("Excess damage 2 armor transferred to RT");
    }
}