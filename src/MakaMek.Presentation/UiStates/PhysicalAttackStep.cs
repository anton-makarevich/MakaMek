namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The steps of the physical attack declaration flow: pick the attacker, pick the target, then
/// declare through the dialog. The attack type and limbs are picked in the declaration step rather
/// than through a separate action-selection step, because availability depends on the target.
/// </summary>
public enum PhysicalAttackStep
{
    SelectingUnit,
    TargetSelection,
    Declaration
}
