using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Serialization;

public class ComponentSpecificDataSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        TypeInfoResolver = new ComponentSpecificDataTypeResolver(),
        WriteIndented = true
    };

    [Fact]
    public void EngineStateData_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var originalData = new EngineStateData(EngineType.Fusion, 300);

        // Act
        var json = JsonSerializer.Serialize<ComponentSpecificData>(originalData, JsonOptions);
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json, JsonOptions);

        // Assert
        deserializedData.ShouldNotBeNull();
        deserializedData.ShouldBeOfType<EngineStateData>();

        var engineData = (EngineStateData)deserializedData;
        engineData.Type.ShouldBe(EngineType.Fusion);
        engineData.Rating.ShouldBe(300);
    }

    [Fact]
    public void AmmoStateData_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var originalData = new AmmoStateData(20);

        // Act
        var json = JsonSerializer.Serialize<ComponentSpecificData>(originalData, JsonOptions);
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json, JsonOptions);

        // Assert
        deserializedData.ShouldNotBeNull();
        deserializedData.ShouldBeOfType<AmmoStateData>();

        var ammoData = (AmmoStateData)deserializedData;
        ammoData.RemainingShots.ShouldBe(20);
    }

    [Fact]
    public void ComponentSpecificData_WithNull_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        ComponentSpecificData? originalData = null;

        // Act
        var json = JsonSerializer.Serialize(originalData, JsonOptions);
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData?>(json, JsonOptions);

        // Assert
        deserializedData.ShouldBeNull();
    }

    [Fact]
    public void ComponentSpecificData_WithAmmoTypeDiscriminator_DeserializesAsAmmoStateData()
    {
        // Arrange
        var json = """
        {
            "$type": "Ammo",
            "RemainingShots": 15
        }
        """;

        // Act
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json, JsonOptions);

        // Assert
        deserializedData.ShouldNotBeNull();
        deserializedData.ShouldBeOfType<AmmoStateData>();

        var ammoData = (AmmoStateData)deserializedData;
        ammoData.RemainingShots.ShouldBe(15);
    }

    [Fact]
    public void ComponentSpecificData_WithEngineTypeDiscriminator_DeserializesAsEngineStateData()
    {
        // Arrange
        var json = """
        {
            "$type": "Engine",
            "Type": 1,
            "Rating": 250
        }
        """;

        // Act
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json, JsonOptions);

        // Assert
        deserializedData.ShouldNotBeNull();
        deserializedData.ShouldBeOfType<EngineStateData>();

        var engineData = (EngineStateData)deserializedData;
        engineData.Type.ShouldBe(EngineType.XLFusion);
        engineData.Rating.ShouldBe(250);
    }
}
