using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using Shouldly;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using ArchModel = ArchUnitNET.Domain.Architecture;

namespace Sanet.MakaMek.Architecture.Tests;

/// <summary>
/// Keeps the Map module at the bottom of the dependency graph and stops the
/// <see cref="Sanet.MakaMek.Map.Models.BattleMap"/> facade from re-acquiring the coupling
/// that was extracted into the pathfinding/LOS collaborators.
/// </summary>
public class MapArchitectureTests
{
    private static readonly ArchModel Architecture = ArchitectureSetup.Architecture;

    private static readonly IObjectProvider<IType> BotsLayer =
        Types().That().ResideInAssembly(ArchitectureSetup.BotsAssembly).As("Bots");

    private static readonly IObjectProvider<IType> MapGenerators =
        Types().That()
            .ResideInNamespaceMatching("^Sanet\\.MakaMek\\.Map\\.Generators($|\\.)")
            .As("map generators");

    [Fact]
    public void Map_ShouldNotDependOnCorePresentationBotsOrAvalonia()
    {
        IArchRule coreOrUi = Types().That()
            .Are(ArchitectureSetup.MapLayer)
            .Should().NotDependOnAny(ArchitectureSetup.CoreLayer)
            .AndShould().NotDependOnAny(ArchitectureSetup.PresentationLayer)
            .AndShould().NotDependOnAny(ArchitectureSetup.UiLayer)
            .Because("Map is a leaf spatial module: it must not reach up into Core, Presentation or any UI assembly");

        IArchRule bots = Types().That()
            .Are(ArchitectureSetup.MapLayer)
            .Should().NotDependOnAny(BotsLayer)
            .Because("Map is a leaf spatial module: it must not depend on the Bots layer");

        coreOrUi.And(bots).Check(Architecture);
    }

    [Fact]
    public void BattleMap_ShouldNotDependOnMapGenerators()
    {
        IObjectProvider<IType> battleMap = Classes().That()
            .ResideInNamespaceMatching("^Sanet\\.MakaMek\\.Map\\.Models$")
            .And().HaveName("BattleMap")
            .As("BattleMap facade");

        // Guard the guard: a renamed/removed type must not make the rule pass vacuously.
        battleMap.GetObjects(Architecture).ShouldNotBeEmpty();

        IArchRule rule = Types().That()
            .Are(battleMap)
            .Should().NotDependOnAny(MapGenerators)
            .Because("BattleMap is a hex-store facade: procedural map generation must stay outside it " +
                     "so the facade keeps its CA1506 coupling budget");

        rule.Check(Architecture);
    }
}
