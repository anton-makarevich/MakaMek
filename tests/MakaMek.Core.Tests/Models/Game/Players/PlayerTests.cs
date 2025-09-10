using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Shouldly;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Players;

public class PlayerTests
{
    private readonly MechFactory _mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),
        Substitute.For<ILocalizationService>());
    
    private Unit CreateMech()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        unitData.Id = Guid.NewGuid();
        return _mechFactory.Create(unitData);
    }
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage-1,
                1,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }
    
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string name = "Test Player";
        const string tint = "#FF0000";
        
        // Act
        var player = new Player(id, name, tint);
        
        // Assert
        player.Id.ShouldBe(id);
        player.Name.ShouldBe(name);
        player.Tint.ShouldBe(tint);
        player.Status.ShouldBe(PlayerStatus.NotJoined);
        player.Units.ShouldBeEmpty();
        player.CanAct.ShouldBeFalse();
    }
    
    [Fact]
    public void Constructor_WithDefaultTint_ShouldUseWhite()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string name = "Test Player";
        
        // Act
        var player = new Player(id, name);
        
        // Assert
        player.Tint.ShouldBe("#ffffff");
    }
    
    [Fact]
    public void AddUnit_ShouldAddUnitToCollection()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player");
        var unit = CreateMech();
        
        // Act
        player.AddUnit(unit);
        
        // Assert
        player.Units.Count.ShouldBe(1);
        player.Units.First().ShouldBe(unit);
    }

    [Fact]
    public void AddUnit_ShouldSetUnitOwner()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player");
        var unit = CreateMech();
        
        // Act
        player.AddUnit(unit);
        
        // Assert
        unit.Owner.ShouldBe(player);
    }
    
    [Fact]
    public void AddUnit_ShouldAllowPlayerToAct()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player");
        var unit = CreateMech();
        
        // Act
        player.AddUnit(unit);
        
        // Assert
        player.CanAct.ShouldBeTrue();
    }
    
    [Fact]
    public void Status_ShouldBeSettable()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player")
        {
            // Act
            Status = PlayerStatus.Joined
        };

        // Assert
        player.Status.ShouldBe(PlayerStatus.Joined);
    }
    
    [Fact]
    public void Units_ShouldBeReadOnly()
    {
        // Arrange
        var player = new Player(Guid.NewGuid(), "Test Player");
        var unit = CreateMech();
        
        // Act
        player.AddUnit(unit);
        
        // Assert
        player.Units.ShouldBeAssignableTo<IReadOnlyList<Unit>>();
    }

    [Fact]
    public void AliveUnits_ShouldReturnOnlyUnitsThatAreNotDestroyed()
    {
        var player = new Player(Guid.NewGuid(), "Test Player");
        var aliveUnit = CreateMech();
        var destroyedUnit = CreateMech();
        // Destroy the head of the destroyed unit
        destroyedUnit.ApplyDamage([CreateHitDataForLocation(PartLocation.Head, 100)], HitDirection.Front);
        player.AddUnit(aliveUnit);
        player.AddUnit(destroyedUnit);
        player.AliveUnits.ShouldBe([aliveUnit]);
    }
}
