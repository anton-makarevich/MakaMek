using Shouldly;
using Sanet.MakaMek.Avalonia.Styles.TemplatedControls;

namespace MakaMek.Avalonia.Tests.Controls
{
    public class ActionButtonTests
    {
        [Fact]
        public void ActionButton_WhenCreated_ShouldHaveDefaultProperties()
        {
            // Arrange & Act
            var button = new ActionButton();

            // Assert
            button.ShouldNotBeNull();
            button.IsEnabled.ShouldBeTrue();
        }

        [Fact]
        public void ActionButton_WhenDisabled_ShouldNotBeInteractive()
        {
            // Arrange
            var button = new ActionButton
            {
                // Act
                IsEnabled = false
            };

            // Assert
            button.IsEnabled.ShouldBeFalse();
        }
    }
}
