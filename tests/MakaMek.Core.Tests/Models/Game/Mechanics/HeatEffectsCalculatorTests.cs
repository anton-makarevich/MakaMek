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
    public void CheckForHeatShutdown_ShouldReturnNull_WhenMechIsAlreadyShutdown()
    {
        // Arrange
        var mech = CreateTestMech();
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
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
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        _diceRoller.DidNotReceive().Roll2D6();
    }
    
    [Fact]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenPilotIsUnconscious()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(6);
        var mech = CreateTestMech();
        SetMechHeat(mech, 20);
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        mech.AssignPilot(pilot);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
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
        result.Value.ShutdownData.Turn.ShouldBe(1);
        _rulesProvider.Received(1).GetHeatShutdownAvoidNumber(mech.CurrentHeat);
        _diceRoller.Received(1).Roll2D6();
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
        result.Value.ShutdownData.Turn.ShouldBe(1);
        _rulesProvider.Received(1).GetHeatShutdownAvoidNumber(mech.CurrentHeat);
        _diceRoller.Received(1).Roll2D6();
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

    [Fact]
    public void AttemptRestart_ShouldReturnNull_WhenMechIsNotShutdown()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        // mech is not shut down

        // Act
        var result = _sut.AttemptRestart(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnNull_WhenShutdownInSameTurn()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act - Try to restart in the same turn
        var result = _sut.AttemptRestart(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnAutoRestart_WhenAvoidNumberIsZero()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(0);
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act - Try to restart in a later turn
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeTrue();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnRestartImpossible_WhenAvoidNumberIs13()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(13);
        var mech = CreateTestMech();
        SetMechHeat(mech, 30);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnRestartImpossible_WhenPilotIsUnconscious()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(6);
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnSuccessfulRestart_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Setup dice roll that succeeds (rolls 6, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([3, 3]);
    }

    [Fact]
    public void AttemptRestart_ShouldReturnFailedRestart_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([2, 3]);
    }
}
