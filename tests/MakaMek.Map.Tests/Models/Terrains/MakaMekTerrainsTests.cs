using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class MakaMekTerrainsTests
{
    [Fact]
    public void Road_HasDistinctIntValue()
    {
        var value = (int)MakaMekTerrains.Road;

        value.ShouldBe(5);
    }

    [Fact]
    public void Pavement_HasDistinctIntValue()
    {
        var value = (int)MakaMekTerrains.Pavement;

        value.ShouldBe(6);
    }

    [Fact]
    public void Bridge_HasDistinctIntValue()
    {
        var value = (int)MakaMekTerrains.Bridge;

        value.ShouldBe(7);
    }

    [Fact]
    public void AllNewValues_AreDistinct()
    {
        var values = new[]
        {
            (int)MakaMekTerrains.Road,
            (int)MakaMekTerrains.Pavement,
            (int)MakaMekTerrains.Bridge
        };

        values.ShouldBe(new[] { 5, 6, 7 });
    }
}
