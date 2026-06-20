using System.Text;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics;

public class FallingDamageDataTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    private static FallingDamageData CreateSutWithHitLocations(bool hasHitLocations = true)
    {
        var hitLocations = hasHitLocations
            ? new List<LocationHitData>
            {
                new LocationHitData(
                    new List<LocationDamageData>
                    {
                        new LocationDamageData(PartLocation.CenterTorso, 4, 1, false)
                    },
                    [],
                    [6],
                    PartLocation.CenterTorso)
            }
            : new List<LocationHitData>();

        var hitLocationsData = new HitLocationsData(hitLocations, 5);
        return new FallingDamageData(
            HexDirection.Top,
            hitLocationsData,
            new DiceResult(1),
            HitDirection.Front);
    }

    [Fact]
    public void Render_ShouldIncludeDamageTotal_WhenHitLocationsExist()
    {
        var sut = CreateSutWithHitLocations();
        var stringBuilder = new StringBuilder();

        sut.Render(stringBuilder, _localizationService);

        var result = stringBuilder.ToString();
        result.ShouldContain("and took 5 damage");
    }

    [Fact]
    public void Render_ShouldIncludeHitLocationsSection_WhenHitLocationsExist()
    {
        var sut = CreateSutWithHitLocations();
        var stringBuilder = new StringBuilder();

        sut.Render(stringBuilder, _localizationService);

        var result = stringBuilder.ToString();
        result.ShouldContain("Hit Locations:");
    }

    [Fact]
    public void Render_ShouldIncludeHitLocationDetails_WhenHitLocationsExist()
    {
        var sut = CreateSutWithHitLocations();
        var stringBuilder = new StringBuilder();

        sut.Render(stringBuilder, _localizationService);

        var result = stringBuilder.ToString();
        result.ShouldContain("CT");
    }

    [Fact]
    public void Render_ShouldNotIncludeHitLocationsSection_WhenNoHitLocations()
    {
        var sut = CreateSutWithHitLocations(false);
        var stringBuilder = new StringBuilder();

        sut.Render(stringBuilder, _localizationService);

        var result = stringBuilder.ToString();
        result.ShouldContain("and took 5 damage");
        result.ShouldNotContain("Hit Locations:");
    }
}
