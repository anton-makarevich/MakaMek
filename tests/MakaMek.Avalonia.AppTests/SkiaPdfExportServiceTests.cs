using System.Globalization;
using System.Text.RegularExpressions;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// Drives the concrete <see cref="SkiaPdfExportService"/> end to end (this assembly has real
/// Skia natives). Page dimensions are verified by parsing the uncompressed page dictionary
/// (<c>/MediaBox</c>) out of the produced PDF instead of a PDF library reader, so no
/// additional dependency is needed.
/// </summary>
public class SkiaPdfExportServiceTests
{
    private readonly SkiaPdfExportService _sut = new();

    [Fact]
    public async Task GeneratePdfFromPngAsync_WithValidPng_ReturnsPdfBytes()
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        var result = await _sut.GeneratePdfFromPngAsync(pngBytes, 100, 100);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Take(5).ShouldBe("%PDF-"u8.ToArray());
    }

    [Fact]
    public async Task GeneratePdfFromPngAsync_WithDifferentPageSizes_SetsCorrectDimensions()
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        var result = await _sut.GeneratePdfFromPngAsync(pngBytes, 612, 792);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Take(5).ShouldBe("%PDF-"u8.ToArray());

        var text = System.Text.Encoding.ASCII.GetString(result);
        // "/Type /Page" must not match the "/Type /Pages" tree node, hence the lookahead.
        Regex.Matches(text, @"/Type\s*/Page(?!s)").Count.ShouldBe(1);
        var mediaBox = Regex.Match(text, @"/MediaBox\s*\[\s*([^\]]+)\]");
        mediaBox.Success.ShouldBeTrue();
        var values = mediaBox.Groups[1].Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => double.Parse(v, CultureInfo.InvariantCulture))
            .ToArray();
        values.Length.ShouldBe(4);
        values[2].ShouldBe(612, 0.5);
        values[3].ShouldBe(792, 0.5);
    }

    [Fact]
    public async Task GeneratePdfFromPngAsync_NullPngBytes_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _sut.GeneratePdfFromPngAsync(null!, 100, 100);

        await act.ShouldThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GeneratePdfFromPngAsync_NegativeOrZeroWidth_ThrowsArgumentOutOfRangeException(int widthPoints)
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        Func<Task> act = () => _sut.GeneratePdfFromPngAsync(pngBytes, widthPoints, 100);

        await act.ShouldThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GeneratePdfFromPngAsync_NegativeOrZeroHeight_ThrowsArgumentOutOfRangeException(int heightPoints)
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        Func<Task> act = () => _sut.GeneratePdfFromPngAsync(pngBytes, 100, heightPoints);

        await act.ShouldThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GeneratePdfFromPngAsync_MalformedImageBytes_ThrowsArgumentException()
    {
        var malformedBytes = "not a png"u8.ToArray();

        Func<Task> act = () => _sut.GeneratePdfFromPngAsync(malformedBytes, 100, 100);

        await act.ShouldThrowAsync<ArgumentException>();
    }

    private const string _1x1RedPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
}
