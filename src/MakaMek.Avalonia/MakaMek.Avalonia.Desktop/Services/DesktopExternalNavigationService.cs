using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Desktop.Services;

/// <summary>
/// Desktop implementation of external navigation service that opens URLs in the default browser.
/// </summary>
public class DesktopExternalNavigationService : IExternalNavigationService
{
    public Task OpenUrlAsync(string url)
    {
        try
        {
            // Use platform-specific approach to open URL
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS
                Process.Start("open", url);
            }
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

            // Use platform-specific approach to open email client
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows
                Process.Start(new ProcessStartInfo
                {
                    FileName = mailtoUri,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux
                Process.Start("xdg-open", mailtoUri);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS
                Process.Start("open", mailtoUri);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to crash the app if email opening fails
            Console.WriteLine($"Failed to open email client for {emailAddress}: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}

