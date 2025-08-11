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
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();
    private readonly HeatEffectsCalculator _sut;

    public HeatEffectsCalculatorTests()
    {
        _sut = new HeatEffectsCalculator(_rulesProvider, _diceRoller);

        // Default mock - can be overridden in specific tests
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(4);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnNull_WhenAvoidNumberIsZero()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(0);
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenAvoidNumberIs13()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(13);
        var mech = CreateTestMech();
        SetMechHeat(mech, 30);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(13);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnFailedShutdown_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6; // Any number between 4-10 would work here
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([2, 3]);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnSuccess_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6; // Any number between 4-10 would work here
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);

        // Setup dice roll that succeeds (rolls 6, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
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
