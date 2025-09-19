namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Base definition for all components containing immutable properties
/// </summary>
public abstract record ComponentDefinition(
    string Name,
    int Size = 1,
    int HealthPoints = 1,
    int BattleValue = 0,
    bool IsRemovable = true,
    MakaMekComponent ComponentType = MakaMekComponent.HeatSink)
{
    /// <summary>
    /// Gets the manufacturer of this component
    /// </summary>
    public virtual string Manufacturer => "Unknown";
}

/// <summary>
/// Definition for actuator components (shoulder, arm, leg actuators)
/// </summary>
public record ActuatorDefinition(
    string Name,
    MakaMekComponent ComponentType,
    bool IsRemovable = false)
    : ComponentDefinition(Name, 1, 1, 0, IsRemovable, ComponentType);

/// <summary>
/// Definition for internal components (life support, sensors, cockpit, gyro)
/// </summary>
public record InternalDefinition(
    string Name,
    int HealthPoints,
    MakaMekComponent ComponentType,
    int Size = 1)
    : ComponentDefinition(Name, Size, HealthPoints, 0, false, ComponentType);

/// <summary>
/// Definition for simple equipment components (heat sinks, jump jets)
/// </summary>
public record EquipmentDefinition(
    string Name,
    MakaMekComponent ComponentType,
    int BattleValue = 0,
    int Size = 1,
    int HealthPoints = 1,
    bool IsRemovable = true)
    : ComponentDefinition(Name, Size, HealthPoints, BattleValue, IsRemovable, ComponentType);