using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Sanet.MakaMek.Services;

public class PdfExportService : IPdfExportService
{
    public async Task<byte[]> GeneratePdfFromPngAsync(byte[] pngBytes, int widthPoints, int heightPoints)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightPoints);
        return await Task.Run(() =>
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(widthPoints);
            page.Height = XUnit.FromPoint(heightPoints);

            using var ms = new MemoryStream(pngBytes);
            using (var image = XImage.FromStream(ms))
            {
                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            }

            using var pdfStream = new MemoryStream();
            document.Save(pdfStream);
            return pdfStream.ToArray();
        });
    }
}
