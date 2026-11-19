using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests;

public class PresentationServicesTests
{
    [Fact]
    public void AddCombatUiStateFactory_RegistersFactoryAndItsPolicies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ILocalizationService>());

        // Act
        services.AddCombatUiStateFactory();

        // Assert
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ICombatUiStateFactory>().ShouldBeOfType<CombatUiStateFactory>();
        provider.GetRequiredService<WeaponsAttackSelectionPolicy>().ShouldNotBeNull();
        provider.GetRequiredService<WeaponRangeAnalyzer>().ShouldNotBeNull();
        provider.GetRequiredService<PhysicalAttackSelectionPolicy>().ShouldNotBeNull();
    }

    [Fact]
    public void AddCombatUiStateFactory_ReturnsTheSameCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddCombatUiStateFactory();

        // Assert
        result.ShouldBeSameAs(services);
    }
}
