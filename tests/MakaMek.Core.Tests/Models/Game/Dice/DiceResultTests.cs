using Shouldly;
using Sanet.MakaMek.Core.Models.Game.Dice;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Dice;

public class DiceResultTests
{
    [Fact]
    public void SettingResult_ShouldThrowException_WhenValueIsOutOfRange()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new DiceResult(7));
        ex.ParamName.ShouldBe("Result");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    public void SettingResult_ShouldSetValue_WhenValueIsInRange(int input, int expected)
    {
        // Arrange
        var diceResult = new DiceResult(input);
        
        // Assert
        diceResult.Result.ShouldBe(expected);
    }
}