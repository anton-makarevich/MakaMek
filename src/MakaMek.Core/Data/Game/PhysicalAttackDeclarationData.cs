using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

/// <summary>
/// Serializable data about a physical attack a unit has declared for the current turn
/// </summary>
public record PhysicalAttackDeclarationData
{
    /// <summary>
    /// The kind of physical attack that was declared
    /// </summary>
    public required PhysicalAttackType AttackType { get; init; }

    /// <summary>
    /// The attacker's limb locations selected for the attack
    /// </summary>
    public required IReadOnlyList<PartLocation> AttackerLimbs { get; init; }

    /// <summary>
    /// The id of the unit the attack was declared against
    /// </summary>
    public required Guid TargetId { get; init; }
}
