using Shouldly;
using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Commands.Server;

public class WeaponAttackResolutionCommandTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Player _player1;
    private readonly Unit _attacker;
    private readonly Unit _target;
    private readonly WeaponData _weaponData;

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
        var weapon = _attacker.Parts.SelectMany(p => p.GetComponents<Weapon>()).First();
        _weaponData = new WeaponData
        {
            Name = weapon.Name, // Added Name property
            Location = weapon.MountedOn!.Location,
            Slots = weapon.MountedAtSlots  // This might need adjustment based on the actual slot position
        };
        
        // Setup localization service
        _localizationService.GetString("Command_WeaponAttackResolution_Hit")
            .Returns("{0}'s {1} hits {3}'s {4} with {2} (Target: {5}, Roll: {6})");
        _localizationService.GetString("Command_WeaponAttackResolution_Miss")
            .Returns("{0}'s {1} misses {3}'s {4} with {2} (Target: {5}, Roll: {6})");
        _localizationService.GetString("Command_WeaponAttackResolution_Direction")
            .Returns("Attack Direction: {0}");
        _localizationService.GetString("AttackDirection_Forward")
            .Returns("Front");
        _localizationService.GetString("AttackDirection_Left")
            .Returns("Left");
        _localizationService.GetString("AttackDirection_Right")
            .Returns("Right");
        _localizationService.GetString("AttackDirection_Rear")
            .Returns("Rear");
        _localizationService.GetString("Command_WeaponAttackResolution_TotalDamage")
            .Returns("Total Damage: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_MissilesHit")
            .Returns("Missiles Hit: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_ClusterRoll")
            .Returns("Cluster Roll: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocations")
            .Returns("Hit Locations:");
        _localizationService.GetString("Command_WeaponAttackResolution_HitLocation")
            .Returns("{0}: {1} damage (Roll: {2})");
        _localizationService.GetString("Command_WeaponAttackResolution_CriticalHit")
            .Returns("Critical hit in {0} slot {1}: {2}");
        _localizationService.GetString("Command_WeaponAttackResolution_CritRoll")
            .Returns("Critical roll: {0}");
        _localizationService.GetString("Command_WeaponAttackResolution_NumCrits")
            .Returns("Criticals: {0}");
    }

    private WeaponAttackResolutionCommand CreateHitCommand()
    {
        // Create a hit with a single location
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [new(6)])
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
            FiringArc.Forward,
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
            false);
        
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
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.LeftArm, 2, [new(2), new(3)]),
            new(PartLocation.RightArm, 2, [new(2), new(1)]),
            new(PartLocation.CenterTorso, 6, [new(5), new(3)])
        };
        
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            10,
            [new(6), new(4)],
            5);
        
        var resolutionData = new AttackResolutionData(
            7,
            [new(4), new(4)],
            true,
            FiringArc.Forward,
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
    public void Format_ShouldFormatHit_Correctly()
    {
        // Arrange
        var command = CreateHitCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Player 1's Locust LCT-1V hits Player 2's Locust LCT-1V with machine Gun");
        result.ShouldContain("Target: 8, Roll: 9");
        result.ShouldContain("Total Damage: 5");
        result.ShouldContain("CenterTorso: 5 damage");
    }

    [Fact]
    public void Format_ShouldFormatMiss_Correctly()
    {
        // Arrange
        var command = CreateMissCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldBe("Player 1's Locust LCT-1V misses Player 2's Locust LCT-1V with Machine Gun (Target: 8, Roll: 5)");
        result.ShouldNotContain("Attack Direction");
    }

    [Fact]
    public void Format_ShouldFormatClusterWeapon_Correctly()
    {
        // Arrange
        var command = CreateClusterHitCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Player 1's Locust LCT-1V hits Player 2's Locust LCT-1V with Machine Gun");
        result.ShouldContain("Target: 7, Roll: 8");
        result.ShouldContain("Total Damage: 10");
        result.ShouldContain("Cluster Roll: 10");
        result.ShouldContain("Missiles Hit: 5");
        result.ShouldContain("Hit Locations:");
        result.ShouldContain("LeftArm: 2 damage");
        result.ShouldContain("RightArm: 2 damage");
        result.ShouldContain("CenterTorso: 6 damage");
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenPlayerNotFound()
    {
        // Arrange
        var command = CreateHitCommand() with { PlayerId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenAttackerNotFound()
    {
        // Arrange
        var command = CreateHitCommand() with { AttackerId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ShouldReturnEmpty_WhenTargetNotFound()
    {
        // Arrange
        var command = CreateHitCommand() with { TargetId = Guid.NewGuid() };

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Format_ShouldIncludeAttackDirection_WhenHit()
    {
        // Arrange
        var command = CreateHitCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Attack Direction: Front");
    }
    
    [Fact]
    public void Format_ShouldIncludeAttackDirection_WithClusterWeapon()
    {
        // Arrange
        var command = CreateClusterHitCommand();

        // Act
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("Attack Direction: Front");
    }
    
    [Theory]
    [InlineData(FiringArc.Left, "Attack Direction: Left")]
    [InlineData(FiringArc.Right, "Attack Direction: Right")]
    [InlineData(FiringArc.Rear, "Attack Direction: Rear")]
    public void Format_ShouldDisplayCorrectAttackDirection(FiringArc direction, string expectedDirectionText)
    {
        // Arrange
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, [new(6)])
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
        
        var command = new WeaponAttackResolutionCommand
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
        var result = command.Format(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain(expectedDirectionText);
    }
    
    [Fact]
    public void Format_Includes_CriticalHitsData_When_It_Present_ButNotHitSlots()
    {
        // Arrange: create a hit with a critical hit in slot 2, with a component
        var leftArm = _target.Parts.First(p => p.Location == PartLocation.LeftArm);
        var critComponent = new MachineGun();
        leftArm.TryAddComponent(critComponent, [2]);
        var hitLocations = new List<HitLocationData>
        {
            new(
                PartLocation.LeftArm,
                5,
                new List<DiceResult>(),
                new CriticalHitsData(7, 0, null)
            )
        };
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            new List<DiceResult>(),
            1);
        var resolutionData = new AttackResolutionData(
            8,
            [new DiceResult(4), new DiceResult(5)],
            true,
            FiringArc.Forward,
            hitLocationsData);

        var command = new WeaponAttackResolutionCommand
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
        var output = command.Format(_localizationService, _game);

        // Assert
        output.ShouldContain("Critical roll: 7");
        output.ShouldContain("Criticals: 0");
        output.ShouldNotContain("Critical hit in");
    }

    [Fact]
    public void Format_Includes_CriticalHit_Info_When_CriticalHits_Present()
    {
        // Arrange: create a hit with a critical hit in slot 2, with a component
        var leftArm = _target.Parts.First(p => p.Location == PartLocation.LeftArm);
        var critComponent = new MachineGun();
        leftArm.TryAddComponent(critComponent, [2]);
        var hitLocations = new List<HitLocationData>
        {
            new(
                PartLocation.LeftArm,
                5,
                new List<DiceResult>(),
                new CriticalHitsData(10, 1, [2])
            )
        };
        var hitLocationsData = new AttackHitLocationsData(
            hitLocations,
            5,
            new List<DiceResult>(),
            1);
        var resolutionData = new AttackResolutionData(
            8,
            [new DiceResult(4), new DiceResult(5)],
            true,
            FiringArc.Forward,
            hitLocationsData);

        var command = new WeaponAttackResolutionCommand
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
        var output = command.Format(_localizationService, _game);

        // Assert
        output.ShouldContain("Critical hit in LeftArm slot 2: Machine Gun");
    }
}
