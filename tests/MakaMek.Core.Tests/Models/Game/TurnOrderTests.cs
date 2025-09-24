using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class TurnOrderTests
{
    private readonly TurnOrder _sut;
    private readonly IPlayer _player1;
    private readonly IPlayer _player2;
    private readonly IPlayer _player3;
    private readonly UnitData _unitData = MechFactoryTests.CreateDummyMechData();
    private readonly MechFactory _mechFactory = new(
        new ClassicBattletechRulesProvider(),
        new ClassicBattletechComponentProvider(),
        new FakeLocalizationService());
    
    public TurnOrderTests()
    {
        _sut = new TurnOrder();
        _player1 = Substitute.For<IPlayer>();
        _player2 = Substitute.For<IPlayer>();
        _player3 = Substitute.For<IPlayer>();

        // Setup unit counts
        
        _player1.AliveUnits.Returns([_mechFactory.Create(_unitData), _mechFactory.Create(_unitData)]); // 2 units
        _player2.AliveUnits.Returns([_mechFactory.Create(_unitData), _mechFactory.Create(_unitData), _mechFactory.Create(_unitData)]); // 3 units
        _player3.AliveUnits.Returns([_mechFactory.Create(_unitData), _mechFactory.Create(_unitData), _mechFactory.Create(_unitData)]); // 3 units
    }

    [Fact]
    public void CalculateOrder_WithUnequalUnits_ShouldHandleDoubleMovements()
    {
        // Arrange - Player2 won, Player1 second, Player3 lost
        var initiativeOrder = new List<IPlayer> { _player2, _player1, _player3 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Following the example from requirements
        var steps = _sut.Steps;
        steps.Count.ShouldBe(6);
        
        // 1. Player 3 moves one unit (2 remains)
        steps[0].ShouldBe(new TurnStep(_player3, 1));
        
        // 2. Player 1 moves one unit (1 remains)
        steps[1].ShouldBe(new TurnStep(_player1, 1));
        
        // 3. Player 2 moves one unit (2 remains)
        steps[2].ShouldBe(new TurnStep(_player2, 1));
        
        // 4. Player 3 moves 2 units (has twice as many as Player 1)
        steps[3].ShouldBe(new TurnStep(_player3, 2));
        
        // 5. Player 1 moves last unit
        steps[4].ShouldBe(new TurnStep(_player1, 1));
        
        // 6. Player 2 moves 2 units
        steps[5].ShouldBe(new TurnStep(_player2, 2));
    }

    [Fact]
    public void CalculateOrder_WithEqualUnits_ShouldMoveOneByOne()
    {
        // Arrange
        _player1.AliveUnits.Returns([_mechFactory.Create(_unitData), _mechFactory.Create(_unitData)]); // 2 units
        _player2.AliveUnits.Returns([_mechFactory.Create(_unitData), _mechFactory.Create(_unitData)]); // 2 units
        var initiativeOrder = new List<IPlayer> { _player2, _player1 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert
        var steps = _sut.Steps;
        steps.Count.ShouldBe(4);
        
        // Loser moves first, one unit at a time
        steps[0].ShouldBe(new TurnStep(_player1, 1));
        steps[1].ShouldBe(new TurnStep(_player2, 1));
        steps[2].ShouldBe(new TurnStep(_player1, 1));
        steps[3].ShouldBe(new TurnStep(_player2, 1));
    }

    [Fact]
    public void GetNextStep_ShouldTrackProgress()
    {
        // Arrange
        var initiativeOrder = new List<IPlayer> { _player1 };
        _sut.CalculateOrder(initiativeOrder);

        // Act & Assert
        _sut.CurrentStep.ShouldBeNull();
        
        var step1 = _sut.GetNextStep();
        step1.ShouldNotBeNull();
        _sut.CurrentStep.ShouldBe(step1);
        _sut.HasNextStep.ShouldBeFalse();

        var step2 = _sut.GetNextStep();
        step2.ShouldBeNull();
        _sut.CurrentStep.ShouldBeNull();
    }

    [Fact]
    public void Reset_ShouldClearCurrentStep()
    {
        // Arrange
        var initiativeOrder = new List<IPlayer> { _player1, _player2 };
        _sut.CalculateOrder(initiativeOrder);
        _sut.GetNextStep(); // Advance to first step

        // Act
        _sut.Reset();

        // Assert
        _sut.CurrentStep.ShouldBeNull();
        var nextStep = _sut.GetNextStep();
        nextStep.ShouldBe(_sut.Steps[0]);
    }

    [Fact]
    public void CalculateOrder_With3To9UnitRatio_ShouldHandleTripleMovements()
    {
        // Arrange - Player 1 has 3 units, Player 2 has 9 units, Player 1 wins initiative
        var player1Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };
        var player2Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };

        _player1.AliveUnits.Returns(player1Units);
        _player2.AliveUnits.Returns(player2Units);

        // Player 1 wins initiative (first in the list)
        var initiativeOrder = new List<IPlayer> { _player1, _player2 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Expected movement sequence based on BattleTech rules
        var steps = _sut.Steps;
        steps.Count.ShouldBe(6);

        // Turn 1: Player 2 has 9 units, Player 1 has 3 units (Player 2 has 3x as many units)
        // Player 2 moves three units (Units 1, 2, 3). Remaining: Player 2 (6), Player 1 (3)
        steps[0].ShouldBe(new TurnStep(_player2, 3));

        // Player 1 moves one unit (Unit 1). Remaining: Player 2 (6), Player 1 (2)
        steps[1].ShouldBe(new TurnStep(_player1, 1));

        // Player 2 moves three units (Units 4, 5, 6). Remaining: Player 2 (3), Player 1 (2)
        steps[2].ShouldBe(new TurnStep(_player2, 3));

        // Player 1 moves one unit (Unit 2). Remaining: Player 2 (3), Player 1 (1)
        steps[3].ShouldBe(new TurnStep(_player1, 1));

        // Player 2 moves three units (Units 7, 8, 9). Remaining: Player 2 (0), Player 1 (1)
        steps[4].ShouldBe(new TurnStep(_player2, 3));

        // Player 1 moves one unit (Unit 3). Remaining: Player 1 (0)
        steps[5].ShouldBe(new TurnStep(_player1, 1));
    }

    [Fact]
    public void CalculateOrder_With1To4UnitRatio_ShouldHandleQuadrupleMovements()
    {
        // Arrange - Player 1 has 1 unit, Player 2 has 4 units, Player 1 wins initiative
        var player1Units = new List<Unit> { _mechFactory.Create(_unitData) };
        var player2Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };

        _player1.AliveUnits.Returns(player1Units);
        _player2.AliveUnits.Returns(player2Units);

        // Player 1 wins initiative (first in the list)
        var initiativeOrder = new List<IPlayer> { _player1, _player2 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Expected movement sequence based on BattleTech rules
        var steps = _sut.Steps;
        steps.Count.ShouldBe(2);

        // Player 2 has 4x as many units as Player 1, so moves 4 units
        steps[0].ShouldBe(new TurnStep(_player2, 4));

        // Player 1 moves 1 unit
        steps[1].ShouldBe(new TurnStep(_player1, 1));
    }

    [Fact]
    public void CalculateOrder_With2To5UnitRatio_ShouldHandlePartialRatios()
    {
        // Arrange - Player 1 has 2 units, Player 2 has 5 units, Player 1 wins initiative
        var player1Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };
        var player2Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };

        _player1.AliveUnits.Returns(player1Units);
        _player2.AliveUnits.Returns(player2Units);

        // Player 1 wins initiative (first in the list)
        var initiativeOrder = new List<IPlayer> { _player1, _player2 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Expected movement sequence based on BattleTech rules
        var steps = _sut.Steps;
        steps.Count.ShouldBe(4);

        // Initial: Player1=2, Player2=5. Min=2. Player2 moves 5/2=2 units, Player1 moves 2/2=1 unit
        steps[0].ShouldBe(new TurnStep(_player2, 2)); // Player2: 5/2=2 units
        steps[1].ShouldBe(new TurnStep(_player1, 1)); // Player1: 2/2=1 unit

        // After round 1: Player1=1, Player2=3. Min=1. Player2 moves 3/1=3 units, Player1 moves 1/1=1 unit
        steps[2].ShouldBe(new TurnStep(_player2, 3)); // Player2: 3/1=3 units
        steps[3].ShouldBe(new TurnStep(_player1, 1)); // Player1: 1/1=1 unit

        // After round 2: Player1=0, Player2=0. Done.
    }

    [Fact]
    public void CalculateOrder_WithThreePlayersUnequalUnits_ShouldHandleMultipleRatios()
    {
        // Arrange - Player 1 has 1 unit, Player 2 has 2 units, Player 3 has 6 units
        var player1Units = new List<Unit> { _mechFactory.Create(_unitData) };
        var player2Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };
        var player3Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };

        _player1.AliveUnits.Returns(player1Units);
        _player2.AliveUnits.Returns(player2Units);
        _player3.AliveUnits.Returns(player3Units);

        // Player 1 wins initiative, Player 2 second, Player 3 last
        var initiativeOrder = new List<IPlayer> { _player1, _player2, _player3 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Expected movement sequence based on BattleTech rules
        var steps = _sut.Steps;
        steps.Count.ShouldBe(3);

        // Initial: Player1=1, Player2=2, Player3=6. Min=1
        // Player 3 moves 6/1=6 units, Player 2 moves 2/1=2 units, Player 1 moves 1/1=1 unit
        steps[0].ShouldBe(new TurnStep(_player3, 6)); // Player 3 moves all 6 units
        steps[1].ShouldBe(new TurnStep(_player2, 2)); // Player 2 moves all 2 units
        steps[2].ShouldBe(new TurnStep(_player1, 1)); // Player 1 moves 1 unit

        // All players have moved all their units in one round
    }

    [Fact]
    public void CalculateOrder_WithZeroUnitsPlayer_ShouldSkipPlayer()
    {
        // Arrange - Player 1 has 0 units, Player 2 has 3 units
        var player2Units = new List<Unit>
        {
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData),
            _mechFactory.Create(_unitData)
        };

        _player1.AliveUnits.Returns([]);
        _player2.AliveUnits.Returns(player2Units);

        var initiativeOrder = new List<IPlayer> { _player1, _player2 };

        // Act
        _sut.CalculateOrder(initiativeOrder);

        // Assert - Player 1 should be skipped entirely, Player 2 moves one unit at a time
        var steps = _sut.Steps;
        steps.Count.ShouldBe(3);

        // Player 2 moves 1 unit at a time since there's no other player to compare against
        steps[0].ShouldBe(new TurnStep(_player2, 1));
        steps[1].ShouldBe(new TurnStep(_player2, 1));
        steps[2].ShouldBe(new TurnStep(_player2, 1));
    }
}
