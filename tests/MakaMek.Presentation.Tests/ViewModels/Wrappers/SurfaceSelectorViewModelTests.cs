using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class SurfaceSelectorViewModelTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public SurfaceSelectorViewModelTests()
    {
        _localizationService.GetString("Surface_Bridge").Returns("On the bridge");
        _localizationService.GetString("Surface_Ground").Returns("Ground");
        _localizationService.GetString("Surface_Option_WithCost").Returns("{0} — {1} MP");
    }

    [Fact]
    public void Constructor_InitializesOptionsFromAvailableSurfaces()
    {
        var surfaces = new List<HexReachabilityData>
        {
            new(new HexCoordinates(0, 0), HexSurface.Ground, 1),
            new(new HexCoordinates(0, 1), HexSurface.Bridge, 2)
        };

        var sut = new SurfaceSelectorViewModel(surfaces, _ => { }, _localizationService);

        sut.Options.Count.ShouldBe(2);
        sut.Options[0].Surface.ShouldBe(HexSurface.Ground);
        sut.Options[0].Cost.ShouldBe(1);
        sut.Options[1].Surface.ShouldBe(HexSurface.Bridge);
        sut.Options[1].Cost.ShouldBe(2);
    }

    [Fact]
    public void Constructor_SetsOptionsCountToZero_WhenNoSurfacesProvided()
    {
        var surfaces = Array.Empty<HexReachabilityData>();

        var sut = new SurfaceSelectorViewModel(surfaces, _ => { }, _localizationService);

        sut.Options.ShouldBeEmpty();
    }

    [Fact]
    public void SelectSurface_InvokesCallbackWithCorrectSurface()
    {
        var surfaces = new List<HexReachabilityData>
        {
            new(new HexCoordinates(0, 0), HexSurface.Ground, 1)
        };
        HexSurface? capturedSurface = null;
        var sut = new SurfaceSelectorViewModel(surfaces, s => capturedSurface = s, _localizationService);

        sut.SelectSurface(HexSurface.Bridge);

        capturedSurface.ShouldBe(HexSurface.Bridge);
    }

    [Fact]
    public void CancelCommand_WhenExecuted_InvokesCancelCallback()
    {
        var surfaces = Array.Empty<HexReachabilityData>();
        var cancelInvoked = false;
        var sut = new SurfaceSelectorViewModel(surfaces, _ => { }, _localizationService, () => cancelInvoked = true);

        sut.CancelCommand.Execute(null);

        cancelInvoked.ShouldBeTrue();
    }

    [Fact]
    public void CancelCommand_WhenCancelIsNull_DoesNotThrow()
    {
        var surfaces = Array.Empty<HexReachabilityData>();
        var sut = new SurfaceSelectorViewModel(surfaces, _ => { }, _localizationService);

        Should.NotThrow(() => sut.CancelCommand.Execute(null));
    }
}
