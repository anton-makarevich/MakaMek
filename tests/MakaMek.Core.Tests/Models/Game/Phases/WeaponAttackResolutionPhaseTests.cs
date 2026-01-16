using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Shouldly;
using Shouldly.ShouldlyExtensionMethods;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Phases;

public class WeaponAttackResolutionPhaseTests : GamePhaseTestsBase
{
    private readonly WeaponAttackResolutionPhase _sut;
    private readonly Guid _player1Id = Guid.NewGuid();
    private readonly Guid _player2Id = Guid.NewGuid();
    private readonly Guid _player1Unit1Id;
    private readonly IUnit _player1Unit1;
    private readonly IUnit _player1Unit2;
    private readonly Guid _player2Unit1Id;
    private readonly IUnit _player2Unit1;
    private readonly IGamePhase _mockNextPhase;
    private readonly IRulesProvider _rulesProvider = new ClassicBattletechRulesProvider();
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public WeaponAttackResolutionPhaseTests()
    {
        // Create a next phase mock and configure the phase manager
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
        
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns(callInfo =>[new LocationDamageData(callInfo.Arg<PartLocation>(),
                callInfo.Arg<int>(),
                0,
                false)]);
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
    public void Enter_ShouldNotPublishCommands_WhenMechCannotFire()
    {
        // Arrange
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        SetMap();

        // Destroy sensors
        var sensors = _player1Unit1.GetAllComponents<Sensors>().First();
        sensors.Hit();
        sensors.Hit();

        // Act
        _sut.Enter();

        // Assert
        // Verify no commands were published
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Is<WeaponAttackResolutionCommand>(cmd =>
            cmd.PlayerId == _player1Id));
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
            Arg.Any<BattleMap>(),
            Arg.Any<bool>(),
            Arg.Any<PartLocation?>());
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
        // Should transition to the next phase
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
        // Should transition to the next phase after processing all attacks
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
            Arg.Any<BattleMap>(),
            Arg.Any<bool>(),
            Arg.Any<PartLocation?>());

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
        SetupDiceRolls(8, 6); // The first roll is for attack (8), the second is for hit location (6)
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
        SetupDiceRolls(5, 6); // The first roll is for attack (5), which is less than to-hit number (7)
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
            UnitId = _player1Unit1Id,
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
        SetupDiceRolls(5, 6); // The first roll is for attack (5), which is less than to-hit number (7)
        SetMap();

        // Make sure the attack will hit and deal enough damage to destroy the head
        // Assume unit2's head is the first part and get its max armor/structure
        var head = _player1Unit2.Parts[PartLocation.Head];
        var lethalDamage = head.MaxArmor + head.MaxStructure + 1;

        _player1Unit2.ApplyDamage([CreateHitDataForLocation(PartLocation.Head, lethalDamage)], HitDirection.Front); // Apply lethal damage();

        // Act
        _sut.Enter();

        // Assert: Should transition to the next phase and not get stuck
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

        // Set up weapons using the existing method
        SetupPlayer1WeaponTargets();

        // Get the target unit (player2's unit)
        var targetUnit = _player2Unit1;

        // Clear weapon targets for the target unit
        targetUnit.ResetTurnState();

        // Get the part we want to target (left arm)
        var targetPart = targetUnit.Parts[PartLocation.LeftArm];

        // Get the initial armor and structure values
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;

        // Get the weapons from player1's unit
        var attackingUnit = _player1Unit1;
        var weaponWithoutTarget = attackingUnit.Parts.Values.Skip(1).First().GetComponents<Weapon>().First();

        // Set a target for the second weapon (same as the first weapon)
        var additionalWeaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = weaponWithoutTarget.Name,
                    Type = weaponWithoutTarget.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(
                            attackingUnit.Parts.Values.Skip(1).First().Location,
                            weaponWithoutTarget.MountedAtFirstLocationSlots.First(),
                            weaponWithoutTarget.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = targetUnit.Id,
                IsPrimaryTarget = true
            }
        };
        attackingUnit.DeclareWeaponAttack(
            attackingUnit.GetAllWeaponTargetsData().Concat(additionalWeaponTargets).ToList());

        // Configure dice rolls to ensure hits and specific hit locations
        // First roll (8) is for the first attack (hit)
        // Second roll (10) is for the first hit location (left arm)
        // Third roll (8) is for the second attack (hit)
        // Fourth roll (10) is for the second hit location (same location)
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
        capturedCommands[0].ResolutionData.HitLocationsData!.HitLocations[0].InitialLocation
            .ShouldBe(PartLocation.LeftArm);
        capturedCommands[1].ResolutionData.HitLocationsData!.HitLocations[0].InitialLocation
            .ShouldBe(PartLocation.LeftArm);

        // Calculate the total damage from both attacks
        var totalDamage = capturedCommands.Sum(cmd =>
            cmd.ResolutionData.HitLocationsData!.TotalDamage);

        var nextLocation = targetPart.GetNextTransferLocation()!.Value;
        var nextPart = targetUnit.Parts[nextLocation];

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
        var clusterWeapon = new TestClusterWeapon(1, 5);
        var part1 = _player1Unit1.Parts[PartLocation.LeftArm];
        part1.TryAddComponent(clusterWeapon).ShouldBeTrue();
        // Set a target for the cluster weapon
        var clusterWeaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = clusterWeapon.Name,
                    Type = clusterWeapon.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            clusterWeapon.MountedAtFirstLocationSlots.First(),
                            clusterWeapon.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(clusterWeaponTargets);

        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
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
                cmd.ResolutionData.HitLocationsData.HitLocations[0].InitialLocation == PartLocation.RightTorso &&
                cmd.ResolutionData.HitLocationsData.HitLocations[0].Damage[0].ArmorDamage == 5 && //first 5 missiles
                cmd.ResolutionData.HitLocationsData.HitLocations[1].Damage[0].ArmorDamage == 3)); //second 8-5=3
    }

    [Fact]
    public void Enter_ShouldCalculateCorrectDamage_ForClusterWeapon()
    {
        // Arrange
        SetMap();
        // Setup PSR for heavy damage 36 > 20 to avoid NRE
        var clusterWeapon = new TestClusterWeapon(6, 6, 1); 
        var part1 = _player1Unit1.Parts.Values.First(p=>p.Location == PartLocation.LeftArm);
        part1.TryAddComponent(clusterWeapon).ShouldBeTrue();
        // Set a target for the cluster weapon 
        var clusterWeaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = clusterWeapon.Name,
                    Type = clusterWeapon.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            clusterWeapon.MountedAtFirstLocationSlots.First(),
                            clusterWeapon.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(clusterWeaponTargets);

        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
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

    /// <summary>
    /// BUG REPRODUCTION TEST: This test demonstrates the bug where a mech receiving
    /// heavy damage (>= 20 points) through pure armor damage (no structure damage,
    /// no component hits, no destroyed parts) does NOT trigger a heavy damage PSR
    /// at the end of the weapon attack resolution phase.
    /// 
    /// Expected Behavior: FallProcessor.ProcessPotentialFall should be called at the end of phase
    /// Actual Behavior: FallProcessor.ProcessPotentialFall is NOT called because the unit
    ///                   is never added to _accumulatedDamageData
    /// </summary>
    [Fact]
    public void Enter_ShouldTriggerHeavyDamagePsr_WhenMechReceivesHeavyDamageWithoutComponentHits()
    {
        // Arrange
        SetMap();

        // Configure rules provider to return the heavy damage threshold of 20
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        mockRulesProvider.GetHeavyDamageThreshold().Returns(20);
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), Arg.Any<HitDirection>()).Returns(PartLocation.CenterTorso);
        SetGameWithRulesProvider(mockRulesProvider);
        // Create a high-damage weapon (20 points) to reach the heavy damage threshold
        var highDamageWeapon = new TestWeapon(damage: 20);
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(highDamageWeapon).ShouldBeTrue();
        // Set up weapon targets - player1 attacks player2
        var weaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = highDamageWeapon.Name,
                    Type = highDamageWeapon.ComponentType,
                    Assignments =
                    [
                        new LocationSlotAssignment(part1.Location,
                            highDamageWeapon.MountedAtFirstLocationSlots.First(),
                            highDamageWeapon.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(weaponTargets);
        // Set up ToHitCalculator to ensure the hit
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
            .Returns(7);
        // Setup dice rolls: attack hits (8), hits center torso (7)
        SetupDiceRolls(8, 7);
        // CRITICAL: Setup damage calculator to return ONLY armor damage (NO structure damage)
        // This is the key to reproducing the bug - armor damage only, no crits
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>(),
                Arg.Any<IReadOnlyList<LocationHitData>?>())
            .Returns(callInfo =>
            [
                new LocationDamageData(
                    callInfo.Arg<PartLocation>(),
                    callInfo.Arg<int>(), // All damage goes to armor
                    0, // NO structure damage - this prevents critical hits
                    false)
            ]);
        // Act
        _sut.Enter();
        // Assert
        // Verify that the target mech received the heavy damage
        var targetMech = _player2Unit1 as Mech;
        targetMech.ShouldNotBeNull();
        targetMech.TotalPhaseDamage.ShouldBeGreaterThanOrEqualTo(20,
            "Mech should have accumulated at least 20 points of damage");
        // BUG: This assertion FAILS because FallProcessor.ProcessPotentialFall is never called
        // The unit is not added to _accumulatedDamageData because there was no structure damage
        MockFallProcessor.Received().ProcessPotentialFall(
            Arg.Is<Mech>(m => m.Id == _player2Unit1.Id),
            Arg.Any<IGame>(),
            Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>>());
    }

    [Fact]
    public void Enter_ShouldNotRollForClusterHits_WhenClusterWeaponMisses()
    {
        // Arrange
        // Add a cluster weapon to unit1
        SetMap();
        var clusterWeapon = new TestClusterWeapon(10, 5); // LRM-10
        var part1 = _player1Unit1.Parts[PartLocation.LeftArm];
        part1.TryAddComponent(clusterWeapon).ShouldBeTrue();
        // Set a target for the cluster weapon 
        var clusterWeaponTargets = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = clusterWeapon.Name,
                    Type = clusterWeapon.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            clusterWeapon.MountedAtFirstLocationSlots.First(),
                            clusterWeapon.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(clusterWeaponTargets);

        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
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
    public void Enter_WhenBattleMapIsNull_ShouldThrowException()
    {
        // Arrange
        // Ensure BattleMap is null (default state after ServerGame creation in test base)
        Game.BattleMap.ShouldBeNull();
    
        // Set up weapon targets - necessary to reach the ResolveAttack call
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
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5, 10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        // Destroy the left arm
        leftArm.ApplyDamage(10, HitDirection.Front); // Apply enough damage to destroy it
        leftArm.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed

        var mech = new Mech("TestChassis", "TestModel", 50, [leftArm, leftTorso, centerTorso]);

        // Configure the rule provider to return LeftArm as the initial hit location
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftArm);

        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 for hit location roll
        );

        var sut = new WeaponAttackResolutionPhase(Game);

        // Act
        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech);

        // Assert
        // Should have transferred from LeftArm to LeftTorso (based on Mech's GetTransferLocation implementation)
        data.InitialLocation.ShouldBe(PartLocation.LeftArm);
        data.Damage[0].Location.ShouldBe(PartLocation.LeftTorso);
    }

    [Fact]
    public void DetermineHitLocation_ShouldTransferMultipleTimes_WhenMultipleLocationsInChainAreDestroyed()
    {
        // Arrange
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);

        // Create a mech with multiple parts including destroyed left arm and left torso
        var leftArm = new Arm("LeftArm", PartLocation.LeftArm, 5, 5);
        var leftTorso = new SideTorso("LeftTorso", PartLocation.LeftTorso, 10, 5, 10);
        var centerTorso = new CenterTorso("CenterTorso", 15, 10, 15);

        // Destroy the left arm and left torso
        leftArm.ApplyDamage(10, HitDirection.Front); // Apply enough damage to destroy it
        leftTorso.ApplyDamage(20, HitDirection.Front); // Apply enough damage to destroy it

        leftArm.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed
        leftTorso.IsDestroyed.ShouldBeTrue(); // Verify it's destroyed

        var mech = new Mech("TestChassis", "TestModel", 50, [leftArm, leftTorso, centerTorso]);

        // Configure the rule provider to return LeftArm as the initial hit location
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.LeftArm);

        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(5)] // 10 for hit location roll
        );

        var sut = new WeaponAttackResolutionPhase(Game);

        // Act
        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech);

        // Assert
        // Should have transferred from LeftArm to LeftTorso to CenterTorso
        data.Damage.Last().Location.ShouldBe(PartLocation.CenterTorso);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DetermineHitLocation_WithSuccessfulAimedShot_ShouldHitIntendedLocation(int secondD6)
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var mech = new MechFactory(
            _rulesProvider,
            _componentProvider,
            _localizationService).Create(mechData);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        mech.Shutdown(shutdownData);
        
        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(5), new DiceResult(secondD6)] // in 6-8 range
        );
        
        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = false,
            AimedShotTarget = PartLocation.LeftArm
        };

        var sut = new WeaponAttackResolutionPhase(Game);

        // Act
        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData);

        // Assert
        // Should hit the intended location (LeftArm) due to a successful aimed shot
        data.InitialLocation.ShouldBe(PartLocation.LeftArm);
        data.Damage[0].Location.ShouldBe(PartLocation.LeftArm);
    }
    
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void DetermineHitLocation_WithUnsuccessfulAimedShot_ShouldHitLocationByTable(int secondD6)
    {
        // Arrange
        var mockRulesProvider = Substitute.For<IRulesProvider>();
        SetGameWithRulesProvider(mockRulesProvider);
        var mechData = MechFactoryTests.CreateDummyMechData();
        var mech = new MechFactory(
            _rulesProvider,
            _componentProvider,
            _localizationService).Create(mechData);
        var shutdownData = new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 };
        mech.Shutdown(shutdownData);

        // Configure the rule provider to return LeftArm as the initial hit location
        mockRulesProvider.GetHitLocation(Arg.Any<int>(), HitDirection.Front).Returns(PartLocation.CenterTorso);

        // Configure aimed shot success values
        mockRulesProvider.GetAimedShotSuccessValues().Returns([6, 7, 8]);

        // Configure dice rolls for hit location
        DiceRoller.Roll2D6().Returns(
            [new DiceResult(4), new DiceResult(secondD6)] // outside the 6-8 range
        );
        
        var weaponTargetData = new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = Guid.NewGuid(),
            IsPrimaryTarget = false,
            AimedShotTarget = PartLocation.LeftArm
        };

        var sut = new WeaponAttackResolutionPhase(Game);

        // Act
        var data = InvokeDetermineHitLocation(sut, HitDirection.Front, 5, mech, weaponTargetData);

        // Assert
        // Should hit location from the table (CenterTorso) due to unsuccessful aimed shot
        data.InitialLocation.ShouldBe(PartLocation.CenterTorso);
        data.Damage[0].Location.ShouldBe(PartLocation.CenterTorso);
    }
    
    [Fact]
    public void Enter_ShouldTrackDestroyedParts_WhenApplyingDamage()
    {
        // Arrange - Setup weapon targets
        SetupPlayer1WeaponTargets();

        // Get the target unit (player2's unit)
        var targetUnit = _player2Unit1;

        // Get the part we want to target (left arm)
        var targetPart = targetUnit.Parts[PartLocation.LeftArm];

        // Apply damage to the part to leave it with minimal structure
        // This way the next attack will destroy it
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;

        // Apply damage to leave only 1 structure point
        targetPart.ApplyDamage(initialArmor + initialStructure - 1, HitDirection.Front);

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
        var targetPart = targetUnit.Parts[PartLocation.CenterTorso];

        // Apply damage to the center torso to leave it with minimal structure
        // This way the next attack will destroy the unit
        var initialArmor = targetPart.CurrentArmor;
        var initialStructure = targetPart.CurrentStructure;

        // Apply damage to leave only 1 structure point
        targetPart.ApplyDamage(initialArmor + initialStructure - 1, HitDirection.Front);

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
    public void PublishCommand_ShouldApplyDamageAndExternalHeat()
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
        var externalHeat = _player2Unit1.GetHeatData(_rulesProvider).ExternalHeatPoints;
        externalHeat.ShouldBe(2);
    }

    [Fact]
    public void Enter_ShouldPublishMechFallingCommand_WhenFallProcessorReturnsCommand()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits
        
        // Configure the MockFallProcessor to return MechFallingCommands
        var mechFallingCommand = new MechFallCommand
        {
            UnitId = _player1Unit1.Id,
            LevelsFallen = 0,
            WasJumping = false,
            GameOriginId = Game.Id,
            DamageData = null
        };

        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand> { mechFallingCommand });
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that FallProcessor.ProcessPotentialFall was called
        MockFallProcessor.Received().ProcessPotentialFall(
            Arg.Is<Mech>(u => u == _player1Unit1), // Target unit
            Arg.Is<IGame>(m => m == Game),
            Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>>());
        
        // Verify that the MechFallingCommand was published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => 
                cmd.UnitId == _player1Unit1.Id && 
                cmd.GameOriginId == Game.Id));
    }
    
    [Fact]
    public void Enter_ShouldApplyDamageAndSetProne_WhenMechFallingCommandHasDamageData()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits
        
        var damageData = new FallingDamageData(HexDirection.Bottom,
            new HitLocationsData(
                HitLocations: [
                CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[])],
                TotalDamage: 5), new DiceResult(3), HitDirection.Front);

        var mechFallingCommand = new MechFallCommand
        {
            UnitId = _player1Unit1.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = damageData,
            GameOriginId = Game.Id
        };

        MockFallProcessor.ProcessPotentialFall(
                Arg.Is<Mech>(m => m.Id == _player1Unit1.Id), // ← Only for player1Unit1
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand> { mechFallingCommand });
        // For other units, return empty
        MockFallProcessor.ProcessPotentialFall(
                Arg.Is<Mech>(m => m.Id != _player1Unit1.Id),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand>());
        
        // Get initial armor value to verify damage is applied
        var targetPart = _player1Unit1.Parts[PartLocation.CenterTorso];
        var initialArmor = targetPart.CurrentArmor;
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that damage was applied to the target
        targetPart.CurrentArmor.ShouldBe(initialArmor - 5);
        
        // Verify that the mech was set to prone
        var mech = _player1Unit1 as Mech;
        mech!.Status.ShouldHaveFlag(UnitStatus.Prone);
        mech.StandupAttempts.ShouldBe(0);
    }
    
    [Fact]
    public void Enter_ShouldPublishConsciousnessCommand_WhenMechFallingAndPilotTakesDamage()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits
        
        var damageData = new FallingDamageData(HexDirection.Bottom,
            new HitLocationsData(
                HitLocations: [
                    CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[])],
                TotalDamage: 5), new DiceResult(3), HitDirection.Front);

        var mechFallingCommand = new MechFallCommand
        {
            UnitId = _player1Unit1.Id,
            LevelsFallen = 0,
            WasJumping = false,
            DamageData = damageData,
            GameOriginId = Game.Id
        };

        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand> { mechFallingCommand });
        
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = _player1Unit1.Pilot!.Id,
            UnitId = _player1Unit1.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [7, 2],
            IsSuccessful = false 
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(_player1Unit1.Pilot!)
            .Returns([consciousnessCommand]);
        
        // Act
        _sut.Enter();

        // Assert
        // Consciousness rolls are published once per attack after both weapon attack and critical hits commands
        // There are 2 attacks total (player1 and player2), so we expect 2 consciousness roll commands
        CommandPublisher.Received(2).PublishCommand(
            Arg.Is<PilotConsciousnessRollCommand>(cmd =>
                cmd.GameOriginId == Game.Id &&
                cmd.IsRecoveryAttempt == false &&
                cmd.IsSuccessful == false));
    }
    
    [Fact]
    public void Enter_ShouldNotPublishMechFallingCommands_WhenNoFallConditionsAreMet()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 6); // Set up dice rolls to ensure hits
        
        // Configure MockFallProcessor to return an empty list (no fall conditions met)
        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand>());
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that no MechFallingCommand was published
        CommandPublisher.DidNotReceive().PublishCommand(Arg.Any<MechFallCommand>());
    }

    // Helper to invoke private method
    private static LocationHitData InvokeDetermineHitLocation(WeaponAttackResolutionPhase phase, HitDirection hitDirection, int dmg,
        Unit? target, WeaponTargetData? weaponTargetData = null)
    {
        weaponTargetData ??= new WeaponTargetData
        {
            Weapon = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 2)]
            },
            TargetId = target?.Id ?? Guid.NewGuid(),
            IsPrimaryTarget = false
        };
        var weapon = new TestWeapon();
        var method = typeof(WeaponAttackResolutionPhase).GetMethod("DetermineHitLocation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (LocationHitData)method!.Invoke(phase, [hitDirection, dmg, target, weapon, weaponTargetData, null])!;
    }
    
    private void SetupPlayer1WeaponTargets()
    {
        // Add a weapon to each unit
        var weapon1 = new TestWeapon();
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(weapon1).ShouldBeTrue();

        // Set up weapon targets using the new system
        var weaponTargets1 = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = weapon1.Name,
                    Type = weapon1.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            weapon1.MountedAtFirstLocationSlots.First(),
                            weapon1.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(weaponTargets1);

        var weapon2 = new TestWeapon();
        var part2 = _player2Unit1.Parts[0];
        part2.TryAddComponent(weapon2).ShouldBeTrue();

        var weaponTargets2 = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = weapon2.Name,
                    Type = weapon2.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part2.Location,
                            weapon2.MountedAtFirstLocationSlots.First(),
                            weapon2.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player1Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player2Unit1.DeclareWeaponAttack(weaponTargets2);

        // Add a third weapon without a target to test that it's properly skipped
        var weaponWithoutTarget = new TestWeapon();
        var part3 = _player1Unit1.Parts.Values.Skip(1).First(); // Using the second part of unit1
        part3.TryAddComponent(weaponWithoutTarget).ShouldBeTrue();
        // Deliberately not setting a target for this weapon

        // Setup ToHitCalculator to return a value
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
            .Returns(7); // Return a default to-hit number of 7
    }
    
    private void SetupCriticalHitsFor(
        MakaMekComponent component,
        int slot,
        PartLocation location,
        IUnit unit)
    {
        // Set up a structure damage calculator to return structure damage
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>())
            .Returns([new LocationDamageData(location, 3, 2, false)]);

        // Setup critical hits calculator to return critical hits that cause falls
        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.Empty,
            TargetId = unit.Id,
            CriticalHits = [new LocationCriticalHitsData(
                location,
                [4, 5],
                1,
                [new ComponentHitData { Type = component, Slot = slot }],
                false)]
        };

        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(
                Arg.Is<Unit>(u => u.Id == unit.Id),
                Arg.Any<List<LocationDamageData>>())
            .Returns(criticalHitsCommand);
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

    private class TestWeapon(WeaponType type = WeaponType.Energy, MakaMekComponent? ammoType = null, int damage = 5)
        : Weapon(new WeaponDefinition(
            "Test Weapon", damage, 3,
            0, 3, 6, 9,
            type, 10, 1, 1, 1, 1, MakaMekComponent.MachineGun, ammoType, 2));

    // Custom cluster weapon class that allows setting damage for testing
    private class TestClusterWeapon(
        int damage = 10,
        int clusterSize = 1,
        int clusters = 2,
        WeaponType type = WeaponType.Missile,
        MakaMekComponent? ammoType = null)
        : Weapon(new WeaponDefinition(
            "Test Cluster Weapon", damage, 3,
            0, 3, 6, 9,
            type, 10, clusters, clusterSize, 1, 1, MakaMekComponent.LRM10, ammoType));

    [Fact]
    public void Enter_ShouldCallCriticalHitsCalculatorAndPublishCommand_WhenStructureDamageOccurs()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits

        // Set up a structure damage calculator to return structure damage
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>())
            .Returns([new LocationDamageData(PartLocation.CenterTorso, 3, 2, false)]);

        // Setup critical hits calculator to return a command
        var expectedCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Game.Id,
            TargetId = _player2Unit1.Id,
            CriticalHits = [new LocationCriticalHitsData(
                PartLocation.CenterTorso,
                [4, 5],
                1,
                [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
                false)]
        };

        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(
                Arg.Any<Unit>(),
                Arg.Any<List<LocationDamageData>>())
            .Returns(expectedCommand);

        // Act
        _sut.Enter();

        // Assert
        // Critical hits calculator should be called with hit locations data
        // Note: Due to initiative order, Player 2 attacks first, so _player1Unit1 is the first target
        MockCriticalHitsCalculator.Received().CalculateAndApplyCriticalHits(
            Arg.Any<Unit>(),
            Arg.Any<List<LocationDamageData>>());

        // The command returned by calculator should be published
        CommandPublisher.Received().PublishCommand(
            Arg.Any<CriticalHitsResolutionCommand>());
    }

    [Fact]
    public void Enter_ShouldAccumulateBlownOffLegAndTriggerPsr_WhenLegIsBlownOff()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits

        // Set up a structure damage calculator to return structure damage for the left leg
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>())
            .Returns([new LocationDamageData(PartLocation.LeftLeg, 3, 2, false)]);

        // Setup critical hits calculator to return a blown-off leg
        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Game.Id,
            TargetId = _player2Unit1.Id,
            CriticalHits = [new LocationCriticalHitsData(
                PartLocation.LeftLeg,
                [6, 6], // Roll of 12 for blown-off
                3,
                null, // No component hits when blown off
                true)] // IsBlownOff = true
        };

        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(
                Arg.Any<Unit>(),
                Arg.Any<List<LocationDamageData>>())
            .Returns(criticalHitsCommand);
        
        // Act
        _sut.Enter();

        // Assert
        // Verify that FallProcessor was called with the blown-off leg in destroyed parts
        MockFallProcessor.Received().ProcessPotentialFall(
            Arg.Any<Mech>(),
            Arg.Any<IGame>(),
            Arg.Any<List<ComponentHitData>>(),
            Arg.Is<List<PartLocation>>(parts => parts.Contains(PartLocation.LeftLeg)));
    }

    [Fact]
    public void Enter_ShouldPublishCriticalHitsCommand_WhenFallProcessorReturnsStructureDamage()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits
        
        // Configure the MockFallProcessor to return MechFallingCommands
        var mechFallingCommand = new MechFallCommand
        {
            UnitId = _player1Unit1.Id,
            LevelsFallen = 0,
            WasJumping = false,
            GameOriginId = Game.Id,
            DamageData = new FallingDamageData(HexDirection.Bottom,
                new HitLocationsData(
                    HitLocations: [
                        new LocationHitData(
                            [new LocationDamageData(PartLocation.LeftTorso, 3, 2, false)], // Structure damage
                            [],
                            [3, 4],
                            PartLocation.LeftLeg)
                    ],
                    TotalDamage: 8), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand> { mechFallingCommand });
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that FallProcessor.ProcessPotentialFall was called
        MockFallProcessor.Received().ProcessPotentialFall(
            Arg.Is<Mech>(u => u == _player1Unit1), // Target unit
            Arg.Is<IGame>(m => m == Game),
            Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>>());
        
        // Verify that the MechFallingCommand was published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => 
                cmd.UnitId == _player1Unit1.Id && 
                cmd.GameOriginId == Game.Id));
        
        // Verify that critical hits calculator was called for fall damage
        MockCriticalHitsCalculator.Received().CalculateAndApplyCriticalHits(
            Arg.Is<Unit>(u => u.Id == _player1Unit1.Id),
            Arg.Is<List<LocationDamageData>>(list => list.Any(d => d.Location == PartLocation.LeftTorso)));
        
        // Verify that 2 critical hits commands were published (initial for weapon damage and for falling damage)
        CommandPublisher.Received(2).PublishCommand(
            Arg.Is<CriticalHitsResolutionCommand>(cmd =>
                cmd.TargetId == _player1Unit1.Id &&
                cmd.GameOriginId == Game.Id ));
    }
    
    [Fact]
    public void Enter_ShouldNotPublishCriticalHitsCommand_WhenFallProcessorReturnsNoStructureDamage()
    {
        // Arrange
        SetMap();
        SetupPlayer1WeaponTargets();
        SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
        SetupDiceRolls(8, 9, 4); // Set up dice rolls to ensure hits
        
        // Configure the MockFallProcessor to return MechFallingCommands
        var mechFallingCommand = new MechFallCommand
        {
            UnitId = _player1Unit1.Id,
            LevelsFallen = 0,
            WasJumping = false,
            GameOriginId = Game.Id,
            DamageData = new FallingDamageData(HexDirection.Bottom,
                new HitLocationsData(
                    HitLocations: [
                        new LocationHitData(
                            [new LocationDamageData(PartLocation.LeftTorso, 3, 0, false)], // No structure damage
                            [],
                            [3, 4],
                            PartLocation.LeftLeg)
                    ],
                    TotalDamage: 8), new DiceResult(3), HitDirection.Front)
        };

        MockFallProcessor.ProcessPotentialFall(
                Arg.Any<Mech>(),
                Arg.Any<IGame>(),
                Arg.Any<List<ComponentHitData>>(),
                Arg.Any<List<PartLocation>>())
            .Returns(new List<MechFallCommand> { mechFallingCommand });
        
        // Act
        _sut.Enter();
        
        // Assert
        // Verify that FallProcessor.ProcessPotentialFall was called
        MockFallProcessor.Received().ProcessPotentialFall(
            Arg.Is<Mech>(u => u == _player1Unit1), // Target unit
            Arg.Is<IGame>(m => m == Game),
            Arg.Any<List<ComponentHitData>>(),
            Arg.Any<List<PartLocation>>());
        
        // Verify that the MechFallingCommand was published
        CommandPublisher.Received().PublishCommand(
            Arg.Is<MechFallCommand>(cmd => 
                cmd.UnitId == _player1Unit1.Id && 
                cmd.GameOriginId == Game.Id));
        
        // Verify that the critical hits calculator was not called for fall damage
        MockCriticalHitsCalculator.DidNotReceive().CalculateAndApplyCriticalHits(
            Arg.Is<Unit>(u => u.Id == _player1Unit1.Id),
            Arg.Is<List<LocationDamageData>>(list => list.Any(d => d.Location == PartLocation.LeftTorso)));
        
        // Verify that only 1 critical hits commands were published (initial for weapon damage)
        CommandPublisher.Received(1).PublishCommand(
            Arg.Is<CriticalHitsResolutionCommand>(cmd =>
                cmd.TargetId == _player1Unit1.Id &&
                cmd.GameOriginId == Game.Id ));
    }

    [Fact]
    public void Enter_ShouldNotPublishCriticalHitsCommand_WhenNoStructureDamage()
    {
        // Arrange
        SetMap();

        // Set up a single weapon attack from player1 to player2
        var weapon1 = new TestWeapon();
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(weapon1).ShouldBeTrue();

        var weaponTargets1 = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = weapon1.Name,
                    Type = weapon1.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            weapon1.MountedAtFirstLocationSlots.First(),
                            weapon1.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(weaponTargets1);

        // Setup ToHitCalculator
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
            .Returns(7);

        // Setup dice rolls: attack hits (8), hits head (12), but no structure damage
        SetupDiceRolls(8, 12);

        // Set up a damage calculator to return only armor damage (no structure damage)
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>())
            .Returns(callInfo => [new LocationDamageData(callInfo.Arg<PartLocation>(), callInfo.Arg<int>(), 0, false)]);
        
        // Set up a consciousness calculator to return a consciousness roll command
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = _player2Unit1.Pilot!.Id,
            UnitId = _player2Unit1.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 3,
            DiceResults = [5, 4],
            IsSuccessful = true
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(_player2Unit1.Pilot!)
            .Returns([consciousnessCommand]);
        
        // Act
        _sut.Enter();

        // Assert
        var publishedCommands = CommandPublisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "PublishCommand")
            .Select(call => (IGameCommand)call.GetArguments()[0]!)
            .ToList();
        
        publishedCommands.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Find the indices of the relevant commands
        var weaponAttackIndex = publishedCommands.FindIndex(cmd =>
            cmd is WeaponAttackResolutionCommand warc && warc.AttackerId == _player1Unit1.Id);
        var consciousnessIndex = publishedCommands.FindIndex(cmd =>
            cmd is PilotConsciousnessRollCommand pilotConsciousnessRollCommand && pilotConsciousnessRollCommand.UnitId == _player2Unit1.Id);

        // Verify all commands were published
        weaponAttackIndex.ShouldBeGreaterThanOrEqualTo(0, "WeaponAttackResolutionCommand should be published");
        consciousnessIndex.ShouldBeGreaterThanOrEqualTo(0, "PilotConsciousnessRollCommand should be published");

        // Verify the correct order: WeaponAttackResolution -> PilotConsciousnessRoll (no CriticalHitsResolution when no structure damage)
        weaponAttackIndex.ShouldBeLessThan(consciousnessIndex,
            "WeaponAttackResolutionCommand should be published before PilotConsciousnessRollCommand");

        // Verify that no critical hits command was published
        CommandPublisher.DidNotReceive().PublishCommand(
            Arg.Is<CriticalHitsResolutionCommand>(cmd => 
                cmd.TargetId == _player2Unit1.Id &&
                cmd.GameOriginId == Game.Id));
    }

    [Fact]
    public void Enter_ShouldPublishCommandsInCorrectOrder_WhenHeadHitCausesPilotDamage()
    {
        // Arrange
        SetMap();

        // Set up a single weapon attack from player1 to player2
        var weapon1 = new TestWeapon();
        var part1 = _player1Unit1.Parts[0];
        part1.TryAddComponent(weapon1).ShouldBeTrue();

        var weaponTargets1 = new List<WeaponTargetData>
        {
            new()
            {
                Weapon = new ComponentData
                {
                    Name = weapon1.Name,
                    Type = weapon1.ComponentType,
                    Assignments = [
                        new LocationSlotAssignment(part1.Location,
                            weapon1.MountedAtFirstLocationSlots.First(),
                            weapon1.MountedAtFirstLocationSlots.Length)
                    ]
                },
                TargetId = _player2Unit1.Id,
                IsPrimaryTarget = true
            }
        };
        _player1Unit1.DeclareWeaponAttack(weaponTargets1);

        // Setup ToHitCalculator
        Game.ToHitCalculator.GetToHitNumber(
                Arg.Any<Unit>(),
                Arg.Any<Unit>(),
                Arg.Any<Weapon>(),
                Arg.Any<BattleMap>(),
                Arg.Any<bool>(),
                Arg.Any<PartLocation?>())
            .Returns(7);

        // Setup dice rolls: attack hits (8), hits head (12), structure damage causes critical hit
        SetupDiceRolls(8, 12);

        // Set up a damage calculator to return head damage with structure damage
        MockDamageTransferCalculator.CalculateStructureDamage(
                Arg.Any<Unit>(),
                Arg.Any<PartLocation>(),
                Arg.Any<int>(),
                Arg.Any<HitDirection>())
            .Returns(callInfo =>
            {
                var location = callInfo.Arg<PartLocation>();
                return location == PartLocation.Head
                    ? [new LocationDamageData(PartLocation.Head, 3, 1, false)]
                    : [new LocationDamageData(location, callInfo.Arg<int>(), 0, false)];
            });

        // Set up a critical hits calculator to return a critical hit
        var criticalHitsCommand = new CriticalHitsResolutionCommand
        {
            GameOriginId = Game.Id,
            TargetId = _player2Unit1.Id,
            CriticalHits = [new LocationCriticalHitsData(
                PartLocation.Head,
                [4, 5],
                1,
                [new ComponentHitData { Type = MakaMekComponent.Sensors, Slot = 1 }],
                false)]
        };

        MockCriticalHitsCalculator.CalculateAndApplyCriticalHits(
                Arg.Any<Unit>(),
                Arg.Any<List<LocationDamageData>>())
            .Returns(criticalHitsCommand);

        // Set up a consciousness calculator to return a consciousness roll command
        var consciousnessCommand = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = _player2Unit1.Pilot!.Id,
            UnitId = _player2Unit1.Id,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 3,
            DiceResults = [5, 4],
            IsSuccessful = true
        };
        MockConsciousnessCalculator.MakeConsciousnessRolls(_player2Unit1.Pilot!)
            .Returns([consciousnessCommand]);

        // Act
        _sut.Enter();

        // Assert
        var publishedCommands = CommandPublisher.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == "PublishCommand")
            .Select(call => (IGameCommand)call.GetArguments()[0]!)
            .ToList();
        
        // Verify we have at least 3 commands published
        publishedCommands.Count.ShouldBeGreaterThanOrEqualTo(3);

        // Find the indices of the relevant commands
        var weaponAttackIndex = publishedCommands.FindIndex(cmd =>
            cmd is WeaponAttackResolutionCommand warc && warc.AttackerId == _player1Unit1.Id);
        var criticalHitsIndex = publishedCommands.FindIndex(cmd =>
            cmd is CriticalHitsResolutionCommand criticalHitsResolutionCommand && criticalHitsResolutionCommand.TargetId == _player2Unit1.Id);
        var consciousnessIndex = publishedCommands.FindIndex(cmd =>
            cmd is PilotConsciousnessRollCommand pilotConsciousnessRollCommand && pilotConsciousnessRollCommand.UnitId == _player2Unit1.Id);

        // Verify all commands were published
        weaponAttackIndex.ShouldBeGreaterThanOrEqualTo(0, "WeaponAttackResolutionCommand should be published");
        criticalHitsIndex.ShouldBeGreaterThanOrEqualTo(0, "CriticalHitsResolutionCommand should be published");
        consciousnessIndex.ShouldBeGreaterThanOrEqualTo(0, "PilotConsciousnessRollCommand should be published");

        // Verify the correct order: WeaponAttackResolution -> CriticalHitsResolution -> PilotConsciousnessRoll
        weaponAttackIndex.ShouldBeLessThan(criticalHitsIndex,
            "WeaponAttackResolutionCommand should be published before CriticalHitsResolutionCommand");
        criticalHitsIndex.ShouldBeLessThan(consciousnessIndex,
            "CriticalHitsResolutionCommand should be published before PilotConsciousnessRollCommand");
    }
}
