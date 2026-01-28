using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// Represents weapon-specific state data, including mounting configuration
/// </summary>
public record WeaponStateData(MountingOptions MountingOptions) : ComponentSpecificData;
