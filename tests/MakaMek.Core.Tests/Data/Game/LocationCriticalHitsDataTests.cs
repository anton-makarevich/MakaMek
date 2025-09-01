using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class LocationCriticalHitsDataTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly Unit _unit;

    public LocationCriticalHitsDataTests()
    {
        // Create a unit using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var unitData = MechFactoryTests.CreateDummyMechData();
        _unit = mechFactory.Create(unitData);
    }

    [Fact]
    public void Render_BasicCriticalHit_ReturnsCorrectOutput()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 4],
            1,
            [new ComponentHitData
            {
                Slot = 0,
                Type = MakaMekComponent.Engine
            }],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 1");
        result.ShouldContain("Critical hit in slot 1:");
        result.ShouldContain("Critical hits in CT:");
    }

    [Fact]
    public void Render_WithLocationHeader_ShowsLocationHeader()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 4],
            1,
            [new ComponentHitData
            {
                Slot = 0,
                Type = MakaMekComponent.Engine
            }],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical hits in CT:");
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 1");
    }

    [Fact]
    public void Render_BlownOffLocation_ShowsBlownOffMessageAndSkipsOtherDetails()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.LeftArm,
            [6, 6],
            0,
            null,
            true);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 12");
        result.ShouldContain("Critical hit in LA, location blown off");
        result.ShouldNotContain("Number of critical hits:");
        result.ShouldNotContain("Critical hit in LA slot");
    }

    [Fact]
    public void Render_ZeroCriticalHits_StopsAfterShowingNumber()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [2, 2],
            0,
            [],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 4");
        result.ShouldContain("Number of critical hits: 0");
        result.ShouldNotContain("Critical hit in slot");
    }

    [Fact]
    public void Render_WithExplosionDamage_ShowsExplosionDetails()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [5, 5],
            1,
            [new ComponentHitData
            {
                Slot = 0,
                Type = MakaMekComponent.Engine,
                ExplosionDamage = 25
            }],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 10");
        result.ShouldContain("Number of critical hits: 1");
        result.ShouldContain("Critical hit in slot 1:");
        result.ShouldContain("exploded, damage: 25");
    }

    [Fact]
    public void Render_WithNonExistentComponentSlot_SkipsThatComponent()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 4],
            2,
            [
                new ComponentHitData { Slot = 0, Type = MakaMekComponent.Engine }, // Valid slot
                new ComponentHitData { Slot = 99, Type = MakaMekComponent.MediumLaser } // Invalid slot
            ],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 8");
        result.ShouldContain("Number of critical hits: 2");
        // Should only show the valid component hit
        result.ShouldContain("Critical hit in slot 1:");
        // Should not show the invalid component hit
        result.ShouldNotContain("slot 100:");
    }

    [Fact]
    public void Render_WithExplosionDamageDistribution_ShowsDistributionDetails()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [5, 5],
            1,
            [new ComponentHitData
            {
                Slot = 0,
                Type = MakaMekComponent.Engine,
                ExplosionDamage = 25,
                ExplosionDamageDistribution = [
                    new LocationDamageData(PartLocation.CenterTorso, 0, 10, false),
                    new LocationDamageData(PartLocation.LeftTorso, 5, 5, false),
                    new LocationDamageData(PartLocation.RightTorso, 0, 5, false)
                ]
            }],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Critical Roll: 10");
        result.ShouldContain("Number of critical hits: 1");
        result.ShouldContain("Critical hit in slot 1:");
        result.ShouldContain("exploded, damage: 25");
        result.ShouldContain("Explosion damage distribution:");
        result.ShouldContain("Excess damage 10 structure transferred to CT");
        result.ShouldContain("Excess damage 5 armor, 5 structure transferred to LT");
        result.ShouldContain("Excess damage 5 structure transferred to RT");
    }

    [Fact]
    public void Render_WithEmptyExplosionDamageDistribution_DoesNotShowDistribution()
    {
        // Arrange
        var sut = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [5, 5],
            1,
            [new ComponentHitData
            {
                Slot = 0,
                Type = MakaMekComponent.Engine,
                ExplosionDamage = 25,
                ExplosionDamageDistribution = []
            }],
            false);

        // Act
        var result = sut.Render(_localizationService, _unit);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("exploded, damage: 25");
        result.ShouldNotContain("Explosion damage distribution:");
    }
}
