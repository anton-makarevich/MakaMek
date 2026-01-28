using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// Base class for component-specific state data
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(EngineStateData), "Engine")]
[JsonDerivedType(typeof(AmmoStateData), "Ammo")]
[JsonDerivedType(typeof(WeaponStateData), "Weapon")]
public abstract record ComponentSpecificData;