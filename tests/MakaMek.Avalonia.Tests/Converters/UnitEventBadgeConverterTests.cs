using System.Globalization;
using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class UnitEventBadgeConverterTests
{
    [Fact]
    public void Convert_EmptyCollection_ReturnsEmptyBadge()
        => new UnitEventBadgeConverter().Convert(Array.Empty<object>(), typeof(string), null, CultureInfo.InvariantCulture).ShouldBe(string.Empty);

    [Fact]
    public void Convert_NonEmptyCollection_ReturnsCountBadge()
        => new UnitEventBadgeConverter().Convert(new[] { 1, 2 }, typeof(string), null, CultureInfo.InvariantCulture).ShouldBe("⚠ 2");
}
