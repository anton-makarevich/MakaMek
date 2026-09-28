namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// The kind of state change a notification announces. The order of the members is the order
/// simultaneous notifications are announced in, so a turn that starts with an initiative result,
/// a new phase and a new active player always reads turn, who won initiative, then phase, then
/// whose turn it is.
/// </summary>
public enum TurnNotificationKind
{
    Turn,
    Initiative,
    Phase,
    ActivePlayer
}

/// <summary>
/// A transient announcement of a game state change, shown once and then discarded.
/// </summary>
/// <param name="Kind">What changed.</param>
/// <param name="Text">The text to announce, already formatted for display.</param>
/// <param name="Tint">The colour to announce it in, usually the active player's tint.</param>
public sealed record TurnNotification(TurnNotificationKind Kind, string Text, string Tint);
