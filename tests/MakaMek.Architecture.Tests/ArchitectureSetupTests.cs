using ArchUnitNET.Domain;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace Sanet.MakaMek.Architecture.Tests;

/// <summary>
/// Guards against rules passing vacuously because an assembly failed to load or a
/// predicate no longer matches anything.
/// </summary>
public class ArchitectureSetupTests
{
    private static readonly ArchModel Architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void Architecture_ShouldContainEveryProductionAssembly()
    {
        var assemblies = Architecture.Assemblies.Select(a => a.Name).ToList();

        assemblies.ShouldContain(ArchitectureSetup.CoreAssembly.GetName().Name!);
        assemblies.ShouldContain(ArchitectureSetup.MapAssembly.GetName().Name!);
        assemblies.ShouldContain(ArchitectureSetup.ServicesAssembly.GetName().Name!);
        assemblies.ShouldContain(ArchitectureSetup.LocalizationAssembly.GetName().Name!);
        assemblies.ShouldContain(ArchitectureSetup.PresentationAssembly.GetName().Name!);
        assemblies.ShouldContain(ArchitectureSetup.AvaloniaAssembly.GetName().Name!);
    }

    [Fact]
    public void DomainAndPresentationLayers_ShouldContainTypes()
    {
        var types = Architecture.Types;

        var enumerable = types as IType[] ?? [.. types];
        enumerable.Any(t => t.Assembly.Name == ArchitectureSetup.CoreAssembly.GetName().Name).ShouldBeTrue();
        enumerable.Any(t => t.Assembly.Name == ArchitectureSetup.PresentationAssembly.GetName().Name).ShouldBeTrue();
        enumerable.Any(t => t.Assembly.Name == ArchitectureSetup.AvaloniaAssembly.GetName().Name).ShouldBeTrue();
    }

    [Fact]
    public void UiStateRuleInput_ShouldMatchAllUiStates()
    {
        var implementations = Types().That()
            .ImplementInterface(typeof(Presentation.UiStates.IUiState));

        implementations.GetObjects(Architecture).ShouldNotBeEmpty();
    }

    [Fact]
    public void GameCommandRuleInput_ShouldMatchStructAndClassCommands()
    {
        // Most commands are record structs; Types() (not Classes()) must see them.
        var implementations = Types().That()
            .ImplementInterface(typeof(Core.Data.Game.Commands.IGameCommand));

        implementations.GetObjects(Architecture).Count().ShouldBeGreaterThan(10);
    }

    [Fact]
    public void MechanicsRuleInput_ShouldMatchImplementations()
    {
        var mechanicsInterfaces = Interfaces().That()
            .ResideInNamespaceMatching("^Sanet.MakaMek.Core.Models.Game.Mechanics($|\\.)");

        var implementations = Types().That().AreAssignableTo(mechanicsInterfaces);

        implementations.GetObjects(Architecture).ShouldNotBeEmpty();
    }

    [Fact]
    public void ViewModelRuleInput_ShouldMatchViewModels()
    {
        var viewModels = Classes().That()
            .ResideInNamespaceMatching("^Sanet.MakaMek.Presentation.ViewModels$")
            .And().HaveNameEndingWith("ViewModel");

        viewModels.GetObjects(Architecture).ShouldNotBeEmpty();
    }
}


