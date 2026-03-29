using System.Globalization;
using System.Windows.Input;
using Sanet.MakaMek.Avalonia.Converters;
using Sanet.MakaMek.Avalonia.Models;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Converters;

public class ActionToCommandConverterTests
{
    private readonly ActionToCommandConverter _sut = new();

    [Fact]
    public void Convert_WithAction_ReturnsLambdaCommand()
    {
        // Arrange
        var actionExecuted = false;

        // Act
        var result = _sut.Convert((Action)Action, typeof(ICommand), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<LambdaCommand>();
        var command = (ICommand)result;
        command.CanExecute(null).ShouldBeTrue();
        
        // Execute the command and verify the action was called
        command.Execute(null);
        actionExecuted.ShouldBeTrue();
        return;

        void Action() => actionExecuted = true;
    }

    [Fact]
    public void Convert_WithNull_ReturnsNull()
    {
        // Act
        var result = _sut.Convert(null, typeof(ICommand), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Convert_WithNonActionValue_ReturnsNull()
    {
        // Arrange
        const string notAnAction = "not an action";

        // Act
        var result = _sut.Convert(notAnAction, typeof(ICommand), null, CultureInfo.InvariantCulture);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act & Assert
        Should.Throw<NotSupportedException>(() =>
            _sut.ConvertBack(null, typeof(Action), null, CultureInfo.InvariantCulture));
    }
}
