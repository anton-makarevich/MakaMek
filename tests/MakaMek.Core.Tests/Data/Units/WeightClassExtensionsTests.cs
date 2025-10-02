using Sanet.MakaMek.Core.Data.Units;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Units;

public class WeightClassExtensionsTests
{
    [Theory]
    [InlineData(0, WeightClass.Light)]
    [InlineData(35, WeightClass.Light)]
    [InlineData(39, WeightClass.Light)]
    [InlineData(40, WeightClass.Medium)]
    [InlineData(55, WeightClass.Medium)]
    [InlineData(59, WeightClass.Medium)]
    [InlineData(60, WeightClass.Heavy)]
    [InlineData(75, WeightClass.Heavy)]
    [InlineData(79, WeightClass.Heavy)]
    [InlineData(80, WeightClass.Assault)]
    [InlineData(100, WeightClass.Assault)]
    [InlineData(101, WeightClass.Unknown)]
    public void ToWeightClass_ReturnsCorrectClass(int tonnage, WeightClass expectedClass)
    {
        // Act
        var result = tonnage.ToWeightClass();

        // Assert
        result.ShouldBe(expectedClass);
    }
    
}