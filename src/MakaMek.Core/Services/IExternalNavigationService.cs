namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Provides a way to navigate to external URLs across different platforms.
/// </summary>
public interface IExternalNavigationService
{
    /// <summary>
    /// Opens the specified URL in the default browser or external application.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OpenUrlAsync(string url);
}

