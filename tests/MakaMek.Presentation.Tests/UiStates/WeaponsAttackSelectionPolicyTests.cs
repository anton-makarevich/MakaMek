using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class WeaponsAttackSelectionPolicyTests
{
    private static readonly List<HashSet<HexCoordinates>> NoRanges = [];

    private readonly WeaponsAttackSelectionPolicy _sut = new();
    private readonly IUnit _unit = Substitute.For<IUnit>();
    private readonly Guid _activePlayerId = Guid.NewGuid();

    public WeaponsAttackSelectionPolicyTests()
    {
        _unit.Owner.Returns(new Player(_activePlayerId, "Owner", PlayerControlType.Human));
    }

    [Fact]
    public void Decide_WhenPlayerCannotAct_IgnoresTheRequest()
    {
        var result = Decide(WeaponsAttackStep.SelectingUnit, _unit, canAct: false, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithNoCandidate_IgnoresTheRequest()
    {
        var result = Decide(WeaponsAttackStep.SelectingUnit, candidate: null, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithDestroyedCandidate_IgnoresTheRequest()
    {
        _unit.IsDestroyed.Returns(true);

        var result = Decide(WeaponsAttackStep.SelectingUnit, _unit, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithOwnLiveUnitWhileSelectingUnit_SelectsAttacker()
    {
        var result = Decide(WeaponsAttackStep.SelectingUnit, _unit, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.SelectAttacker);
    }

    [Fact]
    public void Decide_WithOwnUnitThatAlreadyDeclared_IgnoresTheRequest()
    {
        _unit.HasDeclaredWeaponAttack.Returns(true);

        var result = Decide(WeaponsAttackStep.ActionSelection, _unit, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithEnemyUnitWhileSelectingAttacker_IgnoresTheRequest()
    {
        var enemy = Substitute.For<IUnit>();
        enemy.Owner.Returns(new Core.Models.Game.Players.Player(Guid.NewGuid(), "Enemy", Core.Models.Game.Players.PlayerControlType.Human));

        var result = Decide(WeaponsAttackStep.SelectingUnit, enemy, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithEnemyInsideWeaponRange_SelectsTarget()
    {
        var enemy = CreateEnemyInRange(out var coordinates);

        var result = Decide(
            WeaponsAttackStep.TargetSelection,
            enemy,
            canAct: true,
            [[coordinates]]);

        result.ShouldBe(WeaponsAttackSelectionOutcome.SelectTarget);
    }

    [Fact]
    public void Decide_WithEnemyOutsideWeaponRange_IgnoresTheRequest()
    {
        var enemy = CreateEnemyInRange(out _);

        var result = Decide(WeaponsAttackStep.TargetSelection, enemy, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithOwnUnitWhileSelectingTarget_IgnoresTheRequest()
    {
        var result = Decide(WeaponsAttackStep.TargetSelection, _unit, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_DuringWeaponConfiguration_OnlyNotifies()
    {
        var result = Decide(WeaponsAttackStep.WeaponsConfiguration, _unit, canAct: true, NoRanges);

        result.ShouldBe(WeaponsAttackSelectionOutcome.NotifyOnly);
    }

    private IUnit CreateEnemyInRange(out HexCoordinates coordinates)
    {
        coordinates = new HexCoordinates(1, 1);
        var enemy = Substitute.For<IUnit>();
        enemy.Owner.Returns(new Player(Guid.NewGuid(), "Enemy", PlayerControlType.Human));
        enemy.Position.Returns(new HexPosition(coordinates, HexDirection.Top));
        return enemy;
    }

    private WeaponsAttackSelectionOutcome Decide(
        WeaponsAttackStep step,
        IUnit? candidate,
        bool canAct,
        IEnumerable<HashSet<HexCoordinates>> ranges) =>
        _sut.Decide(step, candidate, canAct, _activePlayerId, ranges);
}
