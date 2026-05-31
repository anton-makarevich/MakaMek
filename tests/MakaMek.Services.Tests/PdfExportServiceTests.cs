using PdfSharp.Pdf.IO;
using Shouldly;

namespace Sanet.MakaMek.Services.Tests;

public class PdfExportServiceTests
{
    private readonly PdfExportService _sut = new();

    [Fact]
    public async Task GeneratePdfFromPngAsync_WithValidPng_ReturnsPdfBytes()
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        var result = await _sut.GeneratePdfFromPngAsync(pngBytes, 100, 100);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Take(5).ShouldBe("%PDF-"u8.ToArray()); // %PDF-
    }

    [Fact]
    public async Task GeneratePdfFromPngAsync_WithDifferentPageSizes_SetsCorrectDimensions()
    {
        var pngBytes = Convert.FromBase64String(_1x1RedPngBase64);

        var result = await _sut.GeneratePdfFromPngAsync(pngBytes, 612, 792);

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.Take(5).ShouldBe("%PDF-"u8.ToArray()); // %PDF-
        using var pdf = PdfReader.Open(new MemoryStream(result), PdfDocumentOpenMode.Import);
        pdf.Pages[0].Width.Point.ShouldBe(612, 0.5);
        pdf.Pages[0].Height.Point.ShouldBe(792, 0.5);
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

    private const string _1x1RedPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
}
