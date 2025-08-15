using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client.Builders;

public class MoveUnitCommandBuilderTests
{
    private readonly MoveUnitCommandBuilder _builder;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _playerId;
    private readonly Unit _unit;

    public MoveUnitCommandBuilderTests()
    {
        _playerId = Guid.NewGuid();
        _builder = new MoveUnitCommandBuilder(_gameId, _playerId);
        _unit = new MechFactory(new ClassicBattletechRulesProvider(),
            Substitute.For<ILocalizationService>()).Create(MechFactoryTests.CreateDummyMechData());
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenNoDataSet()
    {
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenOnlyUnitSet()
    {
        // Arrange
        _builder.SetUnit(_unit);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenOnlyMovementTypeSet()
    {
        // Arrange
        _builder.SetMovementType(MovementType.Walk);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenOnlyMovementPathSet()
    {
        // Arrange
        _builder.SetMovementPath([]);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenMissingMovementType()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetMovementPath([new PathSegment(new HexPosition(1,1,HexDirection.Bottom),
            new HexPosition(1,2,HexDirection.Bottom), 1)]);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenMissingDirection()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetMovementType(MovementType.Walk);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsTrue_WhenAllDataSet()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetMovementType(MovementType.Walk);
        _builder.SetMovementPath([new PathSegment(
            new HexPosition(1,1,HexDirection.Bottom),
            new HexPosition(1,2,HexDirection.Bottom), 
            1)]);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeTrue();
    }
    
    [Fact]
    public void Build_ReturnsCommand()
    {
        // Arrange
        var startPos = new HexPosition(1, 1, HexDirection.Bottom);
        var endPos = new HexPosition(1, 2, HexDirection.Bottom);
        var pathSegment = new PathSegment(startPos, endPos, 1);
        _builder.SetUnit(_unit);
        _builder.SetMovementType(MovementType.Walk);
        _builder.SetMovementPath([pathSegment]);
        
        // Act 
        var command = _builder.Build();
        
        // Assert
        command.ShouldNotBeNull();
        command.Value.GameOriginId.ShouldBe(_gameId);
        command.Value.MovementType.ShouldBe(MovementType.Walk);
        command.Value.PlayerId.ShouldBe(_playerId);
        command.Value.UnitId.ShouldBe(_unit.Id);
        command.Value.MovementPath.Count.ShouldBe(1);
        command.Value.MovementPath[0].Cost.ShouldBe(1);
        command.Value.MovementPath[0].From.Coordinates.ShouldBeEquivalentTo(startPos.Coordinates.ToData());
        command.Value.MovementPath[0].To.Coordinates.ShouldBeEquivalentTo(endPos.Coordinates.ToData());
    }
    
    [Fact]
    public void Build_ReturnsNull_WhenNoDataSet()
    {
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void Build_ReturnsNull_WhenOnlyUnitSet()
    {
        // Arrange
        _builder.SetUnit(_unit);
        
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void Build_ReturnsNull_WhenOnlyMovementTypeSet()
    {
        // Arrange
        _builder.SetMovementType(MovementType.Walk);
        
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void Build_ReturnsNull_WhenOnlyDirectionSet()
    {
        // Arrange
        _builder.SetMovementPath([new PathSegment(new HexPosition(1,1,HexDirection.Bottom),
        new HexPosition(1,2,HexDirection.Bottom), 2)]);
        
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void Build_ReturnsCommand_WithCorrectData_WhenAllDataSet()
    {
        // Arrange
        var startPos = new HexPosition(1, 1, HexDirection.Top);
        var midPos = new HexPosition(1, 1, HexDirection.TopRight);
        var endPos = new HexPosition(1, 2, HexDirection.TopRight);
        var turnSegment = new PathSegment(startPos, midPos, 1);
        var moveSegment = new PathSegment(midPos, endPos, 1);
        
        _builder.SetUnit(_unit);
        _builder.SetMovementType(MovementType.Walk);
        _builder.SetMovementPath([turnSegment, moveSegment]);
        
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldNotBeNull();
        result.Value.GameOriginId.ShouldBe(_gameId);
        result.Value.PlayerId.ShouldBe(_playerId);
        result.Value.UnitId.ShouldBe(_unit.Id);
        result.Value.MovementType.ShouldBe(MovementType.Walk);
        result.Value.MovementPath.Count.ShouldBe(2);
        result.Value.MovementPath[0].Cost.ShouldBe(1);
        result.Value.MovementPath[1].Cost.ShouldBe(1);
    }
    
    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetMovementType(MovementType.Walk);
        _builder.SetMovementPath([new PathSegment(new HexPosition(1,1,HexDirection.Bottom),
        new HexPosition(1,2,HexDirection.Bottom),2)]);
        
        // Act
        _builder.Reset();
        
        // Assert
        _builder.CanBuild.ShouldBeFalse();
    }
}
