using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

/// <summary>
/// The Death From Above specific part of a physical attack resolution: the outcome, the damage
/// dealt to the target and to the attacker's legs, and the attacker-side destruction metadata.
/// A DFA has no attacking limb, so it cannot be expressed with the limb-based fields alone.
/// </summary>
/// <param name="Outcome">Whether the attack hit, missed, failed or destroyed the attacker.</param>
/// <param name="TargetHitLocations">The target's damage clusters, or null when no damage was dealt.</param>
/// <param name="AttackerLegHitLocations">
/// The 5-point clusters of leg damage the attacker takes on a hit, or null when the attack did not
/// hit. The clusters always use the Front direction and the Kick Location Table.
/// </param>
/// <param name="OriginalTargetId">The unit the DFA was declared against.</param>
/// <param name="AttackerDestroyedParts">Parts of the attacker destroyed by its own leg damage.</param>
/// <param name="AttackerUnitDestroyed">Whether the resolution destroyed the attacker.</param>
public record DfaResolutionData(
    DfaOutcome Outcome,
    AttackHitLocationsData? TargetHitLocations = null,
    AttackHitLocationsData? AttackerLegHitLocations = null,
    Guid OriginalTargetId = default,
    IReadOnlyList<PartLocation>? AttackerDestroyedParts = null,
    bool AttackerUnitDestroyed = false);
