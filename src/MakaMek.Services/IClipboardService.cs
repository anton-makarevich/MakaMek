namespace Sanet.MakaMek.Services;

/// <summary>
/// Service for copying text to the system clipboard.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Copies the given text to the system clipboard.
    /// </summary>
    /// <returns>True if the text was copied to the clipboard, false otherwise.</returns>
    Task<bool> SetText(string text);

    /// <summary>
    /// Reads the current text from the system clipboard.
    /// </summary>
    /// <returns>The clipboard text, or null when no text is available or the read fails.</returns>
    Task<string?> GetText();
}
