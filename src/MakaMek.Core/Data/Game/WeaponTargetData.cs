using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Serializable data about a weapon and its target
/// </summary>
public record WeaponTargetData
{
    public required ComponentData Weapon { get; init; }
    public required Guid TargetId { get; init; }
    public required bool IsPrimaryTarget { get; init; }

    /// <summary>
    /// The specific body part being targeted for aimed shots.
    /// Null indicates a normal (non-aimed) shot.
    /// </summary>
    public PartLocation? AimedShotTarget { get; init; }
}
