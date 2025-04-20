using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class TurnOrderTests
{
    private readonly TurnOrder _sut;
    private readonly IPlayer _player1;
    private readonly IPlayer _player2;
    private readonly IPlayer _player3;
    private readonly UnitData _unitData = MechFactoryTests.CreateDummyMechData();
    private readonly MechFactory _mechFactory = new(new ClassicBattletechRulesProvider(), 
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
}
