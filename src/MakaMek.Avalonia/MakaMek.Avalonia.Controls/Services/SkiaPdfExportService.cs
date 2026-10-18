using Sanet.MakaMek.Services;
using SkiaSharp;

namespace Sanet.MakaMek.Avalonia.Controls.Services;

public class SkiaPdfExportService : IPdfExportService
{
    public async Task<byte[]> GeneratePdfFromPngAsync(byte[] pngBytes, int widthPoints, int heightPoints)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightPoints);
        return await Task.Run(() =>
        {
            using var image = SKImage.FromEncodedData(pngBytes);
            if (image == null)
                throw new ArgumentException("Failed to decode PNG image data.", nameof(pngBytes));

            using var pdfStream = new MemoryStream();
            using (var document = SKDocument.CreatePdf(pdfStream))
            {
                if (document == null)
                    throw new NotSupportedException("PDF creation is not supported on this platform.");

                using var canvas = document.BeginPage(widthPoints, heightPoints);
                canvas.DrawImage(image, new SKRect(0, 0, widthPoints, heightPoints));
                document.EndPage();
                document.Close();
            }

            return pdfStream.ToArray();
        });
    }
}
