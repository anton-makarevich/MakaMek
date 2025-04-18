using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Combat.Modifiers;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Combat.Modifiers;

public class TerrainAttackModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var hexCoordinates = new HexCoordinates(3, 4);
        var modifier = new TerrainAttackModifier
        {
            Value = 1,
            TerrainId = MakaMekTerrains.LightWoods,
            Location = hexCoordinates
        };
        _localizationService.GetString("Modifier_Terrain").Returns("{0} at {1}: {2}");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("LightWoods at 0304: 1");
        _localizationService.Received(1).GetString("Modifier_Terrain");
    }
}
