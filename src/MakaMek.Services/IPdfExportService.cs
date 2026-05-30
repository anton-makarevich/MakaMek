namespace Sanet.MakaMek.Services;

public interface IPdfExportService
{
    Task<byte[]> GeneratePdfFromPngAsync(byte[] pngBytes, int widthPoints, int heightPoints);
}
