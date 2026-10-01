using System.Collections.Generic;
using System.Linq;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Avalonia.Controls;

/// <summary>
/// Decides which squad card the bar should bring into view when its contents overflow.
///
/// The bar is free to scroll wherever the player leaves it. It only takes that over when something
/// needs attention: the unit currently acting, or failing that the first one carrying a
/// notification badge.
/// </summary>
public static class SquadBarReveal
{
    /// <summary>
    /// The unit whose card should be scrolled into view, or null to leave the bar where it is.
    /// </summary>
    /// <param name="units">The squad, in the order the bar lays them out.</param>
    /// <param name="activeUnit">The unit driving the current action, if any.</param>
    public static IUnit? UnitToReveal(IEnumerable<IUnit>? units, IUnit? activeUnit)
    {
        var squad = units as IReadOnlyList<IUnit> ?? units?.ToList();
        if (squad is null || squad.Count == 0) return null;

        // An active unit outranks a badge: it is where the player has to act.
        if (activeUnit is not null && squad.Contains(activeUnit)) return activeUnit;

        return squad.FirstOrDefault(HasNotification);
    }

    private static bool HasNotification(IUnit unit) => unit.Notifications.Count > 0;
}
