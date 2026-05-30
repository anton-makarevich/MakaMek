using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Sanet.MakaMek.Services.Avalonia;

public static class ControlExtensions
{
    public static byte[] RenderToPngBytes(this Control control, int width, int height)
    {
        var pixelSize = new PixelSize(width, height);
        using var bitmap = new RenderTargetBitmap(pixelSize);
        bitmap.Render(control);
        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return ms.ToArray();
    }
}
