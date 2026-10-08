using NSubstitute;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Controls;

public class SquadBarRevealTests
{
    private static IUnit Unit(int notifications = 0)
    {
        var unit = Substitute.For<IUnit>();
        unit.Notifications.Returns(Enumerable.Range(0, notifications)
            .Select(_ => new UiEvent(UiEventType.ArmorDamage))
            .ToList());
        return unit;
    }

    [Fact]
    public void UnitToReveal_PrefersTheActiveUnit()
    {
        var active = Unit();
        var squad = new[] { Unit(notifications: 3), active };

        SquadBarReveal.UnitToReveal(squad, active).ShouldBe(active,
            "the player has to act there, so it outranks a badge elsewhere");
    }

    [Fact]
    public void UnitToReveal_FallsBackToTheFirstNotifiedUnit()
    {
        var notified = Unit(notifications: 1);
        var squad = new[] { Unit(), notified, Unit(notifications: 2) };

        SquadBarReveal.UnitToReveal(squad, null).ShouldBe(notified);
    }

    [Fact]
    public void UnitToReveal_LeavesTheBarAlone_WhenNothingNeedsAttention()
    {
        SquadBarReveal.UnitToReveal(new[] { Unit(), Unit() }, null).ShouldBeNull();
    }

    [Fact]
    public void UnitToReveal_IgnoresAnActiveUnitOutsideTheSquad()
    {
        // The attacker can be someone else's unit; the bar only shows the local player's.
        var notified = Unit(notifications: 1);

        SquadBarReveal.UnitToReveal(new[] { Unit(), notified }, Unit()).ShouldBe(notified);
    }

    [Fact]
    public void UnitToReveal_HandlesAnEmptyOrMissingSquad()
    {
        SquadBarReveal.UnitToReveal(null, Unit()).ShouldBeNull();
        SquadBarReveal.UnitToReveal([], Unit()).ShouldBeNull();
    }
}
