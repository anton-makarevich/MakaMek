using NSubstitute;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Presentation.UiStates;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.UiStates;

public class PhysicalAttackSelectionPolicyTests
{
    private readonly PhysicalAttackSelectionPolicy _sut = new();
    private readonly IUnit _candidate = Substitute.For<IUnit>();

    [Fact]
    public void Decide_WhenPlayerCannotAct_IgnoresTheRequest()
    {
        var result = Decide(PhysicalAttackStep.SelectingUnit, canAct: false, canActAsAttacker: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithNoCandidate_IgnoresTheRequest()
    {
        var result = _sut.Decide(
            PhysicalAttackStep.SelectingUnit,
            candidate: null,
            canAct: true,
            isFriendly: false,
            canActAsAttacker: false,
            isReachable: false,
            isCurrentTarget: false);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithDestroyedCandidate_IgnoresTheRequest()
    {
        _candidate.IsDestroyed.Returns(true);

        var result = Decide(PhysicalAttackStep.SelectingUnit, canActAsAttacker: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithAttackerReadyUnitWhileSelectingUnit_SelectsAttacker()
    {
        var result = Decide(PhysicalAttackStep.SelectingUnit, canActAsAttacker: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.SelectAttacker);
    }

    [Fact]
    public void Decide_WithUnitThatCannotAttack_IgnoresTheRequest()
    {
        var result = Decide(PhysicalAttackStep.SelectingUnit, canActAsAttacker: false);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithFriendlyUnitWhileSelectingTarget_SelectsAttacker()
    {
        var result = Decide(
            PhysicalAttackStep.TargetSelection,
            isFriendly: true,
            canActAsAttacker: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.SelectAttacker);
    }

    [Fact]
    public void Decide_WithReachableEnemyWhileSelectingTarget_OpensDeclaration()
    {
        var result = Decide(PhysicalAttackStep.TargetSelection, isReachable: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.OpenDeclaration);
    }

    [Fact]
    public void Decide_WithUnreachableEnemyWhileSelectingTarget_IgnoresTheRequest()
    {
        var result = Decide(PhysicalAttackStep.TargetSelection, isReachable: false);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithAnotherReachableEnemyDuringDeclaration_ReopensDeclaration()
    {
        var result = Decide(PhysicalAttackStep.Declaration, isReachable: true, isCurrentTarget: false);

        result.ShouldBe(PhysicalAttackSelectionOutcome.OpenDeclaration);
    }

    [Fact]
    public void Decide_WithCurrentTargetDuringDeclaration_IgnoresTheRequest()
    {
        var result = Decide(PhysicalAttackStep.Declaration, isReachable: true, isCurrentTarget: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.Ignore);
    }

    [Fact]
    public void Decide_WithFriendlyUnitDuringDeclaration_ClosesDeclaration()
    {
        var result = Decide(PhysicalAttackStep.Declaration, isFriendly: true, isReachable: true);

        result.ShouldBe(PhysicalAttackSelectionOutcome.CloseDeclaration);
    }

    [Fact]
    public void Decide_WithUnreachableEnemyDuringDeclaration_ClosesDeclaration()
    {
        var result = Decide(PhysicalAttackStep.Declaration, isReachable: false);

        result.ShouldBe(PhysicalAttackSelectionOutcome.CloseDeclaration);
    }

    private PhysicalAttackSelectionOutcome Decide(
        PhysicalAttackStep step,
        bool canAct = true,
        bool isFriendly = false,
        bool canActAsAttacker = false,
        bool isReachable = false,
        bool isCurrentTarget = false) =>
        _sut.Decide(
            step,
            _candidate,
            canAct,
            isFriendly,
            canActAsAttacker,
            isReachable,
            isCurrentTarget);
}
