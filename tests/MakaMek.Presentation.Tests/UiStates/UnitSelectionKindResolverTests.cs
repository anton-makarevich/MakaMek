using NSubstitute;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class UnitSelectionKindResolverTests
{
    private readonly IUnit _unit = Substitute.For<IUnit>();
    private readonly IUnit _otherUnit = Substitute.For<IUnit>();

    [Fact]
    public void Resolve_WithNoSelection_ReturnsNone()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, selected: null, attacker: null, target: null);

        result.ShouldBe(UnitSelectionKind.None);
    }

    [Fact]
    public void Resolve_WithSelectedUnit_ReturnsSelected()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, _unit, attacker: null, target: null);

        result.ShouldBe(UnitSelectionKind.Selected);
    }

    [Fact]
    public void Resolve_WithAnotherSelectedUnit_ReturnsNone()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, _otherUnit, attacker: null, target: null);

        result.ShouldBe(UnitSelectionKind.None);
    }

    [Fact]
    public void Resolve_WithAttacker_ReturnsAttacker()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, selected: null, attacker: _unit, target: null);

        result.ShouldBe(UnitSelectionKind.Attacker);
    }

    [Fact]
    public void Resolve_WithAttackerAndSelectedUnit_ReturnsAttacker()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, _unit, attacker: _unit, target: null);

        result.ShouldBe(UnitSelectionKind.Attacker);
    }

    [Fact]
    public void Resolve_WithTarget_ReturnsTarget()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, selected: null, attacker: null, target: _unit);

        result.ShouldBe(UnitSelectionKind.Target);
    }

    [Fact]
    public void Resolve_WithTargetAndSelectedUnit_ReturnsTarget()
    {
        var result = UnitSelectionKindResolver.Resolve(_unit, _unit, attacker: null, target: _unit);

        result.ShouldBe(UnitSelectionKind.Target);
    }

    [Fact]
    public void Resolve_WithDestroyedUnit_ReturnsNone()
    {
        _unit.IsOutOfCommission.Returns(true);

        var result = UnitSelectionKindResolver.Resolve(_unit, _unit, attacker: _unit, target: _unit);

        result.ShouldBe(UnitSelectionKind.None);
    }

    [Fact]
    public void Resolve_WithCombatState_ReturnsTheRoleOfTheUnit()
    {
        var state = new CombatStateStub
        {
            Attacker = _unit,
            SelectedTarget = _otherUnit
        };

        UnitSelectionKindResolver.Resolve(_unit, _unit, state).ShouldBe(UnitSelectionKind.Attacker);
        UnitSelectionKindResolver.Resolve(_otherUnit, _otherUnit, state).ShouldBe(UnitSelectionKind.Target);
    }

    [Fact]
    public void Resolve_WithNonCombatState_FallsBackToTheSelection()
    {
        var state = Substitute.For<IUiState>();

        UnitSelectionKindResolver.Resolve(_unit, _unit, state).ShouldBe(UnitSelectionKind.Selected);
        UnitSelectionKindResolver.Resolve(_unit, null, state).ShouldBe(UnitSelectionKind.None);
    }

    /// <summary>
    /// A UI state that puts units into combat roles.
    /// </summary>
    private sealed class CombatStateStub : IUiState, ICombatSelectionRoles
    {
        public string ActionLabel => string.Empty;

        public bool IsActionRequired => false;

        public IUnit? Attacker { get; init; }

        public IUnit? SelectedTarget { get; init; }

        public void HandleUnitSelectionFromList(IUnit? unit) { }

        public void HandleHexSelection(Hex hex) { }

        public void HandleFacingSelection(HexDirection direction) { }
    }
}
