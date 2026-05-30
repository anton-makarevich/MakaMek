using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace Sanet.MakaMek.Services;

public class PdfExportService : IPdfExportService
{
    public Task<byte[]> GeneratePdfFromPngAsync(byte[] pngBytes, int widthPoints, int heightPoints)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(widthPoints);
        page.Height = XUnit.FromPoint(heightPoints);

        using var gfx = XGraphics.FromPdfPage(page);
        using var ms = new MemoryStream(pngBytes);
        using var image = XImage.FromStream(() => ms);
        gfx.DrawImage(image, 0, 0, page.Width, page.Height);

        using var pdfStream = new MemoryStream();
        document.Save(pdfStream);
        return Task.FromResult(pdfStream.ToArray());
    }
}
