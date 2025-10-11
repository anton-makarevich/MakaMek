using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Services;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services;

public class MmuxUnitsLoaderTests
{
    private readonly MmuxUnitsLoader _sut;
    private readonly IUnitCachingService _unitCachingService;

    public MmuxUnitsLoaderTests()
    {
        _unitCachingService = Substitute.For<IUnitCachingService>();
        _sut = new MmuxUnitsLoader(_unitCachingService);
    }

    [Fact]
    public async Task LoadUnits_WhenCalled_ReturnsUnitsFromCachingService()
    {
        // Arrange
        var expectedUnits = new List<UnitData>
        {
            new()
            {
                Model = "TestMech",
                Chassis = "TestChassis",
                Mass = 30,
                EngineRating = 125,
                EngineType = "Fusion",
                ArmorValues = [],
                Equipment = [],
                AdditionalAttributes = [],
                Quirks = []
            },
            new()
            {
                Model = "AnotherMech",
                Chassis = "AnotherChassis",
                Mass = 30,
                EngineRating = 125,
                EngineType = "Fusion",
                ArmorValues = [],
                Equipment = [],
                AdditionalAttributes = [],
                Quirks = []
            }
        };
        
        _unitCachingService.GetAllUnits().Returns(expectedUnits);

        // Act
        var result = await _sut.LoadUnits();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(expectedUnits.Count);
        result.ShouldBe(expectedUnits);
    }

    [Fact]
    public async Task LoadUnits_WhenNoUnitsInCache_ReturnsEmptyList()
    {
        // Arrange
        _unitCachingService.GetAllUnits().Returns(new List<UnitData>());

        // Act
        var result = await _sut.LoadUnits();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadUnits_WhenCalled_InvokesGetAllUnitsOnce()
    {
        // Arrange
        _unitCachingService.GetAllUnits().Returns(new List<UnitData>());

        // Act
        await _sut.LoadUnits();

        // Assert
        await _unitCachingService.Received(1).GetAllUnits();
    }
}
