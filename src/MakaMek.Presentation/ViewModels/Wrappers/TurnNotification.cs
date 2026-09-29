namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// The kind of state change a notification announces. Simultaneous notifications are announced in
/// rank order, so a turn that starts with a new turn, a new phase and a new active player always
/// reads turn, then phase, then whose turn it is.
///
/// Rank is not the member order: a phase and a result produced by that phase share a rank so they
/// queue in arrival order. See BattleMapViewModel's announcement rank.
/// </summary>
public enum TurnNotificationKind
{
    Turn,
    Phase,
    Initiative,
    ActivePlayer
}

/// <summary>
/// A transient announcement of a game state change, shown once and then discarded.
/// </summary>
/// <param name="Kind">What changed.</param>
/// <param name="Text">The text to announce, already formatted for display.</param>
/// <param name="Tint">The colour to announce it in, usually the active player's tint.</param>
public sealed record TurnNotification(TurnNotificationKind Kind, string Text, string Tint);
