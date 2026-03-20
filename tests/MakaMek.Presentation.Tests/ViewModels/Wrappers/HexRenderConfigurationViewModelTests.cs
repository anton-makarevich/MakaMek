using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class HexRenderConfigurationViewModelTests
{
    [Fact]
    public void ShowLabels_DefaultsToTrue()
    {
        // Arrange & Act
        var sut = new HexRenderConfigurationViewModel();

        // Assert
        sut.ShowLabels.ShouldBeTrue();
    }

    [Fact]
    public void ShowOutline_DefaultsToTrue()
    {
        // Arrange & Act
        var sut = new HexRenderConfigurationViewModel();

        // Assert
        sut.ShowOutline.ShouldBeTrue();
    }

    [Fact]
    public void ToConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var sut = new HexRenderConfigurationViewModel
        {
            ShowLabels = false,
            ShowOutline = true
        };

        // Act
        var config = sut.ToConfiguration();

        // Assert
        config.ShowLabels.ShouldBeFalse();
        config.ShowOutline.ShouldBeTrue();
    }
}
