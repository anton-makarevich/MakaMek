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
    [Fact]
    public void ProcessPotentialFall_ShouldTriggerPilotDamagePsrCalculation_WhenMechFallsDueToGyroHit()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit
        const int totalDamageDealt = 10; // Example damage amount

        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro"); 
        
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage"); 

        // First roll (6) is for GyroHit PSR (6 < 7 fails).
        // Second roll (5) is for PilotDamageFromFall PSR.
        SetupDiceRolls(6, 5); 

        // Setup falling damage calculator (necessary for the FallProcessor to complete the fall logic).
        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(
                _testMech, 
                0,
                false)
            .Returns(fallingDamageData);

        // Act
        _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId);

        // Assert
        // Verify that GetPsrBreakdown was called for PilotDamageFromFall with the correct 'totalDamageDealt'
        // from the initial damage event, as this can influence pilot damage PSR modifiers.
        _mockPilotingSkillCalculator.Received().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types =>
                types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            Arg.Any<BattleMap>());
    }
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
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulPsr_WhenGyroHitPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit
        const int totalDamageDealt = 5; // Not enough for heavy damage fall

        // Setup GyroHit PSR to succeed.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
        
        // Dice roll: 8 for GyroHit PSR (8 >= 7 succeeds).
        SetupDiceRolls(8); 

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)!.Value;

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBeNull(); // No fall, so no damage data
        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.GyroHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeTrue();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(8);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7); // 4 (base) + 3 (gyro hit mod)
        result.IsPilotingSkillRollRequired.ShouldBe(true);
        result.IsPilotTakingDamage.ShouldBe(false); // No fall, so no pilot damage PSR
        result.PilotDamagePilotingSkillRoll.ShouldBeNull();

        // Verify GetPsrBreakdown was called for GyroHit
        _mockPilotingSkillCalculator.Received().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types =>
                types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>());

        // Verify no FallingDamage calculation occurred
        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(
            Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }
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
    //
    [Fact]
    public void ProcessPotentialFall_ShouldReturnNull_WhenGyroHitReportedForMechWithoutGyro()
    {
        // Arrange
        // Ensure the testMech has no Gyro for this test
        var gyroComponent = _testMech.GetAllComponents<Gyro>().FirstOrDefault();
        if (gyroComponent != null)
        {
            // Find the part containing the gyro and remove it.
            // This assumes Gyro is in CenterTorso for standard mechs, but iterates to be safe.
            var partContainingGyro = _testMech.Parts.FirstOrDefault(p => p.GetComponents<Gyro>().Any());
            partContainingGyro?.RemoveComponent(gyroComponent);
        }
        _testMech.GetAllComponents<Gyro>().ShouldBeEmpty("Test 'Mech should not have a Gyro for this scenario.");

        var gyroComponentHit = new ComponentHitData { Type = MakaMekComponent.Gyro, Slot = 1 }; // Report a gyro hit
        var componentHits = new List<ComponentHitData> { gyroComponentHit };
        const int totalDamageDealt = 5; // Damage below heavy damage threshold

        // Ensure heavy damage PSR is not triggered
        // (RulesProvider is ClassicBattletechRulesProvider, GetHeavyDamageThreshold defaults to 20)

        // Act
        var resultCommand = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId);

        // Assert
        resultCommand.ShouldBeNull();

        // Ensure no PSR was attempted for a GyroHit because the mech has no gyro
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech, 
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => 
                types.Contains(PilotingSkillRollType.GyroHit)), 
            Arg.Any<BattleMap>(), 
            Arg.Any<int>());
        
        // Also ensure no pilot damage PSR was attempted as no fall should be processed
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech, 
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)), 
            Arg.Any<BattleMap>(), 
            Arg.Any<int>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedFallAndPilotDamagePsrs_WhenBothPsrsFail()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit
        const int totalDamageDealt = 5; // Not enough for heavy damage fall

        // Setup GyroHit PSR to fail.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
        
        // Setup PilotDamageFromFall PSR to fail.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage from fall"); 

        // Dice rolls:
        // First roll (6) for GyroHit PSR (6 < 7 fails).
        // Second roll (6) for PilotDamageFromFall PSR (6 < 7 fails).
        SetupDiceRolls(6, 6); 

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)!.Value;

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData); // Fall occurred, damage applied

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.GyroHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(6);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7);

        result.IsPilotingSkillRollRequired.ShouldBe(true);
        result.IsPilotTakingDamage.ShouldBe(true); 
        
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.PilotDamagePilotingSkillRoll?.DiceResults.Sum().ShouldBe(6);
        result.PilotDamagePilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7);

        // Verify GetPsrBreakdown was called for GyroHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.GyroHit)),
            _map,
            totalDamageDealt);

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            totalDamageDealt);
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedLlaPsrAndPilotDamagePsr_WhenLlaPsrFails()
    {
        // Arrange
        // For LLA, the specific component (e.g., LeftLowerLegActuator) isn't as crucial as the fact it IS an LLA.
        // The FallProcessor uses FallInducingCriticalsMap which maps MakaMekComponent.LowerLegActuator.
        var componentHits = SetupCriticalHits(MakaMekComponent.LowerLegActuator, 1); 
        const int totalDamageDealt = 5; // Not enough for heavy damage fall

        // Setup LLA Hit PSR to fail.
        // BasePilotingSkill = 4. LLA Hit Mod +1 (from memory/typical rules). TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.LowerLegActuatorHit, 1, "Lower Leg Actuator Hit");
        
        // Setup PilotDamageFromFall PSR.
        // BasePilotingSkill = 4. No specific modifier for this example. TargetNumber = 4.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage from fall"); 

        // Dice rolls:
        // First roll (4) for LLA Hit PSR (4 < 5 fails).
        // Second roll (5) for PilotDamageFromFall PSR (5 >= 4 succeeds).
        SetupDiceRolls(4, 5); 

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)!.Value;

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData);

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.LowerLegActuatorHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(4);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (LLA mod)

        result.IsPilotingSkillRollRequired.ShouldBe(true);
        result.IsPilotTakingDamage.ShouldBe(true); 
        
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue(); // Based on dice roll 5 vs target 4
        result.PilotDamagePilotingSkillRoll?.DiceResults.Sum().ShouldBe(5);
        result.PilotDamagePilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(4);

        // Verify GetPsrBreakdown was called for LowerLegActuatorHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.LowerLegActuatorHit)),
            _map,
            totalDamageDealt);

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            totalDamageDealt);
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
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
