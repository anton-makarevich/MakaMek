using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class ComponentSubmergedTests
{
    [Fact]
    public void IsSubmerged_ReturnsFalse_WhenNotDeployed()
    {
        // Arrange
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();

        // Act - not deployed, so no hex

        // Assert
        sut.IsSubmerged.ShouldBeFalse();
    }

    [Fact]
    public void IsSubmerged_ReturnsFalse_WhenOnClearTerrain()
    {
        // Arrange
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new ClearTerrain());
        mech.Deploy(position, hex);

        // Act

        // Assert
        sut.IsSubmerged.ShouldBeFalse();
    }

    [Fact]
    public void IsSubmerged_ReturnsFalse_WaterDepthLessThanUnitHeight()
    {
        // Arrange - standing mech has height 2, shallow water has depth 0
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(0)); // Depth 0
        mech.Deploy(position, hex);

        // Act & Assert
        sut.IsSubmerged.ShouldBeFalse(); // 0 < 2 (unit height)
    }

    [Fact]
    public void IsSubmerged_ReturnsFalse_WaterDepthOneLessThanUnitHeight()
    {
        // Arrange - standing mech has height 2, depth 1 water
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-1)); // Depth 1
        mech.Deploy(position, hex);

        // Act & Assert
        sut.IsSubmerged.ShouldBeFalse(); // 1 < 2 (unit height)
    }

    [Fact]
    public void IsSubmerged_ReturnsTrue_WaterDepthEqualsUnitHeight()
    {
        // Arrange - standing mech has height 2, depth 2 water
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-2)); // Depth 2
        mech.Deploy(position, hex);

        // Act & Assert
        sut.IsSubmerged.ShouldBeTrue(); // 2 >= 2 (unit height)
    }

    [Fact]
    public void IsSubmerged_ReturnsTrue_WaterDepthGreaterThanUnitHeight()
    {
        // Arrange - standing mech has height 2, depth 3 water
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-3)); // Depth 3
        mech.Deploy(position, hex);

        // Act & Assert
        sut.IsSubmerged.ShouldBeTrue(); // 3 >= 2 (unit height)
    }

    private static List<UnitPart> CreateBasicPartsData()
    {
        var engineData = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            Assignments =
            [
                new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
            ],
            SpecificData = new EngineStateData(EngineType.Fusion, 250)
        };
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(engineData), [0, 1, 2, 7, 8, 9]);
        return
        [
            new Head("Head", 9, 3),
            centerTorso,
            new Leg("LeftLeg", PartLocation.LeftLeg, 25, 8)
        ];
    }
}
