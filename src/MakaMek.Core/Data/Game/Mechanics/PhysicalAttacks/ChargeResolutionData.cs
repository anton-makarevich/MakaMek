namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;

using Sanet.MakaMek.Core.Models.Units;

/// <summary>
/// The charge-specific part of a physical attack resolution: the outcome, the distance that drove
/// the damage, whether a replacement target was hit, and the collision damage the attacker takes.
/// A charge has no attacking limb, so it cannot be expressed with the limb-based fields alone.
/// </summary>
/// <param name="Outcome">Whether the charge hit, missed or automatically failed.</param>
/// <param name="HexesMoved">The hexes the attacker moved before entering the target hex.</param>
/// <param name="IsReplacementTarget">Whether the resolved target replaced the originally declared one.</param>
/// <param name="OriginalTargetId">The unit the charge was declared against.</param>
/// <param name="AttackerCollision">
/// The 5-point clusters of collision damage applied to the attacker on a hit, or null when the
/// charge did no collision damage. The clusters always use the Front direction.
/// </param>
/// <param name="AttackerDestroyedParts">Parts of the attacker destroyed by its collision damage.</param>
/// <param name="AttackerUnitDestroyed">Whether the collision damage destroyed the attacker.</param>
public record ChargeResolutionData(
    ChargeOutcome Outcome,
    int HexesMoved,
    bool IsReplacementTarget,
    Guid OriginalTargetId,
    AttackHitLocationsData? AttackerCollision = null,
    IReadOnlyList<PartLocation>? AttackerDestroyedParts = null,
    bool AttackerUnitDestroyed = false);
