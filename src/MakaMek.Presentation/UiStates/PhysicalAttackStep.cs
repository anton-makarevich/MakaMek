namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The steps of the physical attack declaration flow: pick the attacker, pick the attack type,
/// pick the target. The limb is picked through the declaration action rather than a separate step.
/// </summary>
public enum PhysicalAttackStep
{
    SelectingUnit,
    ActionSelection,
    TargetSelection
}
