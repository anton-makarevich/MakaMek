using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class HeadTests
{
    [Fact]
    public void Head_ShouldBeInitializedCorrectly()
    {
        // Arrange & Act
        var head = new Head("Head",  8, 3);

        // Assert
        head.Name.ShouldBe("Head");
        head.Location.ShouldBe(PartLocation.Head);
        head.MaxArmor.ShouldBe(8);
        head.CurrentArmor.ShouldBe(8);
        head.MaxStructure.ShouldBe(3);
        head.CurrentStructure.ShouldBe(3);
        head.TotalSlots.ShouldBe(12);

        // Verify default components
        head.GetComponent<LifeSupport>().ShouldNotBeNull();
        head.GetComponent<Sensors>().ShouldNotBeNull();
        head.GetComponent<Cockpit>().ShouldNotBeNull();
    }
}
