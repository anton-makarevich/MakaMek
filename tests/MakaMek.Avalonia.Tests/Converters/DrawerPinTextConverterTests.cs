using System.Globalization;
using NSubstitute;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class DrawerPinTextConverterTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly DrawerPinTextConverter _sut;

    public DrawerPinTextConverterTests()
    {
        _localizationService.GetString("UnitHud_PinDrawer").Returns("Pin drawer");
        _localizationService.GetString("UnitHud_UnpinDrawer").Returns("Unpin drawer");
        _sut = new DrawerPinTextConverter(_localizationService);
    }

    [Fact]
    public void Convert_OffersToUnpin_WhenPinned()
    {
        _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture).ShouldBe("Unpin drawer");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Convert_OffersToPin_WhenNotPinned(object? value)
    {
        _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture).ShouldBe("Pin drawer");
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
