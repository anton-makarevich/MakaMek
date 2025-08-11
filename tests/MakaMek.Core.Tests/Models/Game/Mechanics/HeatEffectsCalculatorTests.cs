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

        // Setup default rules provider behavior for GetHeatShutdownAvoidNumber
        rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(x =>
        {
            var heat = x.ArgAt<int>(0);
            return heat switch
            {
                < 14 => 0,    // No shutdown check needed
                < 18 => 4,    // Avoid on 4+
                < 22 => 6,    // Avoid on 6+
                < 26 => 8,    // Avoid on 8+
                < 30 => 10,   // Avoid on 10+
                _ => 13       // Automatic shutdown
            };
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(13)]
    public void CheckForHeatShutdown_ShouldReturnNull_WhenHeatBelowShutdownThreshold(int heatLevel)
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, heatLevel);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(30)]
    [InlineData(35)]
    [InlineData(100)]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenHeat30OrAbove(int heatLevel)
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, heatLevel);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        result.Value.AvoidShutdownRoll.HeatLevel.ShouldBe(heatLevel);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(13);
    }

    [Theory]
    [InlineData(14, 4)]  // 14-17 heat: avoid on 4+
    [InlineData(16, 4)]
    [InlineData(18, 6)]  // 18-21 heat: avoid on 6+
    [InlineData(20, 6)]
    [InlineData(22, 8)]  // 22-25 heat: avoid on 8+
    [InlineData(24, 8)]
    [InlineData(26, 10)] // 26-29 heat: avoid on 10+
    [InlineData(28, 10)]
    public void CheckForHeatShutdown_ShouldPerformRoll_WhenThresholdCrossedAndPilotConscious(int heatLevel, int expectedAvoidNumber)
    {
        // Arrange
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, heatLevel);

        // Setup dice roll that fails (rolls 1 less than needed)
        var diceResults = new List<DiceResult> { new(1), new(expectedAvoidNumber - 2) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([1, expectedAvoidNumber - 2]);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(expectedAvoidNumber);
        result.Value.AvoidShutdownRoll.HeatLevel.ShouldBe(heatLevel);
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
    }

    [Theory]
    [InlineData(14, 4, 2, 2)]  // 2+2=4, needs 4+ to avoid
    [InlineData(18, 6, 3, 4)]  // 3+4=7, needs 6+ to avoid
    [InlineData(22, 8, 4, 4)]  // 4+4=8, needs 8+ to avoid
    [InlineData(26, 10, 5, 5)] // 5+5=10, needs 10+ to avoid
    public void CheckForHeatShutdown_ShouldReturnSuccessfulCommand_WhenRollSucceeds(int heatLevel, int expectedAvoidNumber, int die1, int die2)
    {
        // Arrange
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, heatLevel);

        // Setup dice roll that succeeds
        var diceResults = new List<DiceResult> { new(die1), new(die2) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(expectedAvoidNumber);
        result.Value.AvoidShutdownRoll.HeatLevel.ShouldBe(heatLevel);
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(13)]
    public void ShouldAutoRestart_ShouldReturnTrue_WhenHeatBelowThreshold(int heatLevel)
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, heatLevel);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.ShouldAutoRestart(mech);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(14)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(100)]
    public void ShouldAutoRestart_ShouldReturnFalse_WhenHeatAtOrAboveThreshold(int heatLevel)
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, heatLevel);
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
