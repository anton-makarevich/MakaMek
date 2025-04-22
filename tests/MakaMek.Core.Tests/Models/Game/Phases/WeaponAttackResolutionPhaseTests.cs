using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class WeaponAttackResolutionPhaseTests : GamePhaseTestsBase
{
    private readonly WeaponAttackResolutionPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _player1Unit1Id;
    private readonly Unit _player1Unit1;
    private readonly Unit _player1Unit2;
    private readonly Guid _player2Unit1Id;
    private readonly Unit _player2Unit1;
    private readonly IGamePhase _mockNextPhase;

    public WeaponAttackResolutionPhaseTests()
    {
        // Create mock next phase and configure the phase manager
        _mockNextPhase = Substitute.For<IGamePhase>();
        MockPhaseManager.GetNextPhase(PhaseNames.WeaponAttackResolution, Game).Returns(_mockNextPhase);
        
        _sut = new WeaponAttackResolutionPhase(Game);

        // Add two players with units
        Game.HandleCommand(CreateJoinCommand(_player1Id, "Player 1", 2));
        Game.HandleCommand(CreateJoinCommand(_player2Id, "Player 2"));
        Game.HandleCommand(CreateStatusCommand(_player1Id, PlayerStatus.Ready));
        Game.HandleCommand(CreateStatusCommand(_player2Id, PlayerStatus.Ready));

        // Get unit IDs and references
        var player1 = Game.Players[0];
        _player1Unit1 = player1.Units[0];
        _player1Unit1Id = _player1Unit1.Id;
        _player1Unit2 = player1.Units[1];

        var player2 = Game.Players[1];
        _player2Unit1 = player2.Units[0];
        _player2Unit1Id = _player2Unit1.Id;

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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();
        
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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

        // Act
        _sut.Enter();

        // Assert
        // Verify attack resolution commands were published with correct resolution data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _player1Unit1Id));
        
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.GameOriginId == Game.Id && 
                cmd.AttackerId == _player2Unit1Id));
    }

    [Fact]
    public void Enter_WhenNoWeaponTargets_ShouldTransitionToNextPhase()
    {
        // Arrange - No weapon targets set up

        // Act
        _sut.Enter();

        // Assert
        // Should transition to next phase
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.WeaponAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void Enter_AfterProcessingAllAttacks_ShouldTransitionToNextPhase()
    {
        // Arrange - Setup weapon targets
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

        // Act
        _sut.Enter();

        // Assert
        // Should transition to next phase after processing all attacks
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.WeaponAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
    }

    [Fact]
    public void Enter_ShouldSkipWeaponsWithoutTargets()
    {
        // Arrange - Setup weapon targets (including one without a target)
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // First roll is for attack (8), second is for hit location (6)
        SetMap();

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
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(5, 6); // First roll is for attack (5), which is less than to-hit number (7)
        SetMap();

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
            AttackerId = _player1Unit1Id,
            WeaponTargets = new List<WeaponTargetData>()
        };

        // Act
        _sut.HandleCommand(command);

        // Assert
        // No commands should be published as this phase doesn't process commands
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackResolutionCommand>());
    }

    [Fact]
    public void Enter_ShouldNotGetStuck_WhenUnitIsDestroyedDuringAttackResolution()
    {
        // Arrange: Setup weapon targets so that unit1 attacks unit2's head with lethal damage
        SetupPlayer1WeaponTargets();
        SetupPlayer2WeaponTargets();
        SetupDiceRolls(5, 6); // First roll is for attack (5), which is less than to-hit number (7)
        SetMap();

        // Make sure the attack will hit and deal enough damage to destroy the head
        // Assume unit2's head is the first part and get its max armor/structure
        var head = _player1Unit2.Parts.First(p => p.Location == PartLocation.Head);
        var lethalDamage = head.MaxArmor + head.MaxStructure + 1;
        
        _player1Unit2.ApplyDamage(lethalDamage, head); // Apply lethal damage();

        // Act
        _sut.Enter();

        // Assert: Should transition to next phase and not get stuck
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.WeaponAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
        // Unit2 should be destroyed
        _player1Unit2.Status.ShouldBe(UnitStatus.Destroyed);
    }

    private void SetupPlayer1WeaponTargets()
    {
        // Add a weapon to each unit
        var weapon1 = new TestWeapon();
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(weapon1,[1]);
        weapon1.Target = _player2Unit1; // Set target for weapon1

        var weapon2 = new TestWeapon();
        var part2 = _player2Unit1.Parts[0];
        part2.TryAddComponent(weapon2,[1]);
        weapon2.Target = _player1Unit1; // Set target for weapon2
        
        // Add a third weapon without a target to test that it's properly skipped
        var weaponWithoutTarget = new TestWeapon();
        var part3 = _player1Unit1.Parts[1]; // Using the second part of unit1
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
    private void SetupPlayer2WeaponTargets()
    {
        // Add a weapon to each unit
        var weapon1 = new TestWeapon();
        var part1 = _player2Unit1.Parts[0];
        part1.TryAddComponent(weapon1,[1]);
        weapon1.Target = _player1Unit1; // Set target for weapon1
        
        // Add a third weapon without a target to test that it's properly skipped
        var weaponWithoutTarget = new TestWeapon();
        var part3 = _player2Unit1.Parts[1]; // Using the second part of unit1
        part3.TryAddComponent(weaponWithoutTarget,[2]);
        // Deliberately not setting a target for this weapon// Return a default to-hit number of 7
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
    
    [Fact]
    public void PublishCommand_ShouldFireWeaponAndApplyDamage()
    {
        // Arrange
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();
        
        // Get initial values for verification
        var initialArmor = _player2Unit1.TotalCurrentArmor;
        
        // Act
        _sut.Enter();

        // Assert
        // Verify that damage was applied to the target
        _player2Unit1.TotalCurrentArmor.ShouldBeLessThan(initialArmor);
    }

    private class TestWeapon(WeaponType type = WeaponType.Energy, AmmoType ammoType = AmmoType.None)
        : Weapon("Test Weapon", 5, 3, 0, 3, 6, 9, type, 10, 1, 1, 1,ammoType)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }

    // Custom cluster weapon class that allows setting damage for testing
    private class TestClusterWeapon(
        int damage =10,
        int clusterSize = 1,
        int clusters = 2,
        WeaponType type = WeaponType.Missile,
        AmmoType ammoType = AmmoType.None)
        : Weapon("Test Cluster Weapon", damage, 3, 0, 3, 6, 9, type, 10, 1, clusters, clusterSize, ammoType)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
    
    [Fact]
    public void Enter_ShouldRollForClusterHits_WhenClusterWeaponHits()
    {
        // Arrange
        SetMap();
        // Add a cluster weapon to unit1
        var clusterWeapon = new TestClusterWeapon(10,5); 
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _player2Unit1; // Set target for the cluster weapon
        
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
                cmd.AttackerId == _player1Unit1Id &&
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
        SetMap();
        var clusterWeapon = new TestClusterWeapon(6, 6, 1); // 6 missiles, 1 damage per missile
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _player2Unit1; // Set target for the cluster weapon
        
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
                cmd.AttackerId == _player1Unit1Id &&
                cmd.ResolutionData.IsHit &&
                cmd.ResolutionData.HitLocationsData != null &&
                cmd.ResolutionData.HitLocationsData.TotalDamage == 5)); // 5 hits * 1 damage per missile = 5 damage
    }
    
    [Fact]
    public void Enter_ShouldNotRollForClusterHits_WhenClusterWeaponMisses()
    {
        // Arrange
        // Add a cluster weapon to unit1
        SetMap();
        var clusterWeapon = new TestClusterWeapon(1010,5); // LRM-10
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(clusterWeapon, [1]);
        clusterWeapon.Target = _player2Unit1; // Set target for the cluster weapon
        
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
                cmd.AttackerId == _player1Unit1Id &&
                !cmd.ResolutionData.IsHit &&
                cmd.ResolutionData.HitLocationsData == null));
    }

    [Fact]
    public void DetermineHitLocation_ShouldSetCriticalHits_WhenDamageExceedsArmorAndCritsRolled()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent([i]));
        mockRulesProvider.GetNumCriticalHits(10, part.Location).Returns(2);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.RightArm);
        DiceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(5), new(5) },
            new List<DiceResult> { new(4), new(6) }
        );
        DiceRoller.RollD6().Returns(new DiceResult(2), new DiceResult(3), new DiceResult(4), new DiceResult(5));
        var sut = new WeaponAttackResolutionPhase(Game);
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 5, mech);
        data.CriticalHits.ShouldNotBeNull();
        data.CriticalHits.Length.ShouldBe(2);
        data.CriticalHits.ShouldContain(10);
        data.CriticalHits.ShouldContain(2);
    }

    [Fact]
    public void DetermineHitLocation_ShouldNotSetCriticalHits_WhenNoCritsRolled()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent([i]));
        mockRulesProvider.GetNumCriticalHits(8, part.Location).Returns(0);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.RightArm);
        DiceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(4), new(4) },
            new List<DiceResult> { new(5), new(3) }
        );
        var sut = new WeaponAttackResolutionPhase(Game);
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 5, mech);
        data.CriticalHits.ShouldBeNull();
    }

    [Fact]
    public void DetermineHitLocation_ShouldAutoPickOnlyAvailableSlot()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        var part = new Arm("TestArm", PartLocation.RightArm, 0, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        // Only slot 0 is available
        mockRulesProvider.GetNumCriticalHits(9, part.Location).Returns(1);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.RightArm);
        DiceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(6), new(3) },
            new List<DiceResult> { new(4), new(5) }
        );
        var sut = new WeaponAttackResolutionPhase(Game);
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 5, mech);
        data.CriticalHits.ShouldNotBeNull();
        data.CriticalHits.Length.ShouldBe(1);
        data.CriticalHits[0].ShouldBe(0);
    }

    [Fact]
    public void DetermineHitLocation_ShouldNotSetCriticalHits_WhenDamageDoesNotExceedArmor()
    {
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        var part = new Arm("TestArm", PartLocation.RightArm, 5, 10);
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [part]);
        for (var i = 1; i < part.TotalSlots; i++)
            part.TryAddComponent(new TestComponent([i]));
        mockRulesProvider.GetNumCriticalHits(12, part.Location).Returns(3);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.RightArm);
        DiceRoller.Roll2D6().Returns(
            new List<DiceResult> { new(6), new(6) },
            new List<DiceResult> { new(6), new(6) }
        );
        var sut = new WeaponAttackResolutionPhase(Game);
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 3, mech);
        data.CriticalHits.ShouldBeNull();
    }

    // Helper to invoke private method
    private static HitLocationData InvokeDetermineHitLocation(WeaponAttackResolutionPhase phase, FiringArc arc, int dmg, Unit? target)
    {
        var method = typeof(WeaponAttackResolutionPhase).GetMethod("DetermineHitLocation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (HitLocationData)method!.Invoke(phase, [arc, dmg, target])!;
    }

    private class TestComponent(int[] slots) : Sanet.MakaMek.Core.Models.Units.Components.Component("Test", slots)
    {
        public override MakaMekComponent ComponentType=> MakaMekComponent.MachineGun;
    }

    [Fact]
    public void Enter_WhenBattleMapIsNull_ShouldThrowException()
    {
        // Arrange
        // Ensure BattleMap is null (default state after ServerGame creation in test base)
        Game.BattleMap.ShouldBeNull(); 
        
        // Setup weapon targets - necessary to reach the ResolveAttack call
        SetupPlayer1WeaponTargets(); 
        SetupDiceRolls(8, 6); // Set up dice rolls, values don't matter much here

        // Act
        var act = () => _sut.Enter();

        // Assert
        // Verify that calling Enter throws the specific exception because BattleMap is null
        var exception = Should.Throw<Exception>(act);
        exception.Message.ShouldBe("Battle map is null");
        
        // Verify no commands were published as the exception happens before that
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<WeaponAttackResolutionCommand>());
    }
}