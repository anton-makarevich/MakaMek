using Sanet.Localization;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Creates the combat UiStates for a battle-map view model. The view model asks for a state when the
/// game enters a combat phase; the factory is the only place that composes the states' policies and
/// analyzers, so those stay out of the view model and remain internal to this assembly.
/// </summary>
public interface ICombatUiStateFactory
{
    /// <summary>
    /// Creates the weapons-attack state for the given board.
    /// </summary>
    WeaponsAttackState CreateWeaponsAttackState(BattleMapViewModel viewModel);

    /// <summary>
    /// Creates the physical-attack state for the given board.
    /// </summary>
    PhysicalAttackState CreatePhysicalAttackState(BattleMapViewModel viewModel);
}

/// <summary>
/// Default composition of the combat UiStates. The stateless policies and analyzers are static
/// helpers; the targeting and option-building helpers are per state because they hold selection state
/// and read the game the board is bound to.
/// </summary>
internal sealed class CombatUiStateFactory(
    ILocalizationService localizationService) : ICombatUiStateFactory
{
    public WeaponsAttackState CreateWeaponsAttackState(BattleMapViewModel viewModel) =>
        new(viewModel);

    public PhysicalAttackState CreatePhysicalAttackState(BattleMapViewModel viewModel) =>
        new(viewModel,
            new PhysicalAttackOptionBuilder(
                viewModel.Game ?? throw new InvalidOperationException("Game is not client game"),
                localizationService),
            new PhysicalAttackTargeting());
}
