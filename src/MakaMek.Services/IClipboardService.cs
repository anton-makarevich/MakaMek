namespace Sanet.MakaMek.Services;

/// <summary>
/// Service for copying text to the system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the given text to the system clipboard.
    /// </summary>
    Task SetTextAsync(string text);
}
