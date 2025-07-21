using Sanet.MakaMek.Avalonia.Converters;
using Shouldly;
using System.Globalization;

namespace MakaMek.Avalonia.Tests.Converters;

public class CollectionToVisibilityConverterTests
{
    private readonly CollectionToVisibilityConverter _sut = new();

    [Fact]
    public void Convert_WithNonEmptyCollection_ReturnsTrue()
    {
        // Arrange
        var collection = new List<string> { "item1", "item2" };

        // Act
        var result = _sut.Convert(collection, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithEmptyCollection_ReturnsFalse()
    {
        // Arrange
        var collection = new List<string>();

        // Act
        var result = _sut.Convert(collection, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsFalse()
    {
        // Arrange
        object? value = null;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithNonCollectionValue_ReturnsFalse()
    {
        // Arrange
        var value = false;

        // Act
        var result = _sut.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void Convert_WithEnumerableWithItems_ReturnsTrue()
    {
        // Arrange
        var enumerable = Enumerable.Range(1, 3);

        // Act
        var result = _sut.Convert(enumerable, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(true);
    }

    [Fact]
    public void Convert_WithEmptyEnumerable_ReturnsFalse()
    {
        // Arrange
        var enumerable = Enumerable.Empty<int>();

        // Act
        var result = _sut.Convert(enumerable, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeOfType<bool>();
        result.ShouldBe(false);
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Arrange
        object? value = null;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => 
            _sut.ConvertBack(value, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
