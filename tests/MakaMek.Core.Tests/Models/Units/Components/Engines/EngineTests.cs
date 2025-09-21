using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Engines;

public class EngineTests
{
    private readonly ComponentData _engineData = new ComponentData
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
        sut.MountedAtSlots.ToList().Count.ShouldBe(6);
        sut.MountedAtSlots.ShouldBe([0, 1, 2, 7, 8, 9]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Engine);
        sut.IsRemovable.ShouldBeTrue();
        sut.HealthPoints.ShouldBe(3);
        sut.NumberOfHeatSinks.ShouldBe(10);
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
}
