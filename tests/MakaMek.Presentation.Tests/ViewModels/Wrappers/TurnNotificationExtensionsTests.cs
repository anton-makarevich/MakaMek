using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class TurnNotificationExtensionsTests
{
    [Theory]
    [InlineData(TurnNotificationKind.Turn, 0)]
    [InlineData(TurnNotificationKind.Phase, 1)]
    [InlineData(TurnNotificationKind.Initiative, 1)]
    [InlineData(TurnNotificationKind.ActivePlayer, 2)]
    public void AnnouncementRank_ForKnownKind_ReturnsExpectedRank(TurnNotificationKind kind, int expectedRank)
    {
        // Act
        var rank = kind.AnnouncementRank();

        // Assert
        rank.ShouldBe(expectedRank);
    }

    [Fact]
    public void AnnouncementRank_ForUnknownKind_ReturnsFallbackRank()
    {
        // Arrange
        const TurnNotificationKind kind = (TurnNotificationKind)int.MaxValue;

        // Act
        var rank = kind.AnnouncementRank();

        // Assert
        rank.ShouldBe(3);
    }

    [Fact]
    public void AnnouncementRank_TurnRanksBeforeEveryOtherKind()
    {
        // Arrange
        var turnRank = TurnNotificationKind.Turn.AnnouncementRank();

        // Act
        int[] otherRanks =
        [
            TurnNotificationKind.Phase.AnnouncementRank(),
            TurnNotificationKind.Initiative.AnnouncementRank(),
            TurnNotificationKind.ActivePlayer.AnnouncementRank()
        ];

        // Assert
        otherRanks.ShouldAllBe(rank => rank > turnRank);
    }

    [Fact]
    public void AnnouncementRank_PhaseAndInitiativeShareRank_SoTheyQueueInArrivalOrder()
    {
        // Arrange & Act
        var phaseRank = TurnNotificationKind.Phase.AnnouncementRank();
        var initiativeRank = TurnNotificationKind.Initiative.AnnouncementRank();

        // Assert
        initiativeRank.ShouldBe(phaseRank);
    }

    [Fact]
    public void AnnouncementRank_RanksPhaseNotificationsBeforeActivePlayer()
    {
        // Arrange & Act
        var activePlayerRank = TurnNotificationKind.ActivePlayer.AnnouncementRank();

        // Assert
        TurnNotificationKind.Phase.AnnouncementRank().ShouldBeLessThan(activePlayerRank);
        TurnNotificationKind.Initiative.AnnouncementRank().ShouldBeLessThan(activePlayerRank);
    }

    [Fact]
    public void AnnouncementRank_SortsTurnNotificationsInReadingOrder()
    {
        // Arrange
        TurnNotificationKind[] announcedInReverseReadingOrder =
        [
            TurnNotificationKind.ActivePlayer,
            TurnNotificationKind.Phase,
            TurnNotificationKind.Initiative,
            TurnNotificationKind.Turn
        ];

        // Act - stable ordering by rank, as Announce() applies it
        var ordered = announcedInReverseReadingOrder
            .Select((kind, arrival) => new { Kind = kind, Arrival = arrival })
            .OrderBy(entry => entry.Kind.AnnouncementRank())
            .ThenBy(entry => entry.Arrival)
            .Select(entry => entry.Kind)
            .ToArray();

        // Assert
        ordered.ShouldBe([
            TurnNotificationKind.Turn,
            TurnNotificationKind.Phase,
            TurnNotificationKind.Initiative,
            TurnNotificationKind.ActivePlayer
        ]);
    }
}
