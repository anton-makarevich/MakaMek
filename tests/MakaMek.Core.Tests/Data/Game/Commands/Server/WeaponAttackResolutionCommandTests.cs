using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class WeaponAttackResolutionCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1;
    private readonly Unit _attacker;
    private readonly Unit _target;
    private readonly ComponentData _weaponData;

    public WeaponAttackResolutionCommandTests()
    {
        // Create players
        _player1 = new Player(Guid.NewGuid(), "Player 1");
        var player2 = new Player(Guid.NewGuid(), "Player 2");

        // Create units using MechFactory
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService);
        var attackerData = MechFactoryTests.CreateDummyMechData();
        attackerData.Id = Guid.NewGuid();
        var targetData = MechFactoryTests.CreateDummyMechData();
        targetData.Id = Guid.NewGuid();
        
        _attacker = mechFactory.Create(attackerData);
        _target = mechFactory.Create(targetData);
        
        // Add units to players
        _player1.AddUnit(_attacker);
        player2.AddUnit(_target);
        
        // Setup game to return players
        _game.Players.Returns(new List<IPlayer> { _player1, player2 });
        
        // Setup weapon data - using the Medium Laser in the right arm
        var weapon = _attacker.Parts.Values.SelectMany(p => p.GetComponents<Weapon>()).First();
        _weaponData = weapon.ToData();
    }
    
    private LocationHitData CreateHitDataForLocation(PartLocation partLocation,
        int damage,
        int[]? aimedShotRoll = null,
        int[]? locationRoll = null)
    {
        return new LocationHitData(
        [
            new LocationDamageData(partLocation,
                damage,
                0,
                false)
        ], aimedShotRoll??[], locationRoll??[], partLocation);
    }

    private WeaponAttackResolutionCommand CreateHitCommand()
    {
        // Create a hit with a single location
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [], [3,3]) // No aimed shot, location roll 6
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            [],
            0);
        
        var resolutionData = new AttackResolutionData(
            8,
            [new DiceResult(4), new DiceResult(5)],
            true,
            HitDirection.Front,
            hitLocationsData);
        
        return new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private WeaponAttackResolutionCommand CreateMissCommand()
    {
        var resolutionData = new AttackResolutionData(
            8,
            [new(2), new(3)],
            false,
            HitDirection.Front);
        
        return new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private WeaponAttackResolutionCommand CreateClusterHitCommand()
    {
        // Create a hit with multiple locations and cluster weapon
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.LeftArm, 2, [], [2, 3]), // No aimed shot, location roll 2,3
            CreateHitDataForLocation(PartLocation.RightArm, 2, [], [2, 1]), // No aimed shot, location roll 2,1
            CreateHitDataForLocation(PartLocation.CenterTorso, 6, [], [5, 3]) // No aimed shot, location roll 5,3
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            10,
            [new DiceResult(6), new DiceResult(4)],
            5);
        
        var resolutionData = new AttackResolutionData(
            7,
            [new DiceResult(4), new DiceResult(4)],
            true,
            HitDirection.Front,
            hitLocationsData);
        
        return new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };
    }

    [Fact]
    public void Render_ShouldFormatHit_Correctly()
    {
        // Arrange
        var sut = CreateHitCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Player 1's LCT-1V hits Player 2's LCT-1V with Machine Gun");
        result.ShouldContain("Target: 8, Roll: 9");
        result.ShouldContain("Total Damage: 5");
        result.ShouldContain("CT (Roll: 6): 5 armor damage");
    }

    [Fact]
    public void Render_ShouldFormatMiss_Correctly()
    {
        // Arrange
        var sut = CreateMissCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldBe("Player 1's LCT-1V misses Player 2's LCT-1V with Machine Gun (Target: 8, Roll: 5)");
        result.ShouldNotContain("Attack Direction");
    }

    [Fact]
    public void Render_ShouldFormatClusterWeapon_Correctly()
    {
        // Arrange
        var sut = CreateClusterHitCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Player 1's LCT-1V hits Player 2's LCT-1V with Machine Gun");
        result.ShouldContain("Target: 7, Roll: 8");
        result.ShouldContain("Total Damage: 10");
        result.ShouldContain("Cluster Roll: 10");
        result.ShouldContain("Missiles Hit: 5");
        result.ShouldContain("Hit Locations:");
        result.ShouldContain("LA (Roll: 5): 2 armor damage");
        result.ShouldContain("RA (Roll: 3): 2 armor damage");
        result.ShouldContain("CT (Roll: 8): 6 armor damage");
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var sut = CreateHitCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenAttackerNotFound()
    {
        // Arrange
        var sut = CreateHitCommand() with { AttackerId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenTargetNotFound()
    {
        // Arrange
        var sut = CreateHitCommand() with { TargetId = Guid.NewGuid() };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldIncludeAttackDirection_WhenHit()
    {
        // Arrange
        var sut = CreateHitCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Attack Direction: Front");
    }
    
    [Fact]
    public void Render_ShouldIncludeAttackDirection_WithClusterWeapon()
    {
        // Arrange
        var sut = CreateClusterHitCommand();

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Attack Direction: Front");
    }
    
    [Theory]
    [InlineData(HitDirection.Left, "Attack Direction: Left")]
    [InlineData(HitDirection.Right, "Attack Direction: Right")]
    [InlineData(HitDirection.Rear, "Attack Direction: Rear")]
    public void Render_ShouldDisplayCorrectAttackDirection(HitDirection direction, string expectedDirectionText)
    {
        // Arrange
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [], [3,3]) // No aimed shot, location roll 6
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            new List<DiceResult>(),
            0);
        
        var resolutionData = new AttackResolutionData(
            8,
            [new(4), new(5)],
            true,
            direction,
            hitLocationsData);
        
        var sut = new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain(expectedDirectionText);
    }

    [Fact]
    public void Render_Includes_HitLocationTransfer_Info_When_Transfers_Present()
    {
        // Arrange: create a hit with a transfer from LeftArm to LeftTorso
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(
                PartLocation.LeftTorso,  // Final location
                5,
                [], // No aimed shot
                [6, 4]) with { InitialLocation = PartLocation.LeftArm }
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            [],
            0);
            
        var resolutionData = new AttackResolutionData(
            8,
            [new DiceResult(4), new DiceResult(5)],
            true,
            HitDirection.Front,
            hitLocationsData);

        var sut = new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var output = sut.Render(_localizationService, _game);

        // Assert
        output.ShouldContain("LA (Roll: 10) → LT: 5 armor damage");
    }

    [Fact]
    public void Render_ShouldIncludeDestroyedParts_WhenPartsAreDestroyed()
    {
        // Arrange
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [], [3,3]) // No aimed shot, location roll 6
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            [],
            0);
        
        var destroyedParts = new List<PartLocation> { PartLocation.LeftArm, PartLocation.RightLeg };
        
        var resolutionData = new AttackResolutionData(
            8,
            [new(4), new(5)],
            true,
            HitDirection.Front,
            hitLocationsData,
            destroyedParts);
        
        var sut = new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Destroyed parts:");
        result.ShouldContain("- Left Arm destroyed");
        result.ShouldContain("- Right Leg destroyed");
    }
    
    [Fact]
    public void Render_ShouldIncludeUnitDestroyed_WhenUnitIsDestroyed()
    {
        // Arrange
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [], [3,3]) // No aimed shot, location roll 6
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            [],
            0);
        
        var resolutionData = new AttackResolutionData(
            8,
            [new(4), new(5)],
            true,
            HitDirection.Front,
            hitLocationsData,
            null,
            true);
        
        var sut = new WeaponAttackResolutionCommand
        {
            GameOriginId = _gameId,
            PlayerId = _player1.Id,
            AttackerId = _attacker.Id,
            TargetId = _target.Id,
            WeaponData = _weaponData,
            ResolutionData = resolutionData,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = sut.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("LCT-1V has been destroyed!");
    }
}
