using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Engines;

public class EngineTests
{
    private readonly ComponentData _engineData = new()
    {
        Type = MakaMekComponent.Engine,
        Assignments =
        [
            new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
            new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
        ],
        SpecificData = new EngineStateData(EngineType.Fusion, 100)
    };

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Engine(_engineData);

        // Assert
        sut.Name.ShouldBe("Fusion Engine 100");
        sut.Rating.ShouldBe(100);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Engine);
        sut.IsRemovable.ShouldBeTrue();
        sut.HealthPoints.ShouldBe(3);
        sut.NumberOfHeatSinks.ShouldBe(4);
    }

    [Fact]
    public void FirstHit_DoesNotDestroyComponent()
    {
        var sut = new Engine(_engineData);

        sut.Hit();

        sut.IsDestroyed.ShouldBeFalse();
        sut.Hits.ShouldBe(1);
    }

    [Fact]
    public void SecondHit_DoesNotDestroyComponent()
    {
        var sut = new Engine(_engineData);

        sut.Hit();
        sut.Hit();

        sut.IsDestroyed.ShouldBeFalse();
        sut.Hits.ShouldBe(2);
    }

    [Fact]
    public void ThirdHit_DoesDestroyComponent()
    {
        var sut = new Engine(_engineData);

        sut.Hit();
        sut.Hit();
        sut.Hit();

        sut.IsDestroyed.ShouldBeTrue();
        sut.Hits.ShouldBe(3);
    }

    [Theory]
    [InlineData(0, 0)] // No hits, no heat penalty
    [InlineData(1, 5)] // First hit, +5 heat
    [InlineData(2, 10)] // Second hit, +10 heat
    [InlineData(3, 0)] // Third hit, engine shutdown (no heat penalty)
    public void HeatPenalty_ReturnsCorrectValueBasedOnHits(int hits, int expectedPenalty)
    {
        // Arrange
        var sut = new Engine(_engineData);

        // Act
        for (int i = 0; i < hits; i++)
        {
            sut.Hit();
        }

        // Assert
        sut.HeatPenalty?.Value.ShouldBe(expectedPenalty);
    }

    [Theory]
    [InlineData(EngineType.Fusion, 6)]
    [InlineData(EngineType.XLFusion, 10)]
    [InlineData(EngineType.Light, 4)]
    [InlineData(EngineType.Compact, 3)]
    [InlineData(EngineType.ICE, 6)]
    [InlineData((EngineType)999, 6)]
    public void Size_ShouldBeCorrect_BasedOnEngineType(EngineType engineType, int expectedSize)
    {
        var sut = new Engine(new ComponentData
        {
            Type = MakaMekComponent.Engine,
            SpecificData = new EngineStateData(engineType,
                100),
            Assignments = []
        });

        sut.Size.ShouldBe(expectedSize);
    }

    [Fact]
    public void Constructor_Throws_OnNullSpecificData()
    {
        var data = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            SpecificData = null,
            Assignments = []
        };
        Should.Throw<ArgumentException>(() => new Engine(data));
    }
    
    [Fact]
    public void Manufacturer_ShouldBeOverridable_WhenProvided()
    {
        var data = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            SpecificData = new EngineStateData(EngineType.Fusion, 100),
            Assignments = [],
            Manufacturer = "Test Manufacturer"
        };
        var sut = new Engine(data);
        sut.Manufacturer.ShouldBe("Test Manufacturer");
    }
    
    [Fact]
    public void Manufacturer_ShouldFallBackToDefault_WhenNotProvided()
    {
        var data = new ComponentData
        {
            Type = MakaMekComponent.Engine,
            SpecificData = new EngineStateData(EngineType.Fusion, 100),
            Assignments = []
        };
        var sut = new Engine(data);
        sut.Manufacturer.ShouldBe("Unknown");
    }
    [Theory]
    [InlineData(30, 1)]
    [InlineData(45, 1)]
    [InlineData(50, 2)]
    [InlineData(100, 4)]
    [InlineData(240, 9)]
    [InlineData(250, 10)]
    
    public void NumberOfHeatSinks_ShouldBeCorrect_BasedOnRating(int rating, int expectedNumberOfHeatSinks)
    {
        var sut = new Engine(new ComponentData
        {
            Type = MakaMekComponent.Engine,
            SpecificData = new EngineStateData(EngineType.Fusion, rating),
            Assignments = []
        });

        sut.NumberOfHeatSinks.ShouldBe(expectedNumberOfHeatSinks);
    }
}
