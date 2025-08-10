using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class HeatEffectsCalculatorTests
{
    private readonly IDiceRoller _diceRoller;
    private readonly HeatEffectsCalculator _sut;

    public HeatEffectsCalculatorTests()
    {
        var rulesProvider = Substitute.For<IRulesProvider>();
        _diceRoller = Substitute.For<IDiceRoller>();
        _sut = new HeatEffectsCalculator(rulesProvider, _diceRoller);

        // Setup default rules provider behavior
        rulesProvider.GetHeatShutdownThresholds().Returns([14, 18, 22, 26, 30]);
        rulesProvider.GetHeatShutdownAvoidNumber(14).Returns(4);
        rulesProvider.GetHeatShutdownAvoidNumber(18).Returns(6);
        rulesProvider.GetHeatShutdownAvoidNumber(22).Returns(8);
        rulesProvider.GetHeatShutdownAvoidNumber(26).Returns(10);
        rulesProvider.GetHeatShutdownAvoidNumber(Arg.Is<int>(x => x >= 30)).Returns((int?)null);

    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnNull_WhenNoThresholdCrossed()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenHeat30OrAbove()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 30);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        result.Value.AvoidShutdownRoll.HeatLevel.ShouldBe(30);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldPerformRoll_WhenThresholdCrossedAndPilotConscious()
    {
        // Arrange
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 18);

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new DiceResult(2), new DiceResult(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([2, 3]);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(6);
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnSuccessfulCommand_WhenRollSucceeds()
    {
        // Arrange
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 18);

        // Setup dice roll that succeeds (rolls 6, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeTrue();
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
    }

    [Fact]
    public void ShouldAutoRestart_ShouldReturnTrue_WhenHeatBelowThreshold()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.ShouldAutoRestart(mech);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ShouldAutoRestart_ShouldReturnFalse_WhenHeatAboveThreshold()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.ShouldAutoRestart(mech);

        // Assert
        result.ShouldBeFalse();
    }

    private static Mech CreateTestMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return new MechFactory(new ClassicBattletechRulesProvider(), Substitute.For<ILocalizationService>()).Create(mechData);
    }

    private static void SetMechHeat(Mech mech, int heatLevel)
    {
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = heatLevel
                }
            ],
            DissipationData = default
        };
        mech.ApplyHeat(heatData);
    }
}
