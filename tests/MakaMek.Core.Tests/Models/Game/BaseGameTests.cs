using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class BaseGameTests : BaseGame
{
    private static readonly IRulesProvider RulesProviderInstance = new ClassicBattletechRulesProvider();
    private static readonly IComponentProvider ComponentProviderInstance = new ClassicBattletechComponentProvider();
    public BaseGameTests() : base(
        RulesProviderInstance,
        new MechFactory(
            RulesProviderInstance,
            ComponentProviderInstance,
            Substitute.For<ILocalizationService>()),
        Substitute.For<ICommandPublisher>(),
        Substitute.For<IToHitCalculator>(),
        Substitute.For<IPilotingSkillCalculator>(),
        Substitute.For<IConsciousnessCalculator>(),
        Substitute.For<IHeatEffectsCalculator>())
    {
        base.SetBattleMap(
            BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5, 5, new ClearTerrain())));
    }
    
    public override bool IsDisposed => false;
    
    private static LocationHitData CreateHitDataForLocation(PartLocation partLocation,
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

    [Fact]
    public void AddPlayer_ShouldAddPlayer_WhenJoinGameCommandIsReceived()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units =
            [
            ],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        // Act
        OnPlayerJoined(joinCommand);

        // Assert
        Players.Count.ShouldBe(1);
        Players[0].ControlType.ShouldBe(PlayerControlType.Remote);
    }

    [Fact]
    public void New_ShouldHaveCorrectTurnAndPhase()
    {
        Turn.ShouldBe(1);
        TurnPhase.ShouldBe(PhaseNames.Start);
    }

    [Fact]
    public void OnWeaponConfiguration_DoesNothing_WhenPlayerNotFound()
    {
        // Arrange
        var command = new WeaponConfigurationCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)HexDirection.Bottom
            }
        };

        // Act
        OnWeaponConfiguration(command);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public void OnWeaponConfiguration_DoesNothing_WhenUnitNotFound()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();
        var command = new WeaponConfigurationCommand
        {
            GameOriginId = Id,
            PlayerId = player.Id,
            UnitId = Guid.NewGuid(),
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)HexDirection.Bottom
            }
        };

        // Act
        OnWeaponConfiguration(command);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public void OnMechFalling_DoesNothing_WhenNoDamageData()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var command = new MechFallCommand
        {
            GameOriginId = Id,
            UnitId = Guid.NewGuid(),
            DamageData = null
        };

        // Act
        OnMechFalling(command);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public void OnWeaponConfiguration_RotatesTorso_WhenConfigurationIsTorsoRotation()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();
        var mech = player.Units.First() as Mech;
        mech?.Deploy(new HexPosition(new HexCoordinates(3, 3), HexDirection.BottomLeft));

        var command = new WeaponConfigurationCommand
        {
            GameOriginId = Id,
            PlayerId = player.Id,
            UnitId = mech!.Id,
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)HexDirection.Bottom
            }
        };

        // Act
        OnWeaponConfiguration(command);

        // Assert
        mech.TorsoDirection.ShouldBe(HexDirection.Bottom);
    }

    [Fact]
    public void OnWeaponsAttack_DoesNothing_WhenPlayerNotFound()
    {
        // Arrange
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            WeaponTargets = []
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public void OnWeaponsAttack_DoesNothing_WhenAttackerNotFound()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();

        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Id,
            PlayerId = player.Id,
            UnitId = Guid.NewGuid(),
            WeaponTargets = []
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        // No exception should be thrown
    }

    [Fact]
    public void OnWeaponsAttack_ShouldDeclareWeaponAttack_WhenAttackerAndTargetsFound()
    {
        // Arrange
        // Add attacker player and unit
        var attackerPlayerId = Guid.NewGuid();
        var attackerUnitData = MechFactoryTests.CreateDummyMechData();
        attackerUnitData.Id = Guid.NewGuid();
        var attackerJoinCommand = new JoinGameCommand
        {
            PlayerId = attackerPlayerId,
            PlayerName = "Attacker",
            GameOriginId = Guid.NewGuid(),
            Units = [attackerUnitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units[0] as Mech;
        attackerMech!.Parts[PartLocation.RightArm].TryAddComponent(new MediumLaser()).ShouldBeTrue();
        attackerMech.AssignPilot(new MechWarrior("John", "Doe"));
        attackerMech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Add a target player and unit
        var targetPlayerId = Guid.NewGuid();
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        var targetJoinCommand = new JoinGameCommand
        {
            PlayerId = targetPlayerId,
            PlayerName = "Target",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));

        // Get a weapon from the attacker mech
        var weapon = attackerMech.GetComponentsAtLocation<Weapon>(PartLocation.RightArm).FirstOrDefault();
        weapon.ShouldNotBeNull();

        // Create the attack command
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Id,
            PlayerId = attackerPlayerId,
            UnitId = attackerMech.Id,
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = weapon.ToData(),
                    TargetId = targetMech.Id,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        attackerMech.HasDeclaredWeaponAttack.ShouldBeTrue();
        attackerMech.GetAllWeaponTargetsData().Count.ShouldBe(1);
    }

    [Fact]
    public void OnWeaponsAttack_ShouldHandleMultipleTargets()
    {
        // Arrange
        // Add attacker player and unit
        var attackerPlayerId = Guid.NewGuid();
        var attackerUnitData = MechFactoryTests.CreateDummyMechData();
        attackerUnitData.Id = Guid.NewGuid();
        var attackerJoinCommand = new JoinGameCommand
        {
            PlayerId = attackerPlayerId,
            PlayerName = "Attacker",
            GameOriginId = Guid.NewGuid(),
            Units = [attackerUnitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units.First() as Mech;
        attackerMech!.Parts[PartLocation.RightArm].TryAddComponent(new MediumLaser()).ShouldBeTrue();
        attackerMech.Parts[PartLocation.LeftArm].TryAddComponent(new MediumLaser()).ShouldBeTrue();
        attackerMech.AssignPilot(new MechWarrior("John", "Doe"));
        attackerMech.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));

        // Add first target player and unit
        var targetPlayerId1 = Guid.NewGuid();
        var targetUnitData1 = MechFactoryTests.CreateDummyMechData();
        targetUnitData1.Id = Guid.NewGuid();
        var targetUnitData2 = MechFactoryTests.CreateDummyMechData();
        targetUnitData2.Id = Guid.NewGuid();
        var targetJoinCommand1 = new JoinGameCommand
        {
            PlayerId = targetPlayerId1,
            PlayerName = "Target1",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData1, targetUnitData2],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        OnPlayerJoined(targetJoinCommand1);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId1);
        var targetMech1 = targetPlayer.Units[0] as Mech;
        targetMech1!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        var targetMech2 = targetPlayer.Units[1] as Mech;
        targetMech2!.Deploy(new HexPosition(new HexCoordinates(1, 3), HexDirection.Top));

        // Get weapons from the attacker mech
        var rightArmWeapon = attackerMech.GetComponentsAtLocation<Weapon>(PartLocation.RightArm).FirstOrDefault();
        var leftArmWeapon = attackerMech.GetComponentsAtLocation<Weapon>(PartLocation.LeftArm).FirstOrDefault();
        rightArmWeapon.ShouldNotBeNull();
        leftArmWeapon.ShouldNotBeNull();

        // Create the attack command
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Id,
            PlayerId = attackerPlayerId,
            UnitId = attackerMech.Id,
            WeaponTargets =
            [
                new WeaponTargetData
                {
                    Weapon = rightArmWeapon.ToData(),
                    TargetId = targetMech1.Id,
                    IsPrimaryTarget = true
                },
                new WeaponTargetData
                {
                    Weapon = leftArmWeapon.ToData(),
                    TargetId = targetMech2.Id,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        attackerMech.HasDeclaredWeaponAttack.ShouldBeTrue();
        attackerMech.GetAllWeaponTargetsData().Count.ShouldBe(2);
    }

    [Fact]
    public void OnWeaponsAttackResolution_DoesNothing_WhenTargetNotFound()
    {
        // Arrange
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            AttackerId = Guid.NewGuid(),
            TargetId = Guid.NewGuid(),
            WeaponData = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 0, 2)]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                HitDirection.Front,
                0,
                new AttackHitLocationsData([], 0, [], 0))
        };

        // Act & Assert
        // No exception should be thrown
        var action = () => OnWeaponsAttackResolution(command);
        action.ShouldNotThrow();
    }

    [Fact]
    public void OnWeaponsAttackResolution_DoesNotApplyDamage_WhenAttackMissed()
    {
        // Arrange
        // Add target player and unit
        var targetPlayerId = Guid.NewGuid();
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        var targetJoinCommand = new JoinGameCommand
        {
            PlayerId = targetPlayerId,
            PlayerName = "Target",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units[0] as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));

        // Create hit locations data
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[]),
            CreateHitDataForLocation(PartLocation.LeftArm, 3, [],[])
        };

        // Create the attack resolution command with IsHit = false
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            AttackerId = Guid.NewGuid(),
            TargetId = targetMech.Id,
            WeaponData = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 0, 2)]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                false, // Attack missed
                HitDirection.Front,
                0,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Get initial armor values for verification
        var centerTorsoPart = targetMech.Parts[PartLocation.CenterTorso];
        var leftArmPart = targetMech.Parts[PartLocation.LeftArm];
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;

        // Act
        OnWeaponsAttackResolution(command);

        // Assert
        // Verify that armor was not reduced
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor);
    }

    [Fact]
    public void OnWeaponsAttackResolution_DoesNothing_WhenAttackerNotFound()
    {
        // Arrange
        // Add target player and unit
        var targetPlayerId = Guid.NewGuid();
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        var targetJoinCommand = new JoinGameCommand
        {
            PlayerId = targetPlayerId,
            PlayerName = "Target",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));

        // Create the attack resolution command with a non-existent attacker ID
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            AttackerId = Guid.NewGuid(), // Non-existent attacker
            TargetId = targetMech.Id,
            WeaponData = new ComponentData
            {
                Name = "Test Weapon",
                Type = MakaMekComponent.MachineGun,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 0, 2)]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                HitDirection.Front,
                0,
                new AttackHitLocationsData([], 0, [], 0))
        };

        // Act & Assert
        // No exception should be thrown
        var action = () => OnWeaponsAttackResolution(command);
        action.ShouldNotThrow();
    }

    [Fact]
    public void OnWeaponsAttackResolution_ShouldFireWeaponAndApplyDamageAndExternalHeat()
    {
        // Arrange
        // Add attacker player and unit
        var attackerPlayerId = Guid.NewGuid();
        var attackerUnitData = MechFactoryTests.CreateDummyMechData();
        attackerUnitData.Id = Guid.NewGuid();
        var attackerJoinCommand = new JoinGameCommand
        {
            PlayerId = attackerPlayerId,
            PlayerName = "Attacker",
            GameOriginId = Guid.NewGuid(),
            Units = [attackerUnitData],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units.First() as Mech;
        attackerMech!.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        attackerMech.Parts[PartLocation.RightArm].TryAddComponent(new MediumLaser()).ShouldBeTrue();

        // Add a target player and unit
        var targetPlayerId = Guid.NewGuid();
        var targetUnitData = MechFactoryTests.CreateDummyMechData();
        targetUnitData.Id = Guid.NewGuid();
        var targetJoinCommand = new JoinGameCommand
        {
            PlayerId = targetPlayerId,
            PlayerName = "Target",
            GameOriginId = Guid.NewGuid(),
            Units = [targetUnitData],
            Tint = "#00FF00",
            PilotAssignments = []
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));

        // Get a weapon from the attacker mech
        var weapon = attackerMech.GetComponentsAtLocation<Weapon>(PartLocation.RightArm).FirstOrDefault();
        weapon.ShouldNotBeNull();

        // Create hit locations data
        var hitLocations = new List<LocationHitData>
        {
            CreateHitDataForLocation(PartLocation.CenterTorso, 5, [],[]),
            CreateHitDataForLocation(PartLocation.LeftArm, 3, [],[])
        };

        // Get initial values for verification
        var centerTorsoPart = targetMech.Parts[PartLocation.CenterTorso];
        var leftArmPart = targetMech.Parts[PartLocation.LeftArm];
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;

        // Create the attack resolution command
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = attackerPlayerId,
            AttackerId = attackerMech.Id,
            TargetId = targetMech.Id,
            WeaponData =weapon.ToData(),
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                HitDirection.Front,
                2,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Act
        OnWeaponsAttackResolution(command);

        // Assert
        // Verify that damage was applied to the target
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor - 3);
        targetMech.GetHeatData(RulesProviderInstance).ExternalHeatPoints.ShouldBe(2);
    }

    [Fact]
    public void AlivePlayers_ShouldReturnOnlyReadyPlayersWithAliveUnits()
    {
        var mechFactory = new MechFactory(
            RulesProviderInstance,
            ComponentProviderInstance,
            Substitute.For<ILocalizationService>());
        // Create alive and destroyed mechs
        var aliveMech = mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        var destroyedMech = mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        destroyedMech.ApplyDamage([CreateHitDataForLocation(PartLocation.Head, 100)], HitDirection.Front);

        // Player 1: Ready, has alive unit
        var player1 = new Player(Guid.NewGuid(), "Player1", PlayerControlType.Human)
        {
            Status = PlayerStatus.Ready
        };
        player1.AddUnit(aliveMech);

        // Player 2: Ready, only destroyed unit
        var player2 = new Player(Guid.NewGuid(), "Player2", PlayerControlType.Human)
        {
            Status = PlayerStatus.Ready
        };
        player2.AddUnit(destroyedMech);


        // Use reflection to add players to the protected _players list
        var playersField = typeof(BaseGame).GetField("_players",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var playersList = (List<IPlayer>)playersField!.GetValue(this)!;
        playersList.Add(player1);
        playersList.Add(player2);

        AlivePlayers.ShouldBe([player1]);
    }

    [Fact]
    public void OnWeaponsAttack_ShouldProcessAttack_WhenSensorsAreIntact()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [MechFactoryTests.CreateDummyMechData()],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();
        var mech = player.Units.First() as Mech;
        mech!.AssignPilot(new MechWarrior("John", "Doe"));
        mech.Deploy(new HexPosition(new HexCoordinates(3, 3), HexDirection.BottomLeft));

        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitId = mech.Id,
            WeaponTargets = []
        };

        // Act & Assert - Should not throw or return early
        Should.NotThrow(() => OnWeaponsAttack(command));
        mech.HasDeclaredWeaponAttack.ShouldBeTrue();
    }

    [Fact]
    public void OnWeaponsAttack_ShouldNotProcessAttack_WhenSensorsAreDestroyed()
    {
        // Arrange
        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units =
            [
                MechFactoryTests.CreateDummyMechData()
            ],
            Tint = "#FF0000",
            PilotAssignments = []
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();
        var mech = player.Units.First() as Mech;
        mech?.Deploy(new HexPosition(new HexCoordinates(3, 3), HexDirection.BottomLeft));
        var sensors = mech!.GetAllComponents<Sensors>().First();
        sensors.Hit(); // First hit
        sensors.Hit(); // Second hit - destroys sensors

        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            UnitId = mech.Id,
            WeaponTargets = []
        };

        // Act
        OnWeaponsAttack(command);

        // Assert - Attack should not be processed
        mech.HasDeclaredWeaponAttack.ShouldBeFalse();
        mech.CanFireWeapons.ShouldBeFalse();
    }

    [Fact]
    public void OnPilotConsciousnessRoll_DoesNothing_WhenPilotNotFound()
    {
        // Arrange
        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Id,
            PilotId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [2, 3],
            IsSuccessful = false
        };

        // Act & Assert - Should not throw
        Should.NotThrow(() => OnPilotConsciousnessRoll(command));
    }

    [Fact]
    public void OnPilotConsciousnessRoll_KnocksPilotUnconscious_WhenConsciousnessCheckFails()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments =
            [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = true, Health = 6 }
                }
            ]
        };

        OnPlayerJoined(joinCommand);
        var mech = Players.SelectMany(p => p.Units).First() as Mech;
        var pilot = mech?.Pilot;
        pilot!.Id.ShouldBe(pilotId);

        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilotId,
            UnitId = unitId,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [1, 2],
            IsSuccessful = false // This should trigger KnockUnconscious
        };

        // Act
        OnPilotConsciousnessRoll(command);

        // Assert
        pilot.IsConscious.ShouldBeFalse();
    }

    [Fact]
    public void OnPilotConsciousnessRoll_DoesNotKnockPilotUnconscious_WhenConsciousnessCheckSucceeds()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments =
            [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = true, Health = 6 }
                }
            ]
        };

        OnPlayerJoined(joinCommand);
        var mech = Players.SelectMany(p => p.Units).First() as Mech;
        var pilot = mech?.Pilot;

        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilotId,
            UnitId = unitId,
            IsRecoveryAttempt = false,
            ConsciousnessNumber = 4,
            DiceResults = [2, 2],
            IsSuccessful = true // This should not trigger KnockUnconscious
        };

        // Act
        OnPilotConsciousnessRoll(command);

        // Assert
        pilot.ShouldNotBeNull();
        pilot.IsConscious.ShouldBeTrue();
    }

    [Fact]
    public void OnPilotConsciousnessRoll_RecoversPilot_WhenRecoveryAttemptSucceeds()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments =
            [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = false, Health = 6 }
                }
            ]
        };

        OnPlayerJoined(joinCommand);
        var mech = Players.SelectMany(p => p.Units).First() as Mech;
        var pilot = mech?.Pilot;

        pilot?.KnockUnconscious(1); // Make sure the pilot is unconscious
        pilot?.IsConscious.ShouldBeFalse();

        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilotId,
            UnitId = unitId,
            IsRecoveryAttempt = true,
            ConsciousnessNumber = 4,
            DiceResults = [2, 2],
            IsSuccessful = true // This should trigger recovery
        };

        // Act
        OnPilotConsciousnessRoll(command);

        // Assert - pilot should be conscious
        pilot.ShouldNotBeNull();
        pilot.IsConscious.ShouldBeTrue();
    }

    [Fact]
    public void OnPilotConsciousnessRoll_DoesNotRecoverPilot_WhenRecoveryAttemptFails()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        var pilotId = Guid.NewGuid();

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments =
            [
                new PilotAssignmentData
                {
                    UnitId = unitId,
                    PilotData = new PilotData { Id = pilotId, IsConscious = false, Health = 6 }
                }
            ]
        };

        OnPlayerJoined(joinCommand);
        var mech = Players.SelectMany(p => p.Units).First() as Mech;
        var pilot = mech?.Pilot;

        pilot?.KnockUnconscious(1); // Make sure the pilot is unconscious
        pilot?.IsConscious.ShouldBeFalse();

        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = pilotId,
            UnitId = unitId,
            IsRecoveryAttempt = true,
            ConsciousnessNumber = 4,
            DiceResults = [1, 2],
            IsSuccessful = false // This should not trigger recovery
        };

        // Act
        OnPilotConsciousnessRoll(command);

        // Assert - pilot should remain unconscious
        pilot.ShouldNotBeNull();
        pilot.IsConscious.ShouldBeFalse();
    }

    [Fact]
    public void ValidateCommand_ShouldAutoValidatePilotConsciousnessRollCommand()
    {
        // Arrange
        var command = new PilotConsciousnessRollCommand
        {
            GameOriginId = Guid.NewGuid(),
            PilotId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            IsRecoveryAttempt = true,
            ConsciousnessNumber = 4,
            DiceResults = [1, 2],
            IsSuccessful = false
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommand_ShouldAutoValidateUnitShutdownCommand()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommand_ShouldAutoValidateUnitStartupCommand()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void ValidateCommand_ShouldAutoValidateShutdownUnitCommand()
    {
        // Arrange
        var command = new ShutdownUnitCommand()
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void ValidateCommand_ShouldAutoValidateStartupUnitCommand()
    {
        // Arrange
        var command = new StartupUnitCommand()
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCommand_ShouldAutoValidateAmmoExplosionCommand()
    {
        // Arrange
        var command = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            CriticalHits = [],
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [2, 3],
                AvoidNumber = 6,
                IsSuccessful = false
            }
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void ValidateCommand_ShouldAutoValidateCriticalHitsResolutionCommand()
    {
        // Arrange
        var command = new CriticalHitsResolutionCommand
        {
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            TargetId = Guid.NewGuid(),
            CriticalHits = []
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void ValidateCommand_ShouldReturnTrue_WhenGameEndedCommand()
    {
        // Arrange
        var command = new GameEndedCommand
        {
            GameOriginId = Guid.NewGuid(),
            Reason = GameEndReason.PlayersLeft,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void ValidateCommand_ShouldReturnTrue_WhenPlayerLeftCommand()
    {
        // Arrange
        var command = new PlayerLeftCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = ValidateCommand(command);

        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void OnUnitShutdown_DoesNothing_WhenUnitNotFound()
    {
        // Arrange
        var command = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        Should.NotThrow(() => OnUnitShutdown(command));
    }

    [Fact]
    public void OnUnitShutdown_ShutsDownUnit_WhenAutomaticShutdown()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);
        unit.IsActive.ShouldBeTrue();

        var command = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            IsAutomaticShutdown = true,
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnUnitShutdown(command);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    [Fact]
    public void OnUnitShutdown_ShutsDownUnit_WhenAvoidShutdownRollFails()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);

        var command = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 20,
                DiceResults = [2, 3],
                AvoidNumber = 10,
                IsSuccessful = false
            },
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnUnitShutdown(command);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    [Fact]
    public void OnUnitShutdown_DoesNotShutDownUnit_WhenAvoidShutdownRollSucceeds()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);

        var command = new UnitShutdownCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            ShutdownData = new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 },
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 15,
                DiceResults = [5, 6],
                AvoidNumber = 8,
                IsSuccessful = true
            },
            IsAutomaticShutdown = false,
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnUnitShutdown(command);

        // Assert
        unit.IsActive.ShouldBeTrue();
        unit.IsShutdown.ShouldBeFalse();
    }

    [Fact]
    public void OnMechRestart_DoesNothing_WhenUnitNotFound()
    {
        // Arrange
        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = Guid.NewGuid(),
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert - Should not throw
        Should.NotThrow(() => OnMechRestart(command));
    }

    [Fact]
    public void OnMechRestart_StartsUpUnit_WhenRestartRollSucceeds()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);

        // First shut down the unit
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        unit.IsShutdown.ShouldBeTrue();

        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 10,
                DiceResults = [4, 5],
                AvoidNumber = 8,
                IsSuccessful = true
            },
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnMechRestart(command);

        // Assert
        unit.IsActive.ShouldBeTrue();
        unit.IsShutdown.ShouldBeFalse();
    }
    
    [Fact]
    public void OnMechRestart_StartsUpUnit_WhenAutomaticRestart()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);

        // First shut down the unit
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        unit.IsShutdown.ShouldBeTrue();

        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            IsAutomaticRestart = true,
            IsRestartPossible = true,
            AvoidShutdownRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnMechRestart(command);

        // Assert
        unit.IsActive.ShouldBeTrue();
        unit.IsShutdown.ShouldBeFalse();
    }

    [Fact]
    public void OnMechRestart_DoesNotStartUpUnit_WhenRestartIsImpossible()
    {
        // Arrange (same setup as above)
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;
        OnPlayerJoined(new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        });
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            IsAutomaticRestart = true,
            IsRestartPossible = false,
            AvoidShutdownRoll = null,
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnMechRestart(command);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    [Fact]
    public void OnMechRestart_DoesNotStartUpUnit_WhenRestartRollFails()
    {
        // Arrange
        var mechData = MechFactoryTests.CreateDummyMechData();
        var unitId = Guid.NewGuid();
        mechData.Id = unitId;

        var joinCommand = new JoinGameCommand
        {
            PlayerId = Guid.NewGuid(),
            PlayerName = "Player1",
            GameOriginId = Guid.NewGuid(),
            Units = [mechData],
            Tint = "#FF0000",
            PilotAssignments = []
        };

        OnPlayerJoined(joinCommand);
        var unit = Players.SelectMany(p => p.Units).First(u => u.Id == unitId);

        // First shut down the unit
        unit.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });
        unit.IsShutdown.ShouldBeTrue();

        var command = new UnitStartupCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = unitId,
            IsAutomaticRestart = false,
            IsRestartPossible = true,
            AvoidShutdownRoll = new AvoidShutdownRollData
            {
                HeatLevel = 15,
                DiceResults = [2, 3],
                AvoidNumber = 10,
                IsSuccessful = false
            },
            Timestamp = DateTime.UtcNow
        };

        // Act
        OnMechRestart(command);

        // Assert
        unit.IsActive.ShouldBeFalse();
        unit.IsShutdown.ShouldBeTrue();
    }

    public override void HandleCommand(IGameCommand command)
    {
        throw new NotImplementedException();
    }
}