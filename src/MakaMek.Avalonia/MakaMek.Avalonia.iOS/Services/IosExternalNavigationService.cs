using System;
using System.Threading.Tasks;
using Foundation;
using Sanet.MakaMek.Core.Services;
using UIKit;

namespace Sanet.MakaMek.Avalonia.iOS.Services;

/// <summary>
/// iOS implementation of external navigation service that opens URLs in Safari.
/// </summary>
public class IosExternalNavigationService : IExternalNavigationService
{
    public async Task OpenUrlAsync(string url)
    {
        try
        {
            var nsUrl = new NSUrl(url);
            await UIApplication.SharedApplication.OpenUrlAsync(nsUrl, new UIApplicationOpenUrlOptions());
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to crash the app if URL opening fails
            Console.WriteLine($"Failed to open URL {url}: {ex.Message}");
        }
    }
}

