using Sanet.MakaMek.Avalonia.Controls;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class HexControlConfigurationViewModelTests
{
    [Fact]
    public void ShowLabels_DefaultsToTrue()
    {
        // Arrange & Act
        var sut = new HexControlConfigurationViewModel();

        // Assert
        sut.ShowLabels.ShouldBeTrue();
    }

    [Fact]
    public void ShowOutline_DefaultsToTrue()
    {
        // Arrange & Act
        var sut = new HexControlConfigurationViewModel();

        // Assert
        sut.ShowOutline.ShouldBeTrue();
    }

    [Fact]
    public void ToConfiguration_ReturnsCorrectConfiguration()
    {
        // Arrange
        var sut = new HexControlConfigurationViewModel
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
