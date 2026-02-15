using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Sanet.MakaMek.Services.Avalonia;

public class AvaloniaAssetImageService : IImageService<Bitmap>
{
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private readonly string _assetsBasePath;

    public AvaloniaAssetImageService(string assetsBasePath)
    {
        _assetsBasePath = assetsBasePath;
    }

    public Task<Bitmap?> GetImage(string assetType, string assetName)
    {
        var path = $"{_assetsBasePath}/{assetType.ToLower()}/{assetName.ToLower()}.png";
        return Task.FromResult(_cache.GetOrAdd(path, LoadImage));
    }

    private static Bitmap? LoadImage(string path)
    {
        try
        {
            var uri = new Uri(path);
            var asset = AssetLoader.Open(uri);
            return new Bitmap(asset);
        }
        catch
        {
            return null;
        }
    }

    async Task<object?> IImageService.GetImage(string assetType, string assetName)
    {
        return await GetImage(assetType, assetName);
    }
}
