using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Presentation.UiStates;

namespace Sanet.MakaMek.Presentation;

/// <summary>
/// Presentation-layer service registrations. Kept in this assembly so the combat UiState composition
/// stays internal; the application composition root only calls the extension.
/// </summary>
public static class PresentationServices
{
    /// <summary>
    /// Registers the factory that composes the combat UiStates.
    /// </summary>
    public static IServiceCollection AddCombatUiStateFactory(this IServiceCollection services)
    {
        services.AddTransient<ICombatUiStateFactory, CombatUiStateFactory>();
        return services;
    }
}
