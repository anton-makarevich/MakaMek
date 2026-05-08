using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class JumpJetsFacts
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var sut = new JumpJets();

        // Assert
        sut.Name.ShouldBe("Jump Jets");
        sut.Size.ShouldBe(1);
        sut.JumpMp.ShouldBe(1);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.JumpJet);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_SetsIsDestroyedToTrue()
    {
        // Arrange
        var sut = new JumpJets();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void IsAvailable_ReturnsTrue_WhenNotInWater()
    {
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        mech.Deploy(position, hex);

        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void IsAvailable_ReturnsTrue_InShallowWater()
    {
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(0));
        mech.Deploy(position, hex);

        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void IsAvailable_ReturnsTrue_InDepth1Water_WhenTorsoMounted()
    {
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-1));
        mech.Deploy(position, hex);

        sut.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public void IsAvailable_ReturnsFalse_InDepth1Water_WhenLegMounted()
    {
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var leftLeg = parts.OfType<Leg>().First(l => l.Location == PartLocation.LeftLeg);
        leftLeg.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-1));
        mech.Deploy(position, hex);

        sut.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void IsAvailable_ReturnsFalse_InDepth2Water_AnyMount()
    {
        var sut = new JumpJets();
        var parts = CreateBasicPartsData();
        var centerTorso = parts.OfType<CenterTorso>().First();
        centerTorso.TryAddComponent(sut).ShouldBeTrue();
        var mech = new Mech("Test", "TST-1A", 50, parts);
        var position = new HexPosition(new HexCoordinates(1, 1), HexDirection.Top);
        var hex = new Hex(position.Coordinates);
        hex.AddTerrain(new WaterTerrain(-2));
        mech.Deploy(position, hex);

        sut.IsAvailable.ShouldBeFalse();
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

