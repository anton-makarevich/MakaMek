using System.Globalization;
using Avalonia.Data;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class SquadTileActiveConverterTests
{
    private readonly SquadTileActiveConverter _sut = new();

    private object Convert(params object?[] values) =>
        _sut.Convert(values, typeof(bool), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_WhenTheTileUnitIsTheSelectedUnit_ReturnsTrue()
    {
        var unit = new object();

        Convert(unit, unit, null).ShouldBe(true);
    }

    [Fact]
    public void Convert_WhenTheTileUnitIsTheAttacker_ReturnsTrue()
    {
        var unit = new object();
        var target = new object();

        Convert(unit, target, unit).ShouldBe(true);
    }

    [Fact]
    public void Convert_WhenAnotherUnitIsSelected_ReturnsFalse()
    {
        Convert(new object(), new object(), null).ShouldBe(false);
    }

    [Fact]
    public void Convert_WhenNothingIsSelected_ReturnsFalse()
    {
        Convert(new object(), null, null).ShouldBe(false);
    }

    [Fact]
    public void Convert_WhenTheTileHasNoUnit_ReturnsFalse()
    {
        // Null matches null by reference, which would light up an empty tile.
        Convert(null, null, null).ShouldBe(false);
    }

    [Fact]
    public void Convert_WithOnlyTheTileUnit_ReturnsFalse()
    {
        Convert(new object()).ShouldBe(false);
    }

    [Fact]
    public void ConvertBack_DoesNothing()
    {
        _sut.ConvertBack([true], typeof(object), null, CultureInfo.InvariantCulture)
            .ShouldBe(BindingOperations.DoNothing);
    }
}
