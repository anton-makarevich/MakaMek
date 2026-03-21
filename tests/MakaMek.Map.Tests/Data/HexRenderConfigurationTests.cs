using Sanet.MakaMek.Map.Data;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Data;

public class HexRenderConfigurationTests
{
    [Fact]
    public void Default_ReturnsTrueForShowLabelsAndShowOutline()
    {
        // Act & Assert
        var sut = HexRenderConfiguration.Default;
        sut.ShowLabels.ShouldBeTrue();
        sut.ShowOutline.ShouldBeTrue();
    }
}