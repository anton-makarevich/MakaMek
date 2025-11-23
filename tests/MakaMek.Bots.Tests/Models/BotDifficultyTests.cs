using Sanet.MakaMek.Bots.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models;

public class BotDifficultyTests
{
    [Fact]
    public void BotDifficulty_ShouldHaveExpectedValues()
    {
        // Arrange & Act
        var easyValue = (int)BotDifficulty.Easy;
        var mediumValue = (int)BotDifficulty.Medium;
        var hardValue = (int)BotDifficulty.Hard;

        // Assert
        easyValue.ShouldBe(0);
        mediumValue.ShouldBe(1);
        hardValue.ShouldBe(2);
    }
}

