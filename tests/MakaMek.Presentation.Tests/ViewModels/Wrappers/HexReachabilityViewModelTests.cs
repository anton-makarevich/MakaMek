using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class HexReachabilityViewModelTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public HexReachabilityViewModelTests()
    {
        _localizationService.GetString("Surface_Bridge").Returns("On the bridge");
        _localizationService.GetString("Surface_Ground").Returns("Ground");
        _localizationService.GetString("Surface_Option_WithCost").Returns("{0} — {1} MP");
    }

    [Fact]
    public void Surface_ReturnsSurfaceFromData()
    {
        var data = new HexReachabilityData(new HexCoordinates(1, 2), HexSurface.Ground, 3);

        var sut = new HexReachabilityViewModel(data, _localizationService);

        sut.Surface.ShouldBe(HexSurface.Ground);
    }

    [Fact]
    public void Cost_ReturnsCostFromData()
    {
        var data = new HexReachabilityData(new HexCoordinates(1, 2), HexSurface.Bridge, 5);

        var sut = new HexReachabilityViewModel(data, _localizationService);

        sut.Cost.ShouldBe(5);
    }

    [Fact]
    public void FormattedLabel_ForGroundSurface_FormatsCorrectly()
    {
        var data = new HexReachabilityData(new HexCoordinates(0, 0), HexSurface.Ground, 1);

        var sut = new HexReachabilityViewModel(data, _localizationService);

        sut.FormattedLabel.ShouldBe("Ground — 1 MP");
    }

    [Fact]
    public void FormattedLabel_ForBridgeSurface_FormatsCorrectly()
    {
        var data = new HexReachabilityData(new HexCoordinates(0, 0), HexSurface.Bridge, 2);

        var sut = new HexReachabilityViewModel(data, _localizationService);

        sut.FormattedLabel.ShouldBe("On the bridge — 2 MP");
    }

    [Fact]
    public void FormattedLabel_ForUnknownSurface_UsesEnumName()
    {
        var data = new HexReachabilityData(new HexCoordinates(0, 0), (HexSurface)99, 4);

        var sut = new HexReachabilityViewModel(data, _localizationService);

        sut.FormattedLabel.ShouldBe("99 — 4 MP");
    }
}
