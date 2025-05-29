using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;
using Shouldly.ShouldlyExtensionMethods;

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
        
        _player1Unit2.ApplyArmorAndStructureDamage(lethalDamage, head); // Apply lethal damage();

        // Act
        _sut.Enter();

        // Assert: Should transition to next phase and not get stuck
        MockPhaseManager.Received(1).GetNextPhase(PhaseNames.WeaponAttackResolution, Game);
        _mockNextPhase.Received(1).Enter();
        // Unit2 should be destroyed
        _player1Unit2.Status.ShouldBe(UnitStatus.Destroyed);
    }

    [Fact]
    public void Enter_ShouldConsiderPreviousDamage_WhenMultipleWeaponsTargetSameUnit()
    {
        // Arrange
        SetMap();
        
        // Setup weapons using the existing method
        SetupPlayer1WeaponTargets();
        
        // Get the target unit (player2's unit)
        var targetUnit = _player2Unit1;
        
        // Remove targets for the target unit
        var targetWeapons = targetUnit.GetAllComponents<Weapon>();
        foreach (var targetWeapon in targetWeapons)
        {
            targetWeapon.Target = null;
        }
        
        // Get the part we want to target (left arm)
        var targetPart = targetUnit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Get the initial armor and structure values
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;
        
        // Get the weapons from player1's unit
        var attackingUnit = _player1Unit1;
        var weaponWithoutTarget = attackingUnit.Parts[1].GetComponents<Weapon>().First();
        
        // Set target for the second weapon (same as first weapon)
        weaponWithoutTarget.Target = targetUnit;
        
        // Configure dice rolls to ensure hits and specific hit locations
        // First roll (8) is for first attack (hit)
        // Second roll (10) is for first hit location (left arm)
        // Third roll (8) is for second attack (hit)
        // Fourth roll (10) is for second hit location (same location)
        SetupDiceRolls(8, 10, 8, 10);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that damage was applied to the target part
        targetPart.CurrentArmor.ShouldBeLessThan(initialArmor);
        
        // Capture the published commands to verify both attacks hit the same location
        var capturedCommands = new List<WeaponAttackResolutionCommand>();
        CommandPublisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "PublishCommand")
            .Select(call => call.GetArguments()[0])
            .OfType<WeaponAttackResolutionCommand>()
            .Where(cmd => cmd.AttackerId == attackingUnit.Id) // Only get commands from our attacking unit
            .ToList()
            .ForEach(capturedCommands.Add);
        
        // We should have exactly 2 commands
        capturedCommands.Count.ShouldBe(2);
        
        // Both commands should target the left arm
        capturedCommands[0].ResolutionData.HitLocationsData!.HitLocations[0].Location
            .ShouldBe(PartLocation.LeftArm);
        capturedCommands[1].ResolutionData.HitLocationsData!.HitLocations[0].Location
            .ShouldBe(PartLocation.LeftArm);
        
        // Calculate the total damage from both attacks
        var totalDamage = capturedCommands.Sum(cmd => 
            cmd.ResolutionData.HitLocationsData!.TotalDamage);

        var nextPart = targetUnit.Parts.First(p=> p.Location == targetPart.GetNextTransferLocation());
        
        // The total damage should match the difference in armor/structure
        var armorDamage = initialArmor - targetPart.CurrentArmor;
        var structureDamage = initialStructure - targetPart.CurrentStructure;
        var transferDamage = nextPart.MaxArmor - nextPart.CurrentArmor;
        var totalDamageTaken = armorDamage + structureDamage + transferDamage;
        
        totalDamageTaken.ShouldBe(totalDamage);
    }
    
    [Fact]
    public void Enter_ShouldRollForClusterHits_WhenClusterWeaponHits()
    {
        // Arrange
        SetMap();
        // Add a cluster weapon to unit1
        var clusterWeapon = new TestClusterWeapon(1,5); 
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
        clusterWeapon.Target = _player2Unit1; // Set a target for the cluster weapon
        
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
                cmd.ResolutionData.HitLocationsData.TotalDamage == 30)); // 5 hits * 6 damage per missile = 30 damage
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
    
    // Helper to invoke private method
    private static HitLocationData InvokeDetermineHitLocation(WeaponAttackResolutionPhase phase, FiringArc arc, int dmg, Unit? target)
    {
        var method = typeof(WeaponAttackResolutionPhase).GetMethod("DetermineHitLocation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (HitLocationData)method!.Invoke(phase, [arc, dmg, target])!;
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

    [Fact]
    public void DetermineHitLocation_ShouldTransferToNextLocation_WhenInitialLocationIsDestroyed()
    {
        // Arrange
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        
        // Create a mech with multiple parts including a destroyed left arm
        var leftArm = new Arm("LeftArm", PartLocation.LeftArm, 5, 5);
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5,10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10,15);
        
        // Destroy the left arm
        leftArm.ApplyDamage(10); // Apply enough damage to destroy it
        leftArm.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed
        
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [leftArm, leftTorso, centerTorso]);
        
        // Configure the rules provider to return LeftArm as the initial hit location
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.LeftArm);
        
        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 for hit location roll
        );
        
        var sut = new WeaponAttackResolutionPhase(Game);
        
        // Act
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 5, mech);
        
        // Assert
        // Should have transferred from LeftArm to LeftTorso (based on Mech's GetTransferLocation implementation)
        data.Location.ShouldBe(PartLocation.LeftTorso);
    }
    
    [Fact]
    public void DetermineHitLocation_ShouldTransferMultipleTimes_WhenMultipleLocationsInChainAreDestroyed()
    {
        // Arrange
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        
        // Create a mech with multiple parts including destroyed left arm and left torso
        var leftArm = new Arm("LeftArm", PartLocation.LeftArm, 5, 5);
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5,10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10,15);
        
        // Destroy the left arm and left torso
        leftArm.ApplyDamage(10); // Apply enough damage to destroy it
        leftTorso.ApplyDamage(20); // Apply enough damage to destroy it
        
        leftArm.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed
        leftTorso.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed
        
        var mech = new Mech("TestChassis", "TestModel", 50, 5, [leftArm, leftTorso, centerTorso]);
        
        // Configure the rules provider to return LeftArm as the initial hit location
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), FiringArc.Forward).Returns(PartLocation.LeftArm);
        
        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 for hit location roll
        );
        
        var sut = new WeaponAttackResolutionPhase(Game);
        
        // Act
        var data = InvokeDetermineHitLocation(sut, FiringArc.Forward, 5, mech);
        
        // Assert
        // Should have transferred from LeftArm to LeftTorso to CenterTorso
        data.Location.ShouldBe(PartLocation.CenterTorso);
    }

    [Fact]
    public void Enter_ShouldTrackDestroyedParts_WhenApplyingDamage()
    {
        // Arrange - Setup weapon targets
        SetupPlayer1WeaponTargets();
        
        // Get the target unit (player2's unit)
        var targetUnit = _player2Unit1;
        
        // Get the part we want to target (left arm)
        var targetPart = targetUnit.Parts.First(p => p.Location == PartLocation.LeftArm);
        
        // Apply damage to the part to leave it with minimal structure
        // This way the next attack will destroy it
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;
        
        // Apply damage to leave only 1 structure point
        targetPart.ApplyDamage(initialArmor + initialStructure - 1);
        
        // Configure dice rolls to ensure hits and specific hit locations
        // First roll (8) is for attack (hit)
        // Second roll (10) is for hit location (left arm)
        SetupDiceRolls(8, 10);
        SetMap();
        
        // Act
        _sut.Enter();
        
        // Assert
        // Capture the published command to verify it includes the destroyed part
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.ResolutionData.DestroyedParts != null && 
                cmd.ResolutionData.DestroyedParts.Contains(PartLocation.LeftArm)));
    }
    
    [Fact]
    public void Enter_ShouldTrackUnitDestruction_WhenUnitIsDestroyed()
    {
        // Arrange - Setup weapon targets
        SetupPlayer1WeaponTargets();
        
        // Get the target unit (player2's unit)
        var targetUnit = _player2Unit1;
        
        // Get the part we want to target (center torso)
        var targetPart = targetUnit.Parts.First(p => p.Location == PartLocation.CenterTorso);
        
        // Apply damage to the center torso to leave it with minimal structure
        // This way the next attack will destroy the unit
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;
        
        // Apply damage to leave only 1 structure point
        targetPart.ApplyDamage(initialArmor + initialStructure - 1);
        
        // Configure dice rolls to ensure hits and specific hit locations
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        SetMap();
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify unit is destroyed
        targetUnit.Status.ShouldBe(UnitStatus.Destroyed);
        
        // Capture the published command to verify it includes unit destruction
        CommandPublisher.Received().PublishCommand(
            Arg.Is<WeaponAttackResolutionCommand>(cmd => 
                cmd.ResolutionData.UnitDestroyed));
    }

    [Fact]
    public void PublishCommand_ShouldAndApplyDamage()
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

    [Fact]
    public void Enter_ShouldPublishPilotingSkillRollCommand_WhenGyroIsHit()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        Game.PilotingSkillCalculator.GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.GyroHit)))
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new TestModifier { Value = 3, Name = "Damaged Gyro" }]
            });
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that a PilotingSkillRollCommand was published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<PilotingSkillRollCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.RollType == PilotingSkillRollType.GyroHit));
    }
    
    [Fact]
    public void Enter_ShouldNotPublishPilotingSkillRollCommand_WhenGyroIsNotHit()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    []
                )
            ]);
        
        // Act
        _sut.Enter();

        // Assert
        // Verify that no PilotingSkillRollCommand was published
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<PilotingSkillRollCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishPilotingSkillRollCommand_WhenPsrBreakdownHasNoModifiers()
    {
                // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        Game.PilotingSkillCalculator.GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.GyroHit)))
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = []
            });
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that no PilotingSkillRollCommand was published
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<PilotingSkillRollCommand>());
    }
    
    [Fact]
    public void Enter_ShouldPublishMechFallingCommand_WhenGyroHitPsrFails()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        Game.PilotingSkillCalculator.GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>())
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new TestModifier { Value = 3, Name = "Damaged Gyro" }]
            });
        
        // Setup dice roller to return a failed PSR roll (less than 7)
        DiceRoller.Roll2D6().Returns(
            // First roll for attack
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for hit location
            [new DiceResult(4), new DiceResult(3)],
            // Third roll for PSR (failed roll - 6 is less than 7 needed)
            [new DiceResult(3), new DiceResult(3)]
        );
        
        // Setup falling damage calculator
        var facingRoll = new DiceResult(3);
        var hitLocationRolls = new List<DiceResult> { new(3), new(3) };
        var hitLocationData = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            hitLocationRolls
        );
        var hitLocationsData = new HitLocationsData(
            [hitLocationData],
            5
        );
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.TopRight,
            hitLocationsData,
            facingRoll,
            true,
            [new DiceResult(3), new DiceResult(3)]
        );
        
        Game.FallingDamageCalculator.CalculateFallingDamage(
            Arg.Any<Unit>(),
            Arg.Is<int>(i => i == 0),
            Arg.Is<bool>(b => b == false),
            Arg.Any<PsrBreakdown>())
            .Returns(fallingDamageData);
        
        // Act
        _sut.Enter();
        
        // Assert
        _player1Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
        // Verify that a MechFallingCommand was published with the correct data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallingCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.UnitId == _player1Unit1.Id &&
                cmd.LevelsFallen == 0 &&
                cmd.WasJumping == false &&
                cmd.DamageData == fallingDamageData));
    }
    
    [Fact]
    public void Enter_ShouldGetPsrBreakdownForWarriorDamage_WhenMechFalls()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Setup piloting skill calculator for gyro hit
        Game.PilotingSkillCalculator.GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>())
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new TestModifier { Value = 3, Name = "Damaged Gyro" }]
            });
        
        // Setup dice roller to return a failed PSR roll (less than 7)
        DiceRoller.Roll2D6().Returns(
            // First roll for attack
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for hit location
            [new DiceResult(4), new DiceResult(3)],
            // Third roll for PSR (failed roll - 6 is less than 7 needed)
            [new DiceResult(3), new DiceResult(3)]
        );
        
        // Setup falling damage calculator
        var facingRoll = new DiceResult(3);
        var hitLocationRolls = new List<DiceResult> { new(3), new(3) };
        var hitLocationData = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            hitLocationRolls
        );
        var hitLocationsData = new HitLocationsData(
            [hitLocationData],
            5
        );
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.TopRight,
            hitLocationsData,
            facingRoll,
            true,
            [new DiceResult(3), new DiceResult(3)]
        );
        
        Game.FallingDamageCalculator.CalculateFallingDamage(
            Arg.Any<Unit>(),
            Arg.Is<int>(i => i == 0),
            Arg.Is<bool>(b => b == false),
            Arg.Any<PsrBreakdown>())
            .Returns(fallingDamageData);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that GetPsrBreakdown was called with WarriorDamageFromFall roll type
        Game.PilotingSkillCalculator.Received().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.WarriorDamageFromFall)),
            Arg.Any<BattleMap>());
    }
    
    [Fact]
    public void Enter_ShouldPublishMechFallingCommand_WhenGyroIsDestroyed()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                _player1Unit1,
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Destroy the gyro with 2 hits
        var gyro = _player1Unit1.GetAllComponents<Gyro>().First();
        gyro.Hit();
        gyro.Hit();
        
        // Setup falling damage calculator
        var facingRoll = new DiceResult(3);
        var hitLocationRolls = new List<DiceResult> { new(3), new(3) };
        var hitLocationData = new HitLocationData(
            PartLocation.CenterTorso,
            5,
            hitLocationRolls
        );
        var hitLocationsData = new HitLocationsData(
            [hitLocationData],
            5
        );
        
        var fallingDamageData = new FallingDamageData(
            HexDirection.TopRight,
            hitLocationsData,
            facingRoll,
            true,
            [new DiceResult(3), new DiceResult(3)]
        );
        
        Game.FallingDamageCalculator.CalculateFallingDamage(
            Arg.Any<Unit>(),
            Arg.Is<int>(i => i == 0),
            Arg.Is<bool>(b => b == false),
            Arg.Any<PsrBreakdown>())
            .Returns(fallingDamageData);
        
        // Act
        _sut.Enter();
        
        // Assert
        _player1Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
        
        // Verify that a MechFallingCommand was published with the correct data
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallingCommand>(cmd => 
                cmd.GameOriginId == Game.Id &&
                cmd.UnitId == _player1Unit1.Id &&
                cmd.LevelsFallen == 0 &&
                cmd.WasJumping == false &&
                cmd.DamageData == fallingDamageData));
        
        // Verify that NO PilotingSkillRollCommand was published (since gyro is destroyed)
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<PilotingSkillRollCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishPsrOrMechFallingCommands_WhenNoGyroFound()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                _player1Unit1,
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Remove gyro to simulate no gyro mech (not possible, so maybe we should throw)
        var ct = _player1Unit1.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var gyro = ct.GetComponents<Gyro>().First();
        ct.RemoveComponent(gyro);
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that a MechFallingCommand was published with the correct data
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<MechFallingCommand>());
        
        // Verify that NO PilotingSkillRollCommand was published (since gyro is destroyed)
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<PilotingSkillRollCommand>());
    }
    
    [Fact]
    public void Enter_ShouldNotPublishMechFallingCommand_WhenGyroHitPsrSucceeds()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        
        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
            Arg.Any<Unit>(), 
            Arg.Any<Unit>(), 
            Arg.Any<Weapon>(), 
            Arg.Any<BattleMap>())
            .Returns(7); // Return a to-hit number of 7
        
        // Configure dice rolls to ensure hit on center torso
        // First roll (8) is for attack (hit)
        // Second roll (7) is for hit location (center torso)
        SetupDiceRolls(8, 7);
        
        // Setup critical hit calculator to return a gyro hit
        Game.CriticalHitsCalculator.CalculateCriticalHits(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(loc => loc == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([
                new LocationCriticalHitsData(PartLocation.CenterTorso,
                    7,
                    1,
                    [
                        new ComponentHitData
                        {
                            Type = MakaMekComponent.Gyro,
                            Slot = 3
                        }
                    ]
                )
            ]);
        
        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        Game.PilotingSkillCalculator.GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(
                types => types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>())
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new TestModifier { Value = 3, Name = "Damaged Gyro" }]
            });
        
        // Setup dice roller to return a successful PSR roll (greater than or equal to 7)
        DiceRoller.Roll2D6().Returns(
            // First roll for attack
            [new DiceResult(4), new DiceResult(4)],
            // Second roll for hit location
            [new DiceResult(4), new DiceResult(3)],
            // Third roll for PSR (successful roll - 8 is greater than 7 needed)
            [new DiceResult(4), new DiceResult(4)]
        );
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that no MechFallingCommand was published
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Any<MechFallingCommand>());
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
    
    private class TestWeapon(WeaponType type = WeaponType.Energy, MakaMekComponent? ammoType = null)
        : Weapon( new WeaponDefinition(
            "Test Weapon", 5, 3,
            0, 3, 6, 9, 
            type, 10, 1, 1,1, 1,MakaMekComponent.MachineGun,ammoType))
    {
    }

    // Custom cluster weapon class that allows setting damage for testing
    private class TestClusterWeapon(
        int damage =10,
        int clusterSize = 1,
        int clusters = 2,
        WeaponType type = WeaponType.Missile,
        MakaMekComponent? ammoType = null)
        : Weapon( new WeaponDefinition(
            "Test Cluster Weapon", damage, 3,
            0, 3, 6, 9,
            type, 10, clusters, clusterSize,1,1,MakaMekComponent.LRM10, ammoType))
    {
    }
    
    private record TestModifier : Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.RollModifier
    {
        public required string Name { get; init; }
        
        public override string Render(Sanet.MakaMek.Core.Services.Localization.ILocalizationService localizationService)
        {
            return Name;
        }
    }
}