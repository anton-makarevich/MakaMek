using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Units.Components;

public class LocationSlotAssignmentTests
{
    [Fact]
    public void GetSlots_ReturnsCorrectSlots()
    {
        // Arrange
        var sut = new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3);
        
        // Act
        var slots = sut.GetSlots();
        
        // Assert
        slots.ShouldBe([0, 1, 2]);
    }
}