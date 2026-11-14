using System.Text.RegularExpressions;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Reads a room code from the clipboard and tracks generation numbers so a read that raced with a
/// user action is discarded. Kept out of the join view model so it does not depend on the clipboard
/// service or the room-code format.
/// </summary>
public sealed class RoomCodeClipboardReader
{
    private const string RoomCodeFormat = "^[A-HJ-NP-Z2-9]{6}$";

    private readonly IClipboardService _clipboardService;
    private int _generation;

    public RoomCodeClipboardReader(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService;
    }

    /// <summary>
    /// The current read generation; a read may only be applied when its captured generation still
    /// matches.
    /// </summary>
    public int Generation => _generation;

    /// <summary>
    /// Invalidates any in-flight read so its result is ignored.
    /// </summary>
    public void InvalidatePendingRead() => _generation++;

    /// <summary>
    /// Reads and validates the clipboard text, returning the normalized room code or <c>null</c> when
    /// the clipboard does not hold one. Callers compare <see cref="Generation"/> before and after the
    /// read to discard a result that raced with a user action.
    /// </summary>
    public async Task<string?> ReadAsync()
    {
        var clipboardText = await _clipboardService.GetText();
        if (string.IsNullOrEmpty(clipboardText)) return null;

        var candidate = clipboardText.Trim().ToUpperInvariant();
        return Regex.IsMatch(candidate, RoomCodeFormat) ? candidate : null;
    }
}
