using Shouldly;
using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Tests.Data.Units;

public class PilotDataTests
{
    [Fact]
    public void CreateDefaultPilot_ReturnsPilotWithDefaultValues()
    {
        // Arrange
        const string firstName = "Jane";
        const string lastName = "Smith";

        // Act
        var pilot = PilotData.CreateDefaultPilot(firstName, lastName);

        // Assert
        pilot.FirstName.ShouldBe(firstName);
        pilot.LastName.ShouldBe(lastName);
        pilot.Id.ShouldNotBe(Guid.Empty);
        pilot.Gunnery.ShouldBe(4);
        pilot.Piloting.ShouldBe(5);
        pilot.Health.ShouldBe(6);
        pilot.Injuries.ShouldBe(0);
        pilot.IsConscious.ShouldBeTrue();
        pilot.UnconsciousInTurn.ShouldBeNull();
    }

    [Fact]
    public void CreateDefaultPilot_ReturnsNewInstanceEachTime()
    {
        // Arrange
        const string firstName = "Test";
        const string lastName = "Pilot";

        // Act
        var pilot1 = PilotData.CreateDefaultPilot(firstName, lastName);
        var pilot2 = PilotData.CreateDefaultPilot(firstName, lastName);

        // Assert
        pilot1.Id.ShouldNotBe(pilot2.Id);
    }
}
