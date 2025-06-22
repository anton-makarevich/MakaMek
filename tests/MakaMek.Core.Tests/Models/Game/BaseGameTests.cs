using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Tests.Models.Game;

public class BaseGameTests : BaseGame
{
    public BaseGameTests() : base(
        new ClassicBattletechRulesProvider(),
        new MechFactory(new ClassicBattletechRulesProvider(), Substitute.For<ILocalizationService>()),
        Substitute.For<ICommandPublisher>(),
        Substitute.For<IToHitCalculator>(),
        Substitute.For<IPilotingSkillCalculator>())
    {
        base.SetBattleMap(BattleMapTests.BattleMapFactory.GenerateMap(5, 5, new SingleTerrainGenerator(5,5, new ClearTerrain())));
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
            Units = [],
            Tint = "#FF0000"
        };

        // Act
        OnPlayerJoined(joinCommand);

        // Assert
        Players.Count.ShouldBe(1);
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
            Tint = "#FF0000"
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
            Tint = "#FF0000"
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
            Tint = "#FF0000"
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
            AttackerId = Guid.NewGuid(),
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
            Tint = "#FF0000"
        };
        OnPlayerJoined(joinCommand);
        var player = Players.First();
        
        var command = new WeaponAttackDeclarationCommand
        {
            GameOriginId = Id,
            PlayerId = player.Id,
            AttackerId = Guid.NewGuid(),
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
            Tint = "#FF0000"
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units.First() as Mech;
        attackerMech!.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
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
            Tint = "#00FF00"
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
            AttackerId = attackerMech.Id,
            WeaponTargets = [
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = weapon.Name,
                        Location = PartLocation.RightArm,
                        Slots = weapon.MountedAtSlots
                    },
                    TargetId = targetMech.Id,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        attackerMech.HasDeclaredWeaponAttack.ShouldBeTrue();
        weapon.Target.ShouldNotBeNull();
        weapon.Target.ShouldBe(targetMech);
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
            Tint = "#FF0000"
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units.First() as Mech;
        attackerMech!.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
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
            Units = [targetUnitData1,targetUnitData2],
            Tint = "#00FF00"
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
            AttackerId = attackerMech.Id,
            WeaponTargets = [
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = rightArmWeapon.Name,
                        Location = PartLocation.RightArm,
                        Slots = rightArmWeapon.MountedAtSlots
                    },
                    TargetId = targetMech1.Id,
                    IsPrimaryTarget = true
                },
                new WeaponTargetData
                {
                    Weapon = new WeaponData
                    {
                        Name = leftArmWeapon.Name,
                        Location = PartLocation.LeftArm,
                        Slots = leftArmWeapon.MountedAtSlots
                    },
                    TargetId = targetMech2.Id,
                    IsPrimaryTarget = true
                }
            ]
        };

        // Act
        OnWeaponsAttack(command);

        // Assert
        attackerMech.HasDeclaredWeaponAttack.ShouldBeTrue();
        
        rightArmWeapon.Target.ShouldNotBeNull();
        rightArmWeapon.Target.ShouldBe(targetMech1);
        
        leftArmWeapon.Target.ShouldNotBeNull();
        leftArmWeapon.Target.ShouldBe(targetMech2);
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
            WeaponData = new WeaponData
            {
                Name = "Test Weapon",
                Location = PartLocation.RightArm,
                Slots = [0, 1]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                null,
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
            Tint = "#00FF00"
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        // Create hit locations data
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, []),
            new(PartLocation.LeftArm, 3, [])
        };
        
        // Create the attack resolution command with IsHit = false
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = Guid.NewGuid(),
            AttackerId = Guid.NewGuid(),
            TargetId = targetMech.Id,
            WeaponData = new WeaponData
            {
                Name = "Test Weapon",
                Location = PartLocation.RightArm,
                Slots = [0, 1]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                false, // Attack missed
                null,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Get initial armor values for verification
        var centerTorsoPart = targetMech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var leftArmPart = targetMech.Parts.First(p => p.Location == PartLocation.LeftArm);
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
            Tint = "#00FF00"
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
            WeaponData = new WeaponData
            {
                Name = "Test Weapon",
                Location = PartLocation.RightArm,
                Slots = [0, 1]
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                null,
                new AttackHitLocationsData([], 0, [], 0))
        };

        // Act & Assert
        // No exception should be thrown
        var action = () => OnWeaponsAttackResolution(command);
        action.ShouldNotThrow();
    }
    
    [Fact]
    public void OnWeaponsAttackResolution_ShouldFireWeaponAndApplyDamage()
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
            Tint = "#FF0000"
        };
        OnPlayerJoined(attackerJoinCommand);
        var attackerPlayer = Players.First(p => p.Id == attackerPlayerId);
        var attackerMech = attackerPlayer.Units.First() as Mech;
        attackerMech!.Deploy(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        
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
            Tint = "#00FF00"
        };
        OnPlayerJoined(targetJoinCommand);
        var targetPlayer = Players.First(p => p.Id == targetPlayerId);
        var targetMech = targetPlayer.Units.First() as Mech;
        targetMech!.Deploy(new HexPosition(new HexCoordinates(1, 2), HexDirection.Top));
        
        // Get a weapon from the attacker mech
        var weapon = attackerMech.GetComponentsAtLocation<Weapon>(PartLocation.RightArm).FirstOrDefault();
        weapon.ShouldNotBeNull();
        
        // Create hit locations data
        var hitLocations = new List<HitLocationData>
        {
            new(PartLocation.CenterTorso, 5, []),
            new(PartLocation.LeftArm, 3, [])
        };
        
        // Get initial values for verification
        var centerTorsoPart = targetMech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var leftArmPart = targetMech.Parts.First(p => p.Location == PartLocation.LeftArm);
        var initialCenterTorsoArmor = centerTorsoPart.CurrentArmor;
        var initialLeftArmArmor = leftArmPart.CurrentArmor;
        
        // Create the attack resolution command
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Id,
            PlayerId = attackerPlayerId,
            AttackerId = attackerMech.Id,
            TargetId = targetMech.Id,
            WeaponData = new WeaponData
            {
                Name = weapon.Name,
                Location = PartLocation.RightArm,
                Slots = weapon.MountedAtSlots
            },
            ResolutionData = new AttackResolutionData(
                10,
                [],
                true,
                null,
                new AttackHitLocationsData(hitLocations, 8, [], 0))
        };

        // Act
        OnWeaponsAttackResolution(command);

        // Assert
        // Verify that damage was applied to the target
        centerTorsoPart.CurrentArmor.ShouldBe(initialCenterTorsoArmor - 5);
        leftArmPart.CurrentArmor.ShouldBe(initialLeftArmArmor - 3);
    }
    
    [Fact]
    public void AlivePlayers_ShouldReturnOnlyReadyPlayersWithAliveUnits()
    {
        var mechFactory = new MechFactory(new ClassicBattletechRulesProvider(),Substitute.For<ILocalizationService>());
        // Create alive and destroyed mechs
        var aliveMech = mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        var destroyedMech = mechFactory.Create(MechFactoryTests.CreateDummyMechData());
        var headPart = destroyedMech.Parts.FirstOrDefault(p => p.Location == PartLocation.Head);
        destroyedMech.ApplyArmorAndStructureDamage(100, headPart!);

        // Player 1: Ready, has alive unit
        var player1 = new Player(Guid.NewGuid(), "Player1")
        {
            Status = PlayerStatus.Ready
        };
        player1.AddUnit(aliveMech);

        // Player 2: Ready, only destroyed unit
        var player2 = new Player(Guid.NewGuid(), "Player2")
        {
            Status = PlayerStatus.Ready
        };
        player2.AddUnit(destroyedMech);
        

        // Use reflection to add players to the protected _players list
        var playersField = typeof(BaseGame).GetField("_players", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var playersList = (List<IPlayer>)playersField!.GetValue(this)!;
        playersList.Add(player1);
        playersList.Add(player2);

        AlivePlayers.ShouldBe([player1]);
    }

    public override void HandleCommand(IGameCommand command)
    {
        throw new NotImplementedException();
    }
}