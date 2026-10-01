using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class PhysicalToHitCalculatorTests
{
    private readonly IRulesProvider _rules = Substitute.For<IRulesProvider>();
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly PhysicalToHitCalculator _sut;

    public PhysicalToHitCalculatorTests()
    {
        _rules.GetAttackerMovementModifier(Arg.Any<MovementType>()).Returns(0);
        _map.GetLineOfSight(
                Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>(),
                Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<HexSurface>(), Arg.Any<HexSurface>())
            .Returns(Los(hasLineOfSight: true));
        _sut = new PhysicalToHitCalculator(_rules);
    }

    private static LineOfSightResult Los(bool hasLineOfSight) => new()
    {
        From = new HexCoordinates(1, 1),
        To = new HexCoordinates(1, 2),
        AttackerLosLevel = 2,
        TargetLosLevel = 2,
        TotalInterveningFactor = 0,
        HasLineOfSight = hasLineOfSight,
        HexPath = []
    };

    private PhysicalAttackScenario Scenario(
        PhysicalAttackType attackType = PhysicalAttackType.Punch,
        int piloting = 4,
        MovementType movement = MovementType.Walk,
        IReadOnlyList<RollModifier>? attackerModifiers = null) =>
        PhysicalAttackScenario.ForPreview(
            piloting,
            attackType,
            new HexPosition(new HexCoordinates(1, 1), HexDirection.Top),
            new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom),
            movement,
            attackerModifiers);

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Preview_TakesItsBaseFromPilotingSkill_NotGunnery(int piloting)
    {
        var breakdown = _sut.Preview(Scenario(piloting: piloting), _map);

        breakdown.PilotingBase.Value.ShouldBe(piloting);
        breakdown.AllModifiers.ShouldNotContain(m => m is GunneryRollModifier);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch, 0)]
    [InlineData(PhysicalAttackType.Kick, -2)]
    public void Preview_AppliesThePerTypeBase(PhysicalAttackType attackType, int expected)
    {
        var breakdown = _sut.Preview(Scenario(attackType), _map);

        breakdown.AttackTypeBase.Value.ShouldBe(expected);
        breakdown.AttackTypeBase.AttackType.ShouldBe(attackType);
    }

    [Fact]
    public void Preview_TotalsThePilotingBaseAndTheTypeBase()
    {
        // Piloting 4, kick -2
        _sut.Preview(Scenario(PhysicalAttackType.Kick, piloting: 4), _map).Total.ShouldBe(2);
    }

    [Fact]
    public void Preview_ComposesTheAttackerMovementModifier_FromTheRulesProvider()
    {
        _rules.GetAttackerMovementModifier(MovementType.Run).Returns(2);

        var breakdown = _sut.Preview(Scenario(movement: MovementType.Run), _map);

        breakdown.AttackerMovement.Value.ShouldBe(2);
        breakdown.AttackerMovement.MovementType.ShouldBe(MovementType.Run);
        breakdown.Total.ShouldBe(6); // piloting 4 + punch 0 + movement 2
    }

    /// <summary>
    /// Heat and sensor damage never modify a physical attack. The scenario drops them, so there is
    /// no route by which the calculator could add them back, and the breakdown has no field for
    /// them either. Both halves are checked here.
    /// </summary>
    [Fact]
    public void Preview_DropsHeatAndSensorModifiers_EvenWhenTheUnitReportsThem()
    {
        var reported = new List<RollModifier>
        {
            new HeatRollModifier { Value = 3, HeatLevel = 17 },
            SensorHitModifier.Create(1)!,
            new ProneAttackerModifier { Value = ProneAttackerModifier.DefaultValue }
        };

        var breakdown = _sut.Preview(Scenario(attackerModifiers: reported), _map);

        breakdown.AllModifiers.ShouldNotContain(m => m is HeatRollModifier);
        breakdown.AllModifiers.ShouldNotContain(m => m is SensorHitModifier);
        breakdown.AllModifiers.ShouldContain(m => m is ProneAttackerModifier,
            "everything that is not heat or sensors still applies");
    }

    [Fact]
    public void Preview_KeepsTheProneAndSkiddingModifiersInTheTotal()
    {
        var reported = new List<RollModifier>
        {
            new ProneAttackerModifier { Value = 2 }
        };

        _sut.Preview(Scenario(attackerModifiers: reported), _map).Total
            .ShouldBe(6); // piloting 4 + punch 0 + prone 2
    }

    [Fact]
    public void Preview_ReportsAnImpossibleRoll_WithoutLineOfSight()
    {
        _map.GetLineOfSight(
                Arg.Any<HexCoordinates>(), Arg.Any<HexCoordinates>(),
                Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<HexSurface>(), Arg.Any<HexSurface>())
            .Returns(Los(hasLineOfSight: false));

        _sut.Preview(Scenario(), _map).Total.ShouldBe(PhysicalToHitBreakdown.ImpossibleRoll);
    }

    /// <summary>
    /// The preview exists so the UI can show a number before the attack is declared, so calling it
    /// must not change anything. Same scenario twice has to give the same answer, and the scenario
    /// itself must come back untouched.
    /// </summary>
    [Fact]
    public void Preview_ChangesNothing()
    {
        var scenario = Scenario(PhysicalAttackType.Kick);
        var before = scenario with { };

        var first = _sut.Preview(scenario, _map);
        var second = _sut.Preview(scenario, _map);

        first.Total.ShouldBe(second.Total);
        scenario.ShouldBe(before);
    }

    [Fact]
    public void AllModifiers_ListsEveryContribution()
    {
        var breakdown = _sut.Preview(
            Scenario(attackerModifiers: [new ProneAttackerModifier { Value = 2 }]), _map);

        breakdown.AllModifiers.ShouldContain(m => m is PilotingRollModifier);
        breakdown.AllModifiers.ShouldContain(m => m is PhysicalAttackBaseModifier);
        breakdown.AllModifiers.ShouldContain(m => m is AttackerMovementModifier);
        breakdown.AllModifiers.ShouldContain(m => m is ProneAttackerModifier);
    }

    [Fact]
    public void PhysicalAttackBaseModifier_RejectsATypeWithNoDefinedBase()
        => Should.Throw<ArgumentOutOfRangeException>(
            () => PhysicalAttackBaseModifier.BaseFor((PhysicalAttackType)999));
}
