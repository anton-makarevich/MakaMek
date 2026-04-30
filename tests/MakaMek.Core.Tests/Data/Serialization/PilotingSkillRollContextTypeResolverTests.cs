using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Shouldly;
using PilotingSkillRollContextTypeResolver = Sanet.MakaMek.Core.Data.Serialization.PilotingSkillRollContextTypeResolver;

namespace Sanet.MakaMek.Core.Tests.Data.Serialization;

public class PilotingSkillRollContextTypeResolverTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(new PilotingSkillRollContextTypeResolver(), new DefaultJsonTypeInfoResolver()),
        WriteIndented = true
    };

    [Fact]
    public void Should_Have_Polymorphism_Options_Configured()
    {
        // Arrange
        var sut = new PilotingSkillRollContextTypeResolver();
        
        // Act
        var typeInfo = sut.GetTypeInfo(typeof(PilotingSkillRollContext), _options);
        
        // Assert
        typeInfo.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.TypeDiscriminatorPropertyName.ShouldBe("$type");
        typeInfo.PolymorphismOptions.IgnoreUnrecognizedTypeDiscriminators.ShouldBeFalse();
        typeInfo.PolymorphismOptions.UnknownDerivedTypeHandling.ShouldBe(JsonUnknownDerivedTypeHandling.FailSerialization);
    }
    
    [Fact]
    public void Should_Register_Known_PilotingSkillRollContext_Types()
    {
        // Arrange
        var sut = new PilotingSkillRollContextTypeResolver();
        
        // Act
        var typeInfo = sut.GetTypeInfo(typeof(PilotingSkillRollContext), _options);
        
        // Assert
        typeInfo!.PolymorphismOptions.ShouldNotBeNull();
        typeInfo.PolymorphismOptions.DerivedTypes.ShouldNotBeEmpty();
        
        // Check for specific known types
        var derivedTypeNames = typeInfo.PolymorphismOptions.DerivedTypes
            .Select(dt => dt.DerivedType.Name)
            .ToList();
        
        // These types should be registered by the source generator
        derivedTypeNames.ShouldContain("EnteringDeepWaterRollContext");
        derivedTypeNames.ShouldContain("PilotDamageFromFallRollContext");
    }
    
    [Fact]
    public void Should_Register_All_PilotingSkillRollContext_Derived_Types_In_Assembly()
    {
        // Arrange
        var sut = new PilotingSkillRollContextTypeResolver();
        
        // Act
        var typeInfo = sut.GetTypeInfo(typeof(PilotingSkillRollContext), _options);
        
        // Find all PilotingSkillRollContext derived types in the assembly
        var assembly = typeof(PilotingSkillRollContext).Assembly;
        var derivedTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(PilotingSkillRollContext)))
            .ToList();
        
        // Assert
        typeInfo!.PolymorphismOptions.ShouldNotBeNull();
        
        // The number of registered types should match the number of derived types exactly
        typeInfo.PolymorphismOptions.DerivedTypes.Count.ShouldBe(derivedTypes.Count);
        
        // All derived types should be registered
        foreach (var derivedType in derivedTypes)
        {
            var isRegistered = typeInfo.PolymorphismOptions.DerivedTypes
                .Any(dt => dt.DerivedType == derivedType);
            
            isRegistered.ShouldBeTrue($"Type {derivedType.Name} should be registered");
        }
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_PilotingSkillRollContext()
    {
        // Arrange
        var context = new PilotingSkillRollContext(PilotingSkillRollType.GyroHit);
        
        // Act
        var json = JsonSerializer.Serialize(context, _options);
        var deserializedContext = JsonSerializer.Deserialize<PilotingSkillRollContext>(json, _options);
        
        // Assert
        deserializedContext.ShouldNotBeNull();
        deserializedContext.RollType.ShouldBe(PilotingSkillRollType.GyroHit);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_EnteringDeepWaterRollContext()
    {
        // Arrange
        var context = new EnteringDeepWaterRollContext(3);
        
        // Act
        var json = JsonSerializer.Serialize(context, _options);
        var deserializedContext = JsonSerializer.Deserialize<EnteringDeepWaterRollContext>(json, _options);
        
        // Assert
        deserializedContext.ShouldNotBeNull();
        deserializedContext.RollType.ShouldBe(PilotingSkillRollType.WaterEntry);
        deserializedContext.WaterDepth.ShouldBe(3);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_PilotDamageFromFallRollContext()
    {
        // Arrange
        var context = new PilotDamageFromFallRollContext(2);
        
        // Act
        var json = JsonSerializer.Serialize(context, _options);
        var deserializedContext = JsonSerializer.Deserialize<PilotDamageFromFallRollContext>(json, _options);
        
        // Assert
        deserializedContext.ShouldNotBeNull();
        deserializedContext.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        deserializedContext.LevelsFallen.ShouldBe(2);
    }
    
    [Fact]
    public void Should_Serialize_And_Deserialize_Polymorphic_PilotingSkillRollContext()
    {
        // Arrange
        PilotingSkillRollContext originalContext = new EnteringDeepWaterRollContext(5);
        
        // Act
        var json = JsonSerializer.Serialize(originalContext, _options);
        var deserializedContext = JsonSerializer.Deserialize<PilotingSkillRollContext>(json, _options);
        
        // Assert
        deserializedContext.ShouldNotBeNull();
        deserializedContext.ShouldBeOfType<EnteringDeepWaterRollContext>();
        var waterContext = (EnteringDeepWaterRollContext)deserializedContext;
        waterContext.WaterDepth.ShouldBe(5);
    }
    
    [Fact]
    public void Should_Deserialize_Array_Of_PilotingSkillRollContexts()
    {
        // Arrange
        var contexts = new[]
        {
            new PilotingSkillRollContext(PilotingSkillRollType.GyroHit),
            new EnteringDeepWaterRollContext(2),
            new PilotDamageFromFallRollContext(3)
        };
        
        // Act
        var json = JsonSerializer.Serialize(contexts, _options);
        var deserializedContexts = JsonSerializer.Deserialize<PilotingSkillRollContext[]>(json, _options);
        
        // Assert - runtime types
        deserializedContexts.ShouldNotBeNull();
        deserializedContexts.Length.ShouldBe(3);
        deserializedContexts[0].ShouldBeOfType<PilotingSkillRollContext>();
        deserializedContexts[1].ShouldBeOfType<EnteringDeepWaterRollContext>();
        deserializedContexts[2].ShouldBeOfType<PilotDamageFromFallRollContext>();

        // Assert - wire contract validation
        json.ShouldContain("\"$type\"");
        json.ShouldContain("EnteringDeepWaterRollContext");
        json.ShouldContain("PilotDamageFromFallRollContext");
    }
}
