using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Physical;

public class RelativePilotingModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public RelativePilotingModifierTests()
    {
        _localizationService.GetString("Modifier_RelativePiloting")
            .Returns("Relative Piloting ({0} vs {1}): {2:+#;-#;0}");
    }

    [Theory]
    [InlineData(5, 5, 0, "Relative Piloting (5 vs 5): 0")]
    [InlineData(6, 4, 2, "Relative Piloting (6 vs 4): +2")]
    [InlineData(4, 6, -2, "Relative Piloting (4 vs 6): -2")]
    public void Render_FormatsBothRatingsAndTheDifference(
        int attackerPiloting,
        int targetPiloting,
        int value,
        string expected)
    {
        // Arrange
        var sut = new RelativePilotingModifier
        {
            AttackerPiloting = attackerPiloting,
            TargetPiloting = targetPiloting,
            Value = value
        };

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe(expected);
        _localizationService.Received(1).GetString("Modifier_RelativePiloting");
    }
}
