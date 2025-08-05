using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class ConsciousnessCalculatorTests
{
    private readonly IDiceRoller _diceRoller;
    private readonly ConsciousnessCalculator _sut;
    private readonly IPilot _pilot;

    public ConsciousnessCalculatorTests()
    {
        _diceRoller = Substitute.For<IDiceRoller>();
        _sut = new ConsciousnessCalculator(_diceRoller);
        _pilot = Substitute.For<IPilot>();
        
        // Setup default pilot state
        _pilot.Id.Returns(Guid.NewGuid());
        _pilot.IsConscious.Returns(true);
        _pilot.IsDead.Returns(false);
        _pilot.AssignedTo.Returns(Substitute.For<Sanet.MakaMek.Core.Models.Units.Unit>());
        _pilot.AssignedTo.Id.Returns(Guid.NewGuid());
    }

    [Fact]
    public void MakeConsciousnessRolls_WithNoPendingRolls_ReturnsEmpty()
    {
        // Arrange
        var pendingNumbers = new Queue<int>();
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void MakeConsciousnessRolls_WithUnconsciousPilot_ReturnsEmpty()
    {
        // Arrange
        _pilot.IsConscious.Returns(false);
        var pendingNumbers = new Queue<int>();
        pendingNumbers.Enqueue(5);
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void MakeConsciousnessRolls_WithDeadPilot_ReturnsEmpty()
    {
        // Arrange
        _pilot.IsDead.Returns(true);
        var pendingNumbers = new Queue<int>();
        pendingNumbers.Enqueue(5);
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void MakeConsciousnessRolls_WithSuccessfulRoll_ReturnsSuccessfulCommand()
    {
        // Arrange
        var pendingNumbers = new Queue<int>();
        pendingNumbers.Enqueue(5);
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        var diceResults = new List<DiceResult> { new(3), new(4) }; // Total 7, >= 5
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot).ToList();

        // Assert
        result.Count.ShouldBe(1);
        var command = result[0];
        command.ConsciousnessNumber.ShouldBe(5);
        command.DiceResults.ShouldBe(new List<int> { 3, 4 });
        command.IsSuccessful.ShouldBeTrue();
        command.IsRecoveryAttempt.ShouldBeFalse();
    }

    [Fact]
    public void MakeConsciousnessRolls_WithFailedRoll_ReturnsFailedCommand()
    {
        // Arrange
        var pendingNumbers = new Queue<int>();
        pendingNumbers.Enqueue(7);
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        var diceResults = new List<DiceResult> { new(2), new(3) }; // Total 5, < 7
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot).ToList();

        // Assert
        result.Count.ShouldBe(1);
        var command = result[0];
        command.ConsciousnessNumber.ShouldBe(7);
        command.DiceResults.ShouldBe(new List<int> { 2, 3 });
        command.IsSuccessful.ShouldBeFalse();
        command.IsRecoveryAttempt.ShouldBeFalse();
    }

    [Fact]
    public void MakeConsciousnessRolls_WithMultipleRolls_StopsAfterFailure()
    {
        // Arrange
        var pendingNumbers = new Queue<int>();
        pendingNumbers.Enqueue(3); // First roll
        pendingNumbers.Enqueue(5); // Second roll
        pendingNumbers.Enqueue(7); // Third roll (should not be processed)
        _pilot.PendingConsciousnessNumbers.Returns(pendingNumbers);

        // First roll succeeds, second roll fails
        _diceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(2), new(2) }, // Total 4, >= 3 (success)
            new List<DiceResult> { new(1), new(2) }  // Total 3, < 5 (failure)
        );

        // Act
        var result = _sut.MakeConsciousnessRolls(_pilot).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result[0].IsSuccessful.ShouldBeTrue();
        result[1].IsSuccessful.ShouldBeFalse();
        
        // Verify the queue was cleared after failure
        _pilot.PendingConsciousnessNumbers.Received(1).Clear();
    }

    [Fact]
    public void MakeRecoveryConsciousnessRoll_WithConsciousPilot_ReturnsNull()
    {
        // Arrange
        _pilot.IsConscious.Returns(true);

        // Act
        var result = _sut.MakeRecoveryConsciousnessRoll(_pilot);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void MakeRecoveryConsciousnessRoll_WithDeadPilot_ReturnsNull()
    {
        // Arrange
        _pilot.IsConscious.Returns(false);
        _pilot.IsDead.Returns(true);

        // Act
        var result = _sut.MakeRecoveryConsciousnessRoll(_pilot);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void MakeRecoveryConsciousnessRoll_WithUnconsciousPilot_ReturnsCommand()
    {
        // Arrange
        _pilot.IsConscious.Returns(false);
        _pilot.CurrentConsciousnessNumber.Returns(7);

        var diceResults = new List<DiceResult> { new(4), new(5) }; // Total 9, >= 7
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.MakeRecoveryConsciousnessRoll(_pilot);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ConsciousnessNumber.ShouldBe(7);
        result.Value.DiceResults.ShouldBe(new List<int> { 4, 5 });
        result.Value.IsSuccessful.ShouldBeTrue();
        result.Value.IsRecoveryAttempt.ShouldBeTrue();
    }

    [Fact]
    public void MakeRecoveryConsciousnessRoll_WithFailedRecovery_ReturnsFailedCommand()
    {
        // Arrange
        _pilot.IsConscious.Returns(false);
        _pilot.CurrentConsciousnessNumber.Returns(10);

        var diceResults = new List<DiceResult> { new(3), new(4) }; // Total 7, < 10
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.MakeRecoveryConsciousnessRoll(_pilot);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ConsciousnessNumber.ShouldBe(10);
        result.Value.DiceResults.ShouldBe(new List<int> { 3, 4 });
        result.Value.IsSuccessful.ShouldBeFalse();
        result.Value.IsRecoveryAttempt.ShouldBeTrue();
    }
}
