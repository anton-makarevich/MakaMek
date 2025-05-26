using System.Text.Json;
using System.Text.Json.Serialization;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Transport;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.Transport;

public class RollModifierTypeResolverTests
{
    private readonly JsonSerializerOptions _options;
    
    public RollModifierTypeResolverTests()
    {
        _options = new JsonSerializerOptions
        {
            TypeInfoResolver = new RollModifierTypeResolver(),
            WriteIndented = true
        };
    }
    
    [Fact]
    public void Should_Have_Polymorphism_Options_Configured()
    {
        // Arrange
        var resolver = new RollModifierTypeResolver();
        
        // Act
        var typeInfo = resolver.GetTypeInfo(typeof(RollModifier), _options);
        
        // Assert
        typeInfo.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.TypeDiscriminatorPropertyName.ShouldBe("$type");
        typeInfo.PolymorphismOptions.IgnoreUnrecognizedTypeDiscriminators.ShouldBeFalse();
        typeInfo.PolymorphismOptions.UnknownDerivedTypeHandling.ShouldBe(JsonUnknownDerivedTypeHandling.FailSerialization);
    }
    
    [Fact]
    public void Should_Register_Known_RollModifier_Types()
    {
        // Arrange
        var resolver = new RollModifierTypeResolver();
        
        // Act
        var typeInfo = resolver.GetTypeInfo(typeof(RollModifier), _options);
        
        // Assert
        typeInfo.PolymorphismOptions.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.DerivedTypes.ShouldNotBeEmpty();
        
        // Check for specific known types
        var derivedTypeNames = typeInfo.PolymorphismOptions.DerivedTypes
            .Select(dt => dt.DerivedType.Name)
            .ToList();
        
        // These types should be registered by the source generator
        derivedTypeNames.ShouldContain("GunneryRollModifier");
        derivedTypeNames.ShouldContain("DamagedGyroModifier");
        derivedTypeNames.ShouldContain("FallingLevelsModifier");
    }
    
    [Fact]
    public void Should_Register_All_RollModifier_Derived_Types_In_Assembly()
    {
        // Arrange
        var resolver = new RollModifierTypeResolver();
        
        // Act
        var typeInfo = resolver.GetTypeInfo(typeof(RollModifier), _options);
        
        // Find all RollModifier derived types in the assembly
        var assembly = typeof(RollModifier).Assembly;
        var derivedTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RollModifier)))
            .ToList();
        
        // Assert
        typeInfo.PolymorphismOptions.ShouldNotBeNull();
        
        // The number of registered types should match the number of derived types
        typeInfo.PolymorphismOptions.DerivedTypes.Count.ShouldBeGreaterThanOrEqualTo(derivedTypes.Count);
        
        // All derived types should be registered
        foreach (var derivedType in derivedTypes)
        {
            var isRegistered = typeInfo.PolymorphismOptions.DerivedTypes
                .Any(dt => dt.DerivedType == derivedType);
            
            isRegistered.ShouldBeTrue($"Type {derivedType.Name} should be registered");
        }
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_GunneryRollModifier()
    {
        // Arrange
        var modifier = new GunneryRollModifier
        {
            Value = 2
        };
        
        // Act
        var json = JsonSerializer.Serialize(modifier, _options);
        var deserializedModifier = JsonSerializer.Deserialize<GunneryRollModifier>(json, _options);
        
        // Assert
        deserializedModifier.ShouldNotBeNull();
        deserializedModifier.Value.ShouldBe(2);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_DamagedGyroModifier()
    {
        // Arrange
        var modifier = new DamagedGyroModifier
        {
            Value = 3,
            HitsCount = 2
        };
        
        // Act
        var json = JsonSerializer.Serialize(modifier, _options);
        var deserializedModifier = JsonSerializer.Deserialize<DamagedGyroModifier>(json, _options);
        
        // Assert
        deserializedModifier.ShouldNotBeNull();
        deserializedModifier.Value.ShouldBe(3);
        deserializedModifier.HitsCount.ShouldBe(2);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_FallingLevelsModifier()
    {
        // Arrange
        var modifier = new FallingLevelsModifier
        {
            Value = 3,
            LevelsFallen = 2
        };
        
        // Act
        var json = JsonSerializer.Serialize(modifier, _options);
        var deserializedModifier = JsonSerializer.Deserialize<FallingLevelsModifier>(json, _options);
        
        // Assert
        deserializedModifier.ShouldNotBeNull();
        deserializedModifier.Value.ShouldBe(3);
        deserializedModifier.LevelsFallen.ShouldBe(2);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_Polymorphic_RollModifier()
    {
        // Arrange
        RollModifier originalModifier = new GunneryRollModifier
        {
            Value = 2
        };
        
        // Act
        var json = JsonSerializer.Serialize(originalModifier, _options);
        var deserializedModifier = JsonSerializer.Deserialize<RollModifier>(json, _options);
        
        // Assert
        deserializedModifier.ShouldNotBeNull();
        deserializedModifier.ShouldBeOfType<GunneryRollModifier>();
        deserializedModifier.Value.ShouldBe(2);
    }
    
    [Fact]
    public void Should_Deserialize_Array_Of_RollModifiers()
    {
        // Arrange
        var modifiers = new RollModifier[]
        {
            new GunneryRollModifier { Value = 1 },
            new DamagedGyroModifier { Value = 2, HitsCount = 1 },
            new FallingLevelsModifier { Value = 3, LevelsFallen = 2 }
        };
        
        // Act
        var json = JsonSerializer.Serialize(modifiers, _options);
        var deserializedModifiers = JsonSerializer.Deserialize<RollModifier[]>(json, _options);
        
        // Assert
        deserializedModifiers.ShouldNotBeNull();
        deserializedModifiers.Length.ShouldBe(3);
        deserializedModifiers[0].ShouldBeOfType<GunneryRollModifier>();
        deserializedModifiers[1].ShouldBeOfType<DamagedGyroModifier>();
        deserializedModifiers[2].ShouldBeOfType<FallingLevelsModifier>();
    }
}
