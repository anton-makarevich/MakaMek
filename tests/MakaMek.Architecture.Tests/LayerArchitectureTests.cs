using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MVVM.Core.ViewModels;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace Sanet.MakaMek.Architecture.Tests;

/// <summary>
/// Deterministic architecture rules for the Avalonia → Presentation → Core layering.
/// See docs/architecture and AGENTS.md for the intended dependency direction.
/// Namespace-level dependency-cycle detection is intentionally out of scope:
/// the current namespace graph has pre-existing cycles that need a separate refactor.
/// </summary>
public class LayerArchitectureTests
{
    private static readonly ArchModel Architecture = ArchitectureSetup.Architecture;

    [Fact]
    public void DomainModules_ShouldNotDependOnUiLayer()
    {
        IArchRule rule = Types().That()
            .Are(ArchitectureSetup.CoreLayer)
            .Or().Are(ArchitectureSetup.MapLayer)
            .Or().Are(ArchitectureSetup.ServicesLayer)
            .Or().Are(ArchitectureSetup.LocalizationLayer)
            .Should().NotDependOnAny(ArchitectureSetup.UiLayer)
            .Because("Core, Map, Services and Localization are UI-agnostic layers");

        rule.Check(Architecture);
    }

    [Fact]
    public void Presentation_ShouldNotDependOnAvalonia()
    {
        IArchRule rule = Types().That()
            .Are(ArchitectureSetup.PresentationLayer)
            .Should().NotDependOnAny(ArchitectureSetup.AvaloniaLayer)
            .Because("presentation logic must stay UI-framework agnostic and unit-testable");

        rule.Check(Architecture);
    }

    [Fact]
    public void DependencyDirection_IsOneWay_AvaloniaToPresentationToCore()
    {
        IArchRule coreToPresentation = Types().That()
            .Are(ArchitectureSetup.CoreLayer)
            .Should().NotDependOnAny(ArchitectureSetup.PresentationLayer)
            .Because("Core must not know about Presentation");

        IArchRule presentationToAvalonia = Types().That()
            .Are(ArchitectureSetup.PresentationLayer)
            .Should().NotDependOnAny(ArchitectureSetup.AvaloniaLayer)
            .Because("Presentation must not know about Avalonia");

        coreToPresentation.And(presentationToAvalonia).Check(Architecture);
    }

    [Fact]
    public void UiStateImplementations_ShouldResideInPresentationUiStatesNamespace()
    {
        IArchRule rule = Types().That()
            .ImplementInterface(typeof(IUiState))
            .Should().ResideInNamespaceMatching("^Sanet.MakaMek.Presentation.UiStates($|\\.)")
            .Because("per-phase interaction logic is owned by Presentation.UiStates");

        rule.Check(Architecture);
    }

    [Fact]
    public void ViewModels_ShouldDeriveFromSanetMvvmBaseType()
    {
        // ViewModels.Wrappers contains read-only presentation DTOs (not observable VMs),
        // so the rule applies to the primary ViewModels namespace only.
        IArchRule rule = Classes().That()
            .ResideInNamespaceMatching("^Sanet.MakaMek.Presentation.ViewModels$")
            .And().HaveNameEndingWith("ViewModel")
            .Should().BeAssignableTo(typeof(BindableBase))
            .Because("presentation uses the Sanet.MVVM framework");

        rule.Check(Architecture);
    }

    [Fact]
    public void GameCommandImplementations_ShouldResideInCoreCommandsNamespace()
    {
        IArchRule rule = Types().That()
            .ImplementInterface(typeof(IGameCommand))
            .Should().ResideInNamespaceMatching("^Sanet.MakaMek.Core.Data.Game.Commands($|\\.)")
            .Because("commands are the only state-change path and live in Core");

        rule.Check(Architecture);
    }

    [Fact]
    public void MechanicsImplementations_ShouldResideInCoreMechanicsNamespace()
    {
        IObjectProvider<Interface> mechanicsInterfaces = Interfaces().That()
            .ResideInNamespaceMatching("^Sanet.MakaMek.Core.Models.Game.Mechanics($|\\.)")
            .As("mechanics interfaces");

        IArchRule rule = Types().That()
            .AreAssignableTo(mechanicsInterfaces)
            .Should().ResideInNamespaceMatching("^Sanet.MakaMek.Core.Models.Game.Mechanics($|\\.)")
            .Because("mechanics implementations belong next to their contracts");

        rule.Check(Architecture);
    }
}
