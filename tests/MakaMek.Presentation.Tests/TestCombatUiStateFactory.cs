using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Presentation.UiStates;

namespace Sanet.MakaMek.Presentation.Tests;

/// <summary>
/// Builds the real combat UiState factory with the presentation-internal policies, so board view
/// model tests exercise the production phase-state creation instead of a stub.
/// </summary>
internal static class TestCombatUiStateFactory
{
    public static ICombatUiStateFactory Create(ILocalizationService? localizationService = null) =>
        new CombatUiStateFactory(
            localizationService ?? Substitute.For<ILocalizationService>());
}
