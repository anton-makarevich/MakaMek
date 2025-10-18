using System;
using System.Threading.Tasks;
using Android.Content;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Avalonia.Android.Services;

/// <summary>
/// Android implementation of external navigation service that opens URLs in the default browser.
/// </summary>
public class AndroidExternalNavigationService : IExternalNavigationService
{
    public Task OpenUrlAsync(string url)
    {
        try
        {
            var uri = global::Android.Net.Uri.Parse(url);
            var intent = new Intent(Intent.ActionView, uri);
            intent.AddFlags(ActivityFlags.NewTask);

            global::Android.App.Application.Context.StartActivity(intent);
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
            var uri = global::Android.Net.Uri.Parse(mailtoUri);
            var intent = new Intent(Intent.ActionSendto, uri);
            intent.AddFlags(ActivityFlags.NewTask);

            global::Android.App.Application.Context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - we don't want to crash the app if email opening fails
            Console.WriteLine($"Failed to open email client for {emailAddress}: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}

