using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Presentation.UiStates;

namespace Sanet.MakaMek.Presentation;

/// <summary>
/// Presentation-layer service registrations. Kept in this assembly so the combat UiState policies and
/// analyzers stay internal; the application composition root only calls the extension.
/// </summary>
public static class PresentationServices
{
    /// <summary>
    /// Registers the factory that composes the combat UiStates and the stateless policies it shares.
    /// </summary>
    public static IServiceCollection AddCombatUiStateFactory(this IServiceCollection services)
    {
        services.AddTransient<WeaponsAttackSelectionPolicy>();
        services.AddTransient<WeaponRangeAnalyzer>();
        services.AddTransient<PhysicalAttackSelectionPolicy>();
        services.AddTransient<ICombatUiStateFactory, CombatUiStateFactory>();
        return services;
    }
}
