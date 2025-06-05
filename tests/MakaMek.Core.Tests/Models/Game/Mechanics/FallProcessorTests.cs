using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class FallProcessorTests
{
    private readonly FallProcessor _sut;
    private readonly IRulesProvider _mockRulesProvider;
    private readonly IPilotingSkillCalculator _mockPilotingSkillCalculator;
    private readonly IDiceRoller _mockDiceRoller;
    private readonly IFallingDamageCalculator _mockFallingDamageCalculator;

    private Mech _testMech;
    private BattleMap _map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10,
        new SingleTerrainGenerator(10,10, new ClearTerrain()));
    private Guid _gameId = Guid.NewGuid();

    public FallProcessorTests()
    {
        _mockRulesProvider = new ClassicBattletechRulesProvider();
        _mockPilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
        _mockDiceRoller = Substitute.For<IDiceRoller>();
        _mockFallingDamageCalculator = Substitute.For<IFallingDamageCalculator>();

        _sut = new FallProcessor(
            _mockRulesProvider,
            _mockPilotingSkillCalculator,
            _mockDiceRoller,
            _mockFallingDamageCalculator);

        _testMech = new MechFactory(
            _mockRulesProvider,
            Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData());
    }


    [Fact]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenGyroHitPsrFails()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3);

        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");

        // Setup piloting skill calculator for pilot damage
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
        
        // roll for PSR (failed roll - 6 is less than 7 needed)
        SetupDiceRolls(6);

        // Setup falling damage calculator

        var fallingDamageData = GetFallingDamageData();

        _mockFallingDamageCalculator.CalculateFallingDamage(
                Arg.Any<Unit>(),
                Arg.Is<int>(i => i == 0),
                Arg.Is<bool>(b => b == false))
            .Returns(fallingDamageData);

        // Act
        var result =
            _sut.ProcessPotentialFall(_testMech, _map, componentHits, 10, _gameId)!.Value;

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.LevelsFallen.ShouldBe(0);
        result.WasJumping.ShouldBe(false);
        result.DamageData.ShouldBe(fallingDamageData);
        result.IsPilotTakingDamage.ShouldBe(true);
        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.IsPilotingSkillRollRequired.ShouldBe(true);

    }
    //
    // [Fact]
    // public void Enter_ShouldGetPsrBreakdownForWarriorDamage_WhenMechFalls()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //
    //     // Setup ToHitCalculator to return a value
    //     SetupToHitNumber(7);
    //
    //     // Configure dice rolls to ensure hit on center torso
    //     // First roll (8) is for attack (hit)
    //     // Second roll (7) is for hit location (center torso)
    //     // Third roll for PSR (failed roll - 6 is less than 7 needed)
    //     SetupDiceRolls(8, 7, 6);
    //
    //     // Setup critical hit calculator to return a gyro hit
    //     SetupCriticalHitsFor(MakaMekComponent.Gyro,3, PartLocation.CenterTorso);
    //
    //     // Setup piloting skill calculator for gyro hit
    //     SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
    //
    //     // Setup piloting skill calculator for pilot damage
    //     SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
    //
    //     // Setup falling damage calculator
    //     var facingRoll = new DiceResult(3);
    //     var hitLocationRolls = new List<DiceResult> { new(3), new(3) };
    //     var hitLocationData = new HitLocationData(
    //         PartLocation.CenterTorso,
    //         5,
    //         hitLocationRolls
    //     );
    //     var hitLocationsData = new HitLocationsData(
    //         [hitLocationData],
    //         5
    //     );
    //
    //     var fallingDamageData = new FallingDamageData(
    //         HexDirection.TopRight,
    //         hitLocationsData,
    //         facingRoll
    //     );
    //
    //     Game.FallingDamageCalculator.CalculateFallingDamage(
    //             Arg.Any<Unit>(),
    //             Arg.Is<int>(i => i == 0),
    //             Arg.Is<bool>(b => b == false))
    //         .Returns(fallingDamageData);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     // Verify that GetPsrBreakdown was called with WarriorDamageFromFall roll type
    //     Game.PilotingSkillCalculator.Received().GetPsrBreakdown(
    //         Arg.Any<Unit>(),
    //         Arg.Is<IEnumerable<PilotingSkillRollType>>(types =>
    //             types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
    //         Arg.Any<BattleMap>());
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommand_WhenGyroIsDestroyed()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //
    //     // Setup ToHitCalculator to return a value
    //     SetupToHitNumber(7);
    //
    //     // Configure dice rolls to ensure hit on center torso
    //     // First roll (8) is for attack (hit)
    //     // Second roll (7) is for hit location (center torso)
    //     SetupDiceRolls(8, 7);
    //
    //     // Setup critical hit calculator to return a gyro hit
    //     SetupCriticalHitsFor(MakaMekComponent.Gyro,3, PartLocation.CenterTorso);
    //
    //     // Setup piloting skill calculator for pilot damage
    //     SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
    //
    //     // Destroy the gyro with 2 hits
    //     var gyro = _player1Unit1.GetAllComponents<Gyro>().First();
    //     gyro.Hit();
    //     gyro.Hit();
    //
    //     // Setup falling damage calculator
    //     var fallingDamageData = GetFallingDamageData();
    //
    //     Game.FallingDamageCalculator.CalculateFallingDamage(
    //             Arg.Any<Unit>(),
    //             Arg.Is<int>(i => i == 0),
    //             Arg.Is<bool>(b => b == false))
    //         .Returns(fallingDamageData);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player1Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
    //
    //     // Verify that a MechFallingCommand was published with the correct data
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.GameOriginId == Game.Id &&
    //             cmd.UnitId == _player1Unit1.Id &&
    //             cmd.LevelsFallen == 0 &&
    //             cmd.WasJumping == false &&
    //             cmd.DamageData == fallingDamageData &&
    //             cmd.IsPilotingSkillRollRequired == false));
    // }
    //
    // [Fact]
    // public void Enter_ShouldNotPublishMechFallingCommand_WhenNoGyroFound()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //
    //     // Setup ToHitCalculator to return a value
    //     SetupToHitNumber(7);
    //
    //     // Configure dice rolls to ensure hit on center torso
    //     // First roll (8) is for attack (hit)
    //     // Second roll (7) is for hit location (center torso)
    //     SetupDiceRolls(8, 7);
    //
    //     // Setup critical hit calculator to return a gyro hit
    //     SetupCriticalHitsFor(MakaMekComponent.Gyro,3, PartLocation.CenterTorso,_player1Unit1);
    //
    //     // Remove gyro to simulate no gyro mech (not possible, so maybe we should throw)
    //     var ct = _player1Unit1.Parts.First(p => p.Location == PartLocation.CenterTorso);
    //     var gyro = ct.GetComponents<Gyro>().First();
    //     ct.RemoveComponent(gyro);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     // Verify that a MechFallingCommand was published with the correct data
    //     CommandPublisher.DidNotReceive().PublishCommand(
    //         Arg.Any<MechFallingCommand>());
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommand_WhenGyroHitPsrSucceeds()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //
    //     // Setup ToHitCalculator to return a value
    //     Game.ToHitCalculator.GetToHitNumber(
    //             Arg.Any<Unit>(),
    //             Arg.Any<Unit>(),
    //             Arg.Any<Weapon>(),
    //             Arg.Any<BattleMap>())
    //         .Returns(7); // Return a to-hit number of 7
    //
    //     // Configure dice rolls to ensure hit on center torso
    //     // First roll (8) is for attack (hit)
    //     // Second roll (7) is for hit location (center torso)
    //     // Third roll for PSR (successful roll - 8 is greater than 7 needed)
    //     SetupDiceRolls(8, 7, 8);
    //
    //     // Setup critical hit calculator to return a gyro hit
    //     SetupCriticalHitsFor(MakaMekComponent.Gyro,3, PartLocation.CenterTorso);
    //
    //     // Setup piloting skill calculator to return a PSR breakdown with modifiers
    //     SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     // Verify that a MechFallingCommand was published
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.GameOriginId == Game.Id &&
    //             cmd.UnitId == _player1Unit1.Id &&
    //             cmd.LevelsFallen == 0 &&
    //             cmd.WasJumping == false &&
    //             cmd.DamageData == null &&
    //             cmd.FallPilotingSkillRoll!.DiceResults.Sum() == 8 &&
    //             cmd.FallPilotingSkillRoll!.RollType == PilotingSkillRollType.GyroHit &&
    //             cmd.IsPilotingSkillRollRequired == true));
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommandWithPilotDamagePsr_WhenPilotDamageCheckFails()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //
    //     // Setup ToHitCalculator to return a value
    //     SetupToHitNumber(7);
    //
    //     // Setup critical hit calculator to return a gyro hit
    //     SetupCriticalHitsFor(MakaMekComponent.Gyro,3, PartLocation.CenterTorso);
    //
    //     // Setup piloting skill calculator for gyro hit
    //     SetupPsrFor(PilotingSkillRollType.GyroHit,3,"Damaged Gyro");
    //
    //     // Setup piloting skill calculator for pilot damage
    //     SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
    //
    //     // Setup dice roller to return a failed PSR roll for both checks
    //     SetupDiceRolls(8, 7, 6, 3);
    //
    //     // Setup falling damage calculator
    //     var facingRoll = new DiceResult(3);
    //     var hitLocationRolls = new List<DiceResult> { new(3), new(3) };
    //     var hitLocationData = new HitLocationData(
    //         PartLocation.CenterTorso,
    //         5,
    //         hitLocationRolls
    //     );
    //     var hitLocationsData = new HitLocationsData(
    //         [hitLocationData],
    //         5
    //     );
    //
    //     var fallingDamageData = new FallingDamageData(
    //         HexDirection.TopRight,
    //         hitLocationsData,
    //         facingRoll
    //     );
    //
    //     Game.FallingDamageCalculator.CalculateFallingDamage(
    //             Arg.Any<Unit>(),
    //             Arg.Is<int>(i => i == 0),
    //             Arg.Is<bool>(b => b == false))
    //         .Returns(fallingDamageData);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player1Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
    //
    //     // Verify that a MechFallingCommand was published with the correct data
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.GameOriginId == Game.Id &&
    //             cmd.UnitId == _player1Unit1.Id &&
    //             cmd.LevelsFallen == 0 &&
    //             cmd.WasJumping == false &&
    //             cmd.DamageData == fallingDamageData &&
    //             cmd.IsPilotTakingDamage == true &&
    //             cmd.PilotDamagePilotingSkillRoll != null &&
    //             !cmd.PilotDamagePilotingSkillRoll.IsSuccessful));
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommand_WhenLowerLegActuatorHitPsrFails()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //     SetupToHitNumber(7);
    //
    //     // Dice: 1. Attack roll (hits), 2. Hit Location roll (LeftLeg - 9 for Front/Rear), 3. PSR roll (fails - 4 vs target 5)
    //     SetupDiceRolls(8, 9, 4);
    //
    //     SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
    //
    //     // PSR for LLA Hit: Base Skill 4, LLA Mod +1 => Target 5.
    //     SetupPsrFor(PilotingSkillRollType.LowerLegActuatorHit, 1, "Lower Leg Actuator Hit");
    //
    //     // PSR for Pilot Damage from Fall
    //     SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 2, "Pilot Damage from Fall");
    //
    //     var fallingDamageData = GetFallingDamageData();
    //     Game.FallingDamageCalculator.CalculateFallingDamage(Arg.Any<Unit>(), 0, false)
    //         .Returns(fallingDamageData);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player1Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.UnitId == _player1Unit1.Id &&
    //             cmd.IsPilotingSkillRollRequired == true &&
    //             cmd.FallPilotingSkillRoll != null &&
    //             cmd.FallPilotingSkillRoll.RollType == PilotingSkillRollType.LowerLegActuatorHit &&
    //             cmd.PilotDamagePilotingSkillRoll != null &&
    //             cmd.DamageData == fallingDamageData));
    // }
    //
    // [Fact]
    // public void Enter_ShouldNotPublishMechFallingCommand_WhenLowerLegActuatorHitPsrSucceeds()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //     SetupToHitNumber(7);
    //
    //     // Dice: 1. Attack (hits), 2. Hit Location (LeftLeg - 9), 3. PSR (succeeds - 8 vs target 5)
    //     SetupDiceRolls(8, 9, 8);
    //
    //     SetupCriticalHitsFor(MakaMekComponent.LowerLegActuator,2, PartLocation.LeftLeg, _player1Unit1);
    //
    //     // PSR for LLA Hit: Base Skill 4, LLA Mod +1 => Target 5.
    //     SetupPsrFor(PilotingSkillRollType.LowerLegActuatorHit, 1, "Lower Leg Actuator Hit");
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player1Unit1.Status.ShouldNotHaveFlag(UnitStatus.Prone);
    //     // Verify that a MechFallingCommand was published
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.GameOriginId == Game.Id &&
    //             cmd.UnitId == _player1Unit1.Id &&
    //             cmd.LevelsFallen == 0 &&
    //             cmd.WasJumping == false &&
    //             cmd.DamageData == null &&
    //             cmd.FallPilotingSkillRoll!.DiceResults.Sum() == 8 &&
    //             cmd.FallPilotingSkillRoll!.RollType == PilotingSkillRollType.LowerLegActuatorHit &&
    //             cmd.IsPilotingSkillRollRequired == true));
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommand_WhenHeavyDamagePsrFails()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //     SetupToHitNumber(7);
    //
    //     // Configure a high-damage weapon
    //     var highDamageWeapon = new TestWeapon(WeaponType.Energy, null, 20);
    //     var part = _player1Unit1.Parts[0];
    //     part.TryAddComponent(highDamageWeapon, [1]);
    //     highDamageWeapon.Target = _player2Unit1;
    //
    //     // Dice: 1. Attack roll (hits), 2. Hit Location roll, 3. PSR roll (fails - 4 vs target 7)
    //     SetupDiceRolls(8, 7, 4);
    //
    //     // Setup piloting skill calculator for heavy damage
    //     SetupPsrFor(PilotingSkillRollType.HeavyDamage, 3, "Heavy Damage");
    //
    //     // Setup piloting skill calculator for pilot damage
    //     SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 2, "Pilot Damage from Fall");
    //
    //     var fallingDamageData = GetFallingDamageData();
    //     Game.FallingDamageCalculator.CalculateFallingDamage(Arg.Any<Unit>(), 0, false)
    //         .Returns(fallingDamageData);
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player2Unit1.Status.ShouldHaveFlag(UnitStatus.Prone);
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.UnitId == _player2Unit1.Id &&
    //             cmd.IsPilotingSkillRollRequired == true &&
    //             cmd.FallPilotingSkillRoll != null &&
    //             cmd.FallPilotingSkillRoll.RollType == PilotingSkillRollType.HeavyDamage &&
    //             cmd.PilotDamagePilotingSkillRoll != null &&
    //             cmd.DamageData == fallingDamageData));
    // }
    //
    // [Fact]
    // public void Enter_ShouldPublishMechFallingCommand_WhenHeavyDamagePsrSucceeds()
    // {
    //     // Arrange
    //     SetMap();
    //     SetupPlayer1WeaponTargets();
    //     SetupToHitNumber(7);
    //
    //     // Configure a high-damage weapon
    //     var highDamageWeapon = new TestWeapon(WeaponType.Energy, null, 20);
    //     var part = _player1Unit1.Parts[0];
    //     part.TryAddComponent(highDamageWeapon, [1]);
    //     highDamageWeapon.Target = _player2Unit1;
    //
    //     // Dice: 1. Attack roll (hits), 2. Hit Location roll, 3. PSR roll (succeeds - 8 vs target 7)
    //     SetupDiceRolls(8, 7, 8);
    //
    //     // Setup piloting skill calculator for heavy damage
    //     SetupPsrFor(PilotingSkillRollType.HeavyDamage, 3, "Heavy Damage (20+ points)");
    //
    //     // Act
    //     _sut.Enter();
    //
    //     // Assert
    //     _player2Unit1.Status.ShouldNotHaveFlag(UnitStatus.Prone);
    //     CommandPublisher.Received().PublishCommand(
    //         Arg.Is<MechFallingCommand>(cmd =>
    //             cmd.GameOriginId == Game.Id &&
    //             cmd.UnitId == _player2Unit1.Id &&
    //             cmd.LevelsFallen == 0 &&
    //             cmd.WasJumping == false &&
    //             cmd.DamageData == null &&
    //             cmd.FallPilotingSkillRoll!.DiceResults.Sum() == 8 &&
    //             cmd.FallPilotingSkillRoll!.RollType == PilotingSkillRollType.HeavyDamage &&
    //             cmd.IsPilotingSkillRollRequired == true));
    // }
    private void SetupPsrFor(PilotingSkillRollType psrType, int modifierValue, string modifierName)
    {
        _mockPilotingSkillCalculator.GetPsrBreakdown(
                Arg.Any<Unit>(),
                Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(psrType)),
                Arg.Any<BattleMap>(),
                Arg.Any<int>())
            .Returns(new PsrBreakdown
            {
                BasePilotingSkill = 4,
                Modifiers = [new TestModifier { Value = modifierValue, Name = modifierName }]
            });
    }

    private List<ComponentHitData> SetupCriticalHits(
        MakaMekComponent component,
        int slot)
    {
        return
        [
            new ComponentHitData
            {
                Type = component,
                Slot = slot
            }
        ];
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
        _mockDiceRoller.Roll2D6().Returns(_ =>
        {
            var result = diceResults[callCount % diceResults.Count];
            callCount++;
            return result;
        });
    }

    private FallingDamageData GetFallingDamageData()
    {
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

        return new FallingDamageData(
            HexDirection.TopRight,
            hitLocationsData,
            facingRoll
        );
    }
    
    public record TestModifier : Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.RollModifier
    {
        public required string Name { get; init; }

        public override string Render(Sanet.MakaMek.Core.Services.Localization.ILocalizationService localizationService)
        {
            return Name;
        }
    }
}
