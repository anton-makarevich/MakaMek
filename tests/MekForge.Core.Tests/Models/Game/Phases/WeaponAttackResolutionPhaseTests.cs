using NSubstitute;
using Sanet.MekForge.Core.Data.Game;
using Sanet.MekForge.Core.Models.Game.Commands.Client;
using Sanet.MekForge.Core.Models.Game.Commands.Server;
using Sanet.MekForge.Core.Models.Game.Dice;
using Sanet.MekForge.Core.Models.Game.Phases;
using Sanet.MekForge.Core.Models.Game.Players;
using Sanet.MekForge.Core.Models.Map;
using Sanet.MekForge.Core.Models.Units;
using Sanet.MekForge.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MekForge.Core.Tests.Models.Game.Phases;

public class WeaponAttackResolutionPhaseTests : GameStateTestsBase
{
    private readonly WeaponAttackResolutionPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _unit1Id;
    private readonly Guid _unit2Id;
    private readonly Unit _unit1;
    private readonly Unit _unit2;

    public WeaponAttackResolutionPhaseTests()
    {
        _sut = new WeaponAttackResolutionPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1", 2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Playing));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Playing));

        // Get unit IDs and references
        var player1 = Game.Players[0];
        _unit1 = player1.Units[0];
        _unit1Id = _unit1.Id;

        var player2 = Game.Players[1];
        _unit2 = player2.Units[0];
        _unit2Id = _unit2.Id;

        // Set initiative order
        Game.SetInitiativeOrder(new List<IPlayer> { player2, player1 });

        // Deploy units
        var q = 1;
        var r = 1;
        foreach (var unit in player1.Units.Concat(player2.Units))
        {
            unit.Deploy(new HexPosition(q, r, HexDirection.Top));
            q++;
            r++;
        }
    }

    [Fact]
    public void Enter_ShouldProcessAttacksInInitiativeOrder()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Verify that attack resolution commands were published in initiative order
        Received.InOrder(() =>
        {
            // Player 2 (initiative winner) attacks are resolved first
            CommandPublisher.PublishCommand(Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.PlayerId == _player2Id));
            
            // Player 1's attacks are resolved second
            CommandPublisher.PublishCommand(Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.PlayerId == _player1Id));
        });
    }

    [Fact]
    public void Enter_ShouldCalculateToHitNumbersForAllWeapons()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Verify ToHitCalculator was called for each weapon with a target
        Game.ToHitCalculator.Received(2).GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>());
    }

    [Fact]
    public void Enter_ShouldPublishAttackResolutionCommands()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Verify attack resolution commands were published with correct resolution data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _unit1Id));
        
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _unit2Id));
    }

    [Fact]
    public void Enter_WhenNoWeaponTargets_ShouldTransitionToNextPhase()
    {
        // Arrange - No weapon targets set up

        // Act
        _sut.Enter();

        // Assert
        // Should transition to PhysicalAttackPhase
        Game.TurnPhase.ShouldBe(PhaseNames.PhysicalAttack);
    }

    [Fact]
    public void Enter_AfterProcessingAllAttacks_ShouldTransitionToNextPhase()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Should transition to PhysicalAttackPhase after processing all attacks
        Game.TurnPhase.ShouldBe(PhaseNames.PhysicalAttack);
    }

    [Fact]
    public void Enter_ShouldSkipWeaponsWithoutTargets()
    {
        // Arrange - Setup weapon targets (including one without a target)
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Verify ToHitCalculator was called exactly twice (only for weapons with targets)
        // and not for the third weapon that has no target
        Game.ToHitCalculator.Received(2).GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>());
            
        // Verify only two attack resolution commands were published
        // (one for each weapon with a target)
        CommandPublisher.Received(2).PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id));
    }

    [Fact]
    public void Enter_ShouldRollDiceForAttackResolution()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits

        // Act
        _sut.Enter();

        // Assert
        // Verify that dice were rolled for attack resolution
        DiceRoller.Received().Roll2D6(); // Once for each attack
    }

    [Fact]
    public void Enter_ShouldRollForHitLocation_WhenAttackHits()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(8, 6); // First roll is for attack (8), second is for hit location (6)

        // Act
        _sut.Enter();

        // Assert
        // Verify that dice were rolled for hit location when attack hits
        DiceRoller.Received(4).Roll2D6(); // 2 for attacks, 2 for hit locations
    }

    [Fact]
    public void Enter_ShouldNotRollForHitLocation_WhenAttackMisses()
    {
        // Arrange - Setup weapon targets
        SetupWeaponTargets();
        SetupDiceRolls(5, 6); // First roll is for attack (5), which is less than to-hit number (7)

        // Act
        _sut.Enter();

        // Assert
        // Verify that dice were rolled only for attacks, not for hit locations
        DiceRoller.Received(2).Roll2D6(); // Only for attacks, not for hit locations
    }

    [Fact]
    public void HandleCommand_ShouldIgnoreAllCommands()
    {
        // Arrange
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Game.Id,
            PlayerId = _player1Id,
            AttackerId = _unit1Id,
            WeaponTargets = new List<WeaponTargetData>()
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        // No commands should be published as this phase doesn't process commands
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackResolutionCommand>());
    }

    private void SetupWeaponTargets()
    {
        // Add a weapon to each unit
        var weapon1 = new TestWeapon();
        var part1 = _unit1.Parts[0];
        part1.TryAddComponent(weapon1,[1]);
        weapon1.Target = _unit2; // Set target for weapon1

        var weapon2 = new TestWeapon();
        var part2 = _unit2.Parts[0];
        part2.TryAddComponent(weapon2,[1]);
        weapon2.Target = _unit1; // Set target for weapon2
        
        // Add a third weapon without a target to test that it's properly skipped
        var weaponWithoutTarget = new TestWeapon();
        var part3 = _unit1.Parts[1]; // Using the second part of unit1
        part3.TryAddComponent(weaponWithoutTarget,[2]);
        // Deliberately not setting a target for this weapon

        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a default to-hit number of 7
    }

    private void SetupDiceRolls(params int[] rolls)
    {
        var diceResults = new List<List<DiceResult>>();
        
        // Create dice results for each roll
        foreach (var roll in rolls)
        {
            var diceResult = new List<DiceResult>
            {
                new(roll / 2 + roll % 2),
                new(roll / 2)
            };
            diceResults.Add(diceResult);
        }
        
        // Set up the dice roller to return the predefined results
        var callCount = 0;
        DiceRoller.Roll2D6().Returns(_ =>
        {
            var result = diceResults[callCount % diceResults.Count];
            callCount++;
            return result;
        });
    }

    private class TestWeapon(WeaponType type = WeaponType.Energy, AmmoType ammoType = AmmoType.None)
        : Weapon("Test Weapon", 5, 3, 0, 3, 6, 9, type, 10, 1, 1, 1,ammoType);
        
    // Custom cluster weapon class that allows setting damage for testing
    private class TestClusterWeapon(
        int damage =10,
        int clusterSize = 1,
        int clusters = 2,
        WeaponType type = WeaponType.Missile,
        AmmoType ammoType = AmmoType.None)
        : Weapon("Test Cluster Weapon", damage, 3, 0, 3, 6, 9, type, 10, 1, clusters, clusterSize, ammoType);
        
    #region Cluster Weapon Tests
    
    [Fact]
    public void Enter_ShouldRollForClusterHits_WhenClusterWeaponHits()
    {
        // Arrange
        // Add a cluster weapon to unit1
        var clusterWeapon = new TestClusterWeapon(10,5); 
        var part1 = _unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _unit2; // Set target for the cluster weapon
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
            
        // Setup dice rolls: first for attack (8), second for cluster (9), third and fourth for hit locations
        SetupDiceRolls(8, 9, 6, 6);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that dice were rolled for attack, cluster hits, and hit locations
        DiceRoller.Received(4).Roll2D6(); // 1 for attack, 1 for cluster, 2 for hit locations
        
        // Verify the attack resolution command contains the correct data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _unit1Id &&
                cmd.ResolutionData.IsHit &&
                cmd.ResolutionData.HitLocationsData != null &&
                cmd.ResolutionData.HitLocationsData.ClusterRoll.Count == 2 && // 2 dice for cluster roll
                cmd.ResolutionData.HitLocationsData.ClusterRoll.Sum(d => d.Result) == 9 && // Total of 9
                cmd.ResolutionData.HitLocationsData.MissilesHit == 8 && // 8 hits for LRM-10 with roll of 9
                cmd.ResolutionData.HitLocationsData.HitLocations.Count == 2 && //2 clusters hit
                cmd.ResolutionData.HitLocationsData.HitLocations[0].Location == PartLocation.RightTorso &&
                cmd.ResolutionData.HitLocationsData.HitLocations[0].Damage == 5 && //first 5 missiles
                cmd.ResolutionData.HitLocationsData.HitLocations[1].Damage == 3 )); //second 8-5=3
    }
    
    [Fact]
    public void Enter_ShouldCalculateCorrectDamage_ForClusterWeapon()
    {
        // Arrange
        // Add a cluster weapon to unit1 (SRM-6 with 1 damage per missile)
        var clusterWeapon = new TestClusterWeapon(6, 6, 1); // 6 missiles, 1 damage per missile
        var part1 = _unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _unit2; // Set target for the cluster weapon
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
            
        // Setup dice rolls: first for attack (8), second for cluster (9 = 5 hits), third for hit location
        SetupDiceRolls(8, 9, 6);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify the attack resolution command contains the correct damage
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _unit1Id &&
                cmd.ResolutionData.IsHit &&
                cmd.ResolutionData.HitLocationsData != null &&
                cmd.ResolutionData.HitLocationsData.TotalDamage == 5)); // 5 hits * 1 damage per missile = 5 damage
    }
    
    [Fact]
    public void Enter_ShouldNotRollForClusterHits_WhenClusterWeaponMisses()
    {
        // Arrange
        // Add a cluster weapon to unit1
        var clusterWeapon = new TestClusterWeapon(1010,5); // LRM-10
        var part1 = _unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _unit2; // Set target for the cluster weapon
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
            
        // Setup dice rolls: first for attack (6), which is less than to-hit number (7)
        SetupDiceRolls(6);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that dice were rolled only for attack, not for cluster hits or hit locations
        DiceRoller.Received(1).Roll2D6(); // Only for attack
        
        // Verify the attack resolution command contains the correct data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _unit1Id &&
                !cmd.ResolutionData.IsHit &&
                cmd.ResolutionData.HitLocationsData == null));
    }
    
    #endregion
}