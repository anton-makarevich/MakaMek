namespace Sanet.MakaMek.Core.Services;

/// <summary>
/// Provides a way to navigate to external URLs and compose emails across different platforms.
/// </summary>
public interface IExternalNavigationService
{
    /// <summary>
    /// Opens the specified URL in the default browser or external application.
    /// </summary>
    /// <param name="url">The URL to open.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OpenUrlAsync(string url);

    /// <summary>
    /// Opens the default email client with a pre-filled email address and subject.
    /// </summary>
    /// <param name="emailAddress">The recipient email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OpenEmailAsync(string emailAddress, string subject);
}

