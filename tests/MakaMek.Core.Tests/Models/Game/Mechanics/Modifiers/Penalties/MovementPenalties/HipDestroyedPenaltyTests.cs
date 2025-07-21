using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

public class HipDestroyedPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Create_WithNoDestroyedHips_ShouldReturnZeroPenalty()
    {
        // Act
        var result = HipDestroyedPenalty.Create(0, 6);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(6, 3)] // 6 MP -> 3 MP (halved), penalty = 6 - 3 = 3
    [InlineData(5, 2)] // 5 MP -> 3 MP (ceiling of 2.5), penalty = 5 - 3 = 2
    [InlineData(4, 2)] // 4 MP -> 2 MP (halved), penalty = 4 - 2 = 2
    [InlineData(3, 1)] // 3 MP -> 2 MP (ceiling of 1.5), penalty = 3 - 2 = 1
    public void Create_WithOneDestroyedHip_ShouldHalveMovement(int baseMp, int expectedPenalty)
    {
        // Act
        var result = HipDestroyedPenalty.Create(1, baseMp);

        // Assert
        result!.Value.ShouldBe(expectedPenalty);
        result.DestroyedHipCount.ShouldBe(1);
        result.BaseWalkingMp.ShouldBe(baseMp);
    }

    [Theory]
    [InlineData(6, 6)] // 6 MP -> 0 MP, penalty = 6
    [InlineData(4, 4)] // 4 MP -> 0 MP, penalty = 4
    [InlineData(8, 8)] // 8 MP -> 0 MP, penalty = 8
    public void Create_WithTwoOrMoreDestroyedHips_ShouldReduceMovementToZero(int baseMp, int expectedPenalty)
    {
        // Act
        var result = HipDestroyedPenalty.Create(2, baseMp);

        // Assert
        result!.Value.ShouldBe(expectedPenalty);
        result.DestroyedHipCount.ShouldBe(2);
        result.BaseWalkingMp.ShouldBe(baseMp);
    }

    [Fact]
    public void Render_WithOneDestroyedHip_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = HipDestroyedPenalty.Create(1, 6);
        _localizationService.GetString("Penalty_HipDestroyed_Single").Returns("Hip Actuator Destroyed: -{0} MP (movement halved)");

        // Act
        var result = sut!.Render(_localizationService);

        // Assert
        result.ShouldBe("Hip Actuator Destroyed: -3 MP (movement halved)");
        _localizationService.Received(1).GetString("Penalty_HipDestroyed_Single");
    }

    [Fact]
    public void Render_WithTwoDestroyedHips_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = HipDestroyedPenalty.Create(2, 6);
        _localizationService.GetString("Penalty_HipDestroyed_Both").Returns("Both Hip Actuators Destroyed: Movement reduced to 0");

        // Act
        var result = sut!.Render(_localizationService);

        // Assert
        result.ShouldBe("Both Hip Actuators Destroyed: Movement reduced to 0");
        _localizationService.Received(1).GetString("Penalty_HipDestroyed_Both");
    }
}
