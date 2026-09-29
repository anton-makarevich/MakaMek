namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// A transient announcement of a game state change, shown once and then discarded.
/// </summary>
/// <param name="Kind">What changed.</param>
/// <param name="Text">The text to announce, already formatted for display.</param>
/// <param name="Tint">The colour to announce it in, usually the active player's tint.</param>
public sealed record TurnNotification(TurnNotificationKind Kind, string Text, string Tint);
