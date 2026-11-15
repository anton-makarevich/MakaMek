using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexCoordinatesExtensionsTests
{
    [Fact]
    public void GetDirectionAwayFrom_ReturnsTheNeighbourDirectionPointingAwayFromTheOrigin()
    {
        // The origin is two hexes north of the coordinates, so the direction away is south.
        var coordinates = new HexCoordinates(5, 5);
        var origin = new HexCoordinates(5, 3);

        var direction = coordinates.GetDirectionAwayFrom(origin);

        direction.ShouldBe(HexDirection.Bottom);
    }

    [Fact]
    public void GetDirectionAwayFrom_WhenTheOriginIsBelow_ReturnsTop()
    {
        var coordinates = new HexCoordinates(5, 5);
        var origin = new HexCoordinates(5, 7);

        var direction = coordinates.GetDirectionAwayFrom(origin);

        direction.ShouldBe(HexDirection.Top);
    }

    [Fact]
    public void GetDirectionsByAlignmentAwayFrom_OrdersTheBestAlignmentFirstAndReturnsEveryDirection()
    {
        var coordinates = new HexCoordinates(5, 5);
        var origin = new HexCoordinates(5, 3);

        var directions = coordinates.GetDirectionsByAlignmentAwayFrom(origin);

        directions[0].ShouldBe(HexDirection.Bottom);
        directions.Count.ShouldBe(HexDirectionExtensions.AllDirections.Length);
        directions.ShouldBe(HexDirectionExtensions.AllDirections, ignoreOrder: true);
    }

    [Fact]
    public void GetDirectionsByAlignmentAwayFrom_WhenTwoDirectionsAreEquallyAligned_BreaksTheTieByDirectionOrder()
    {
        // An origin due west leaves the two forward-right directions equally aligned: the lower
        // enum value comes first so the order is deterministic.
        var coordinates = new HexCoordinates(5, 5);
        var origin = new HexCoordinates(3, 5);

        var directions = coordinates.GetDirectionsByAlignmentAwayFrom(origin);

        directions[0].ShouldBe(HexDirection.TopRight);
        directions[1].ShouldBe(HexDirection.BottomRight);
    }

    [Fact]
    public void GetDirectionsByAlignmentAwayFrom_AtTheOrigin_ReturnsAllDirectionsInDeclarationOrder()
    {
        var coordinates = new HexCoordinates(5, 5);

        var directions = coordinates.GetDirectionsByAlignmentAwayFrom(coordinates);

        directions.ShouldBe(HexDirectionExtensions.AllDirections);
    }
}
