using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Services;

public class AvaloniaAssetImageService : IImageService<Bitmap>
{
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private const string AssetsBasePath = "avares://Sanet.MakaMek.Avalonia/Assets";

    public Task<Bitmap?> GetImage(string assetType, string assetName)
    {
        var path = $"{AssetsBasePath}/{assetType.ToLower()}/{assetName.ToLower()}.png";
        return Task.FromResult(_cache.GetOrAdd(path, LoadImage));
    }

    private static Bitmap? LoadImage(string path)
    {
        try
        {
            var uri = new Uri(path);
            var asset =AssetLoader.Open(uri);
            return new Bitmap(asset);
        }
        catch
        {
            return null;
        }
    }

    Task <object?> IImageService.GetImage(string assetType, string assetName)
    {
        return Task.FromResult<object?>(GetImage(assetType, assetName));
    }
}
