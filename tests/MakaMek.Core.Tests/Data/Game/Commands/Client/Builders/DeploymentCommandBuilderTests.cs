using NSubstitute;
using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Client.Builders;

public class DeploymentCommandBuilderTests
{
    private readonly DeploymentCommandBuilder _builder;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Unit _unit;
    private readonly HexCoordinates _coordinates;
    
    public DeploymentCommandBuilderTests()
    {
        _builder = new DeploymentCommandBuilder(_gameId, _playerId);
        _unit = new MechFactory(
                new ClassicBattletechRulesProvider(),
                new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData());
        _coordinates = new HexCoordinates(1, 1);
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
    public void CanBuild_ReturnsFalse_WhenOnlyPositionSet()
    {
        // Arrange
        _builder.SetPosition(_coordinates);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsFalse_WhenOnlyDirectionSet()
    {
        // Arrange
        _builder.SetDirection(HexDirection.Top);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeFalse();
    }
    
    [Fact]
    public void CanBuild_ReturnsTrue_WhenAllDataSet()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetPosition(_coordinates);
        _builder.SetDirection(HexDirection.Top);
        
        // Act & Assert
        _builder.CanBuild.ShouldBeTrue();
    }
    
    [Fact]
    public void Build_ReturnsNull_WhenCanBuildIsFalse()
    {
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void Build_ReturnsCommand_WithCorrectData_WhenAllDataSet()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetPosition(_coordinates);
        _builder.SetDirection(HexDirection.Top);
        
        // Act
        var result = _builder.Build();
        
        // Assert
        result.ShouldNotBeNull();
        result.Value.GameOriginId.ShouldBe(_gameId);
        result.Value.PlayerId.ShouldBe(_playerId);
        result.Value.UnitId.ShouldBe(_unit.Id);
        result.Value.Position.Q.ShouldBe(_coordinates.Q);
        result.Value.Position.R.ShouldBe(_coordinates.R);
        result.Value.Direction.ShouldBe((int)HexDirection.Top);
    }
    
    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        _builder.SetUnit(_unit);
        _builder.SetPosition(_coordinates);
        _builder.SetDirection(HexDirection.Top);
        
        // Act
        _builder.Reset();
        
        // Assert
        _builder.CanBuild.ShouldBeFalse();
        _builder.Build().ShouldBeNull();
    }
}
