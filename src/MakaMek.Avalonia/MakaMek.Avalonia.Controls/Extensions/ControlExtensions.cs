using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Sanet.MakaMek.Avalonia.Controls.Extensions;

public static class ControlExtensions
{
    extension(Control control)
    {
        public byte[] RenderToPngBytes(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return [];
            var pixelSize = new PixelSize(width, height);
            using var bitmap = new RenderTargetBitmap(pixelSize);
            bitmap.Render(control);
            using var ms = new MemoryStream();
            bitmap.Save(ms);
            return ms.ToArray();
        }
    }
}
