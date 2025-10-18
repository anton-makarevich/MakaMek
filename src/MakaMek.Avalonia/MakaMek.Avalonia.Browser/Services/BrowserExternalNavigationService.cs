using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Browser.Services;

/// <summary>
/// Browser (WASM) implementation of external navigation service that opens URLs in a new tab.
/// </summary>
public partial class BrowserExternalNavigationService : IExternalNavigationService
{
    public Task OpenUrlAsync(string url)
    {
        try
        {
            OpenUrlInNewTab(url);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to crash the app if URL opening fails
            Console.WriteLine($"Failed to open URL {url}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public Task OpenEmailAsync(string emailAddress, string subject)
    {
        try
        {
            // Create mailto URI with subject
            var mailtoUri = $"mailto:{emailAddress}?subject={Uri.EscapeDataString(subject)}";
            OpenUrlInNewTab(mailtoUri);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to crash the app if email opening fails
            Console.WriteLine($"Failed to open email client for {emailAddress}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    [JSImport("globalThis.window.open")]
    private static partial void OpenUrlInNewTab(string url);
}

