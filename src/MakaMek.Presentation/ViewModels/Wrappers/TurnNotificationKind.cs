namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// The kind of state change a notification announces. Simultaneous notifications are announced in
/// rank order, so a turn that starts with a new turn, a new phase and a new active player always
/// reads turn, then phase, then whose turn it is.
///
/// Rank is not the member order: a phase and a result produced by that phase share a rank so they
/// queue in arrival order. See <see cref="TurnNotificationExtensions.AnnouncementRank"/>.
/// </summary>
public enum TurnNotificationKind
{
    Turn,
    Phase,
    Initiative,
    ActivePlayer
}

public static class TurnNotificationExtensions
{
    /// <summary>
    /// Where a notification of this kind sits relative to others queued at the same moment.
    ///
    /// A phase and a result of that phase share a rank deliberately. The initiative winner is only
    /// known once the last roll lands, which is after the initiative phase has been announced and
    /// before the movement phase is - and equal ranks queue in arrival order, so sharing a rank is
    /// what puts the result between the two phase banners. Ranking it ahead of phases announced it
    /// before the phase it reports on; ranking it behind them announced it after the next phase.
    /// </summary>
    public static int AnnouncementRank(this TurnNotificationKind kind) => kind switch
    {
        TurnNotificationKind.Turn => 0,
        TurnNotificationKind.Phase or TurnNotificationKind.Initiative => 1,
        TurnNotificationKind.ActivePlayer => 2,
        _ => 3
    };
}
