using System.Text.Json;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Units.Components;

public class ComponentSpecificDataTests
{
    [Fact]
    public void EngineStateData_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var originalData = new EngineStateData(EngineType.Fusion, 300);

        // Act
        var json = JsonSerializer.Serialize<ComponentSpecificData>(originalData);
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json);

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
        var json = JsonSerializer.Serialize<ComponentSpecificData>(originalData);
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json);

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
        var json = JsonSerializer.Serialize(originalData );
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData?>(json);

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
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json);

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
        var deserializedData = JsonSerializer.Deserialize<ComponentSpecificData>(json);

        // Assert
        deserializedData.ShouldNotBeNull();
        deserializedData.ShouldBeOfType<EngineStateData>();

        var engineData = (EngineStateData)deserializedData;
        engineData.Type.ShouldBe(EngineType.XLFusion);
        engineData.Rating.ShouldBe(250);
    }

    [Fact]
    public void ComponentSpecificData_OriginalErrorScenario_DeserializesCorrectly()
    {
        // This test reproduces the exact scenario from the original error:
        // "Deserialization of interface or abstract types is not supported. Type 'Sanet.MakaMek.Core.Data.Units.Components.ComponentSpecificData'.
        // Path: $.Units[0].Equipment[21].SpecificData | LineNumber: 477 | BytePositionInLine: 27."

        // Arrange - JSON structure similar to what was causing the error
        var json = """
        {
          "PlayerName": "TestPlayer",
          "Units": [
            {
              "Id": "12345678-1234-1234-1234-123456789012",
              "Chassis": "TestMech",
              "Model": "TM-1",
              "Mass": 50,
              "WalkMp": 4,
              "EngineRating": 200,
              "EngineType": "Fusion",
              "ArmorValues": {},
              "Equipment": [
                {
                  "Type": 11,
                  "Assignments": [
                    {
                      "Location": 3,
                      "FirstSlot": 0,
                      "Length": 1
                    }
                  ],
                  "Hits": 0,
                  "IsActive": true,
                  "HasExploded": false,
                  "Name": null,
                  "Manufacturer": null,
                  "SpecificData": {
                    "$type": "Ammo",
                    "RemainingShots": 20
                  }
                }
              ],
              "AdditionalAttributes": {},
              "Quirks": {}
            }
          ],
          "PilotAssignments": [],
          "Tint": "#FF0000",
          "GameOriginId": "12345678-1234-1234-1234-123456789012",
          "Timestamp": "2025-09-24T18:00:00.0000000Z",
          "PlayerId": "12345678-1234-1234-1234-123456789012"
        }
        """;

        // Act - This should NOT throw the original error anymore
        JoinGameCommand result = default;
        Should.NotThrow(() =>
        {
            result = JsonSerializer.Deserialize<JoinGameCommand>(json);
        });

        // Assert - Verify the ComponentSpecificData was deserialized correctly
        result.Units.ShouldNotBeEmpty();
        var equipment = result.Units[0].Equipment;
        equipment.ShouldNotBeEmpty();

        var component = equipment[0];
        component.SpecificData.ShouldNotBeNull();
        component.SpecificData.ShouldBeOfType<AmmoStateData>();

        var ammoData = (AmmoStateData)component.SpecificData!;
        ammoData.RemainingShots.ShouldBe(20);
    }
}
