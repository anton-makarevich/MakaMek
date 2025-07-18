using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Tests.Models.Map;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Mechs.Falling;

public class FallProcessorTests
{
    private readonly FallProcessor _sut;
    private readonly IPilotingSkillCalculator _mockPilotingSkillCalculator;
    private readonly IDiceRoller _mockDiceRoller;
    private readonly IFallingDamageCalculator _mockFallingDamageCalculator;

    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Mech _testMech;
    private readonly BattleMap _map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10,
        new SingleTerrainGenerator(10,10, new ClearTerrain()));
    private readonly Guid _gameId = Guid.NewGuid();

    public FallProcessorTests()
    {
        IRulesProvider rulesProvider = new ClassicBattletechRulesProvider();
        _mockPilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
        _mockDiceRoller = Substitute.For<IDiceRoller>();
        _mockFallingDamageCalculator = Substitute.For<IFallingDamageCalculator>();

        _game.BattleMap.Returns(_map);
        _game.Id.Returns(_gameId);

        _sut = new FallProcessor(
            rulesProvider,
            _mockPilotingSkillCalculator,
            _mockDiceRoller,
            _mockFallingDamageCalculator);

        _testMech = new MechFactory(
            rulesProvider,
            Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData());
    }


    [Fact]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenGyroHitPsrFails()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3);

        // Setup piloting skill calculator to return a PSR breakdown with modifiers
        // Assuming base piloting skill 4, +3 for Gyro Hit = Target Number 7
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");

        // Setup piloting skill calculator for pilot damage
        // Assuming base piloting skill 4, +3 for Pilot Damage = Target Number 7
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
        var results =
            _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.ShouldHaveSingleItem("A single command should be returned for a gyro hit PSR fail.");
        var command = results.Single();

        command.GameOriginId.ShouldBe(_gameId);
        command.UnitId.ShouldBe(_testMech.Id);
        command.LevelsFallen.ShouldBe(0);
        command.WasJumping.ShouldBe(false);
        command.DamageData.ShouldBe(fallingDamageData);
        command.IsPilotTakingDamage.ShouldBe(true);
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeFalse();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.GyroHit);
        command.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        command.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeFalse();
        command.PilotDamagePilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        command.IsPilotingSkillRollRequired.ShouldBe(true);
    }
    [Fact]
    public void ProcessPotentialFall_ShouldTriggerPilotDamagePsrCalculation_WhenMechFallsDueToGyroHit()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit
        
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
        _sut.ProcessPotentialFall(_testMech, _game, componentHits);

        // Assert
        // Verify that GetPsrBreakdown was called 
        _mockPilotingSkillCalculator.Received().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Any<PilotingSkillRollType>(),
            Arg.Any<IGame>());
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenGyroIsDestroyed()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro,3);
    
        // Setup piloting skill calculator for pilot damage
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
    
        SetupDiceRolls(5);
        // Destroy the gyro with 2 hits
        var gyro = _testMech.GetAllComponents<Gyro>().First();
        gyro.Hit();
        gyro.Hit();
    
        // Setup falling damage calculator
        var fallingDamageData = GetFallingDamageData();
    
        _mockFallingDamageCalculator.CalculateFallingDamage(
                Arg.Any<Unit>(),
                Arg.Is<int>(i => i == 0),
                Arg.Is<bool>(b => b == false))
            .Returns(fallingDamageData);
    
        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .First();
    
        // Assert
        // Verify that a MechFallingCommand was published with the correct data
        result.GameOriginId.ShouldBe(_gameId);
            result.UnitId.ShouldBe(_testMech.Id); 
            result.LevelsFallen.ShouldBe(0);
            result.WasJumping.ShouldBe(false);
            result.DamageData.ShouldBe(fallingDamageData);
            result.IsPilotTakingDamage.ShouldBe(true);
            result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
            result.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeFalse();
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulPsr_WhenGyroHitPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit

        // Setup GyroHit PSR to succeed.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
        
        // Dice roll: 8 for GyroHit PSR (8 >= 7 succeeds).
        SetupDiceRolls(8); 

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .First();

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
            Arg.Any<PilotingSkillRollType>(),
            Arg.Any<IGame>());

        // Verify no FallingDamage calculation occurred
        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(
            Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnOneCommand_WhenGyroHitAndHeavyDamagePsrsFail()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 1); // Gyro hit
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        _testMech.ApplyDamage([new HitLocationData(PartLocation.CenterTorso, totalDamageDealt, []),]);
        
        // Gyro Hit PSR: Base 4 + Mod 3 (Gyro Hit) = TN 7. Roll 6 -> Fails.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Gyro Hit");
        // Heavy Damage PSR: Base 4 + Mod 2 (20 damage) = TN 6. Roll 5 -> Fails.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");
        // PilotDamageFromFall PSR: Base 4 + Mod 0 = TN 4. Roll 7 -> Succeeds (pilot avoids damage).
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot Damage PSR");

        // Dice rolls: Gyro (fails), HeavyDamage (fails), PilotDamage (succeeds)
        SetupDiceRolls(6,7,5,7);

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.Count.ShouldBe(1, "Mech falls on first command.");

        // First command (expected Gyro Hit)
        var command = results.FirstOrDefault(c => c.FallPilotingSkillRoll?.RollType == PilotingSkillRollType.GyroHit);
        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll!.IsSuccessful.ShouldBeFalse();
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(6);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7);
        command.DamageData.ShouldBe(fallingDamageData);
        command.IsPilotTakingDamage.ShouldBeFalse(); // Pilot damage PSR is made because a fall occurred
        command.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        command.PilotDamagePilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        command.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeTrue(); // Rolled 7 vs TN 4
        command.PilotDamagePilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.GyroHit),
            Arg.Any<IGame>());

        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.HeavyDamage),
            Arg.Any<IGame>());

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            Arg.Any<IGame>());

        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnTwoCommands_WhenGyroHitAndHeavyDamagePsrsSucceed()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 1); // Gyro hit
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        _testMech.ApplyDamage([new HitLocationData(PartLocation.CenterTorso, totalDamageDealt, []),]);
        
        // Gyro Hit PSR: Base 4 + Mod 3 (Gyro Hit) = TN 7. Roll 8 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Gyro Hit");
        // Heavy Damage PSR: Base 4 + Mod 2 (20 damage) = TN 6. Roll 7 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");

        // Dice rolls: Gyro (succeeds), HeavyDamage (succeeds)
        SetupDiceRolls(8, 7);

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .ToList();

        // Assert
        results.Count.ShouldBe(2, "Two commands should be returned for Gyro hit and Heavy Damage PSR successes.");

        // First command (expected Gyro Hit)
        var gyroCommand = results.FirstOrDefault(c => c.FallPilotingSkillRoll?.RollType == PilotingSkillRollType.GyroHit);
        gyroCommand.IsPilotingSkillRollRequired.ShouldBeTrue();
        gyroCommand.FallPilotingSkillRoll!.IsSuccessful.ShouldBeTrue();
        gyroCommand.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(8);
        gyroCommand.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7);
        gyroCommand.DamageData.ShouldBeNull();
        gyroCommand.IsPilotTakingDamage.ShouldBeFalse();
        gyroCommand.PilotDamagePilotingSkillRoll.ShouldBeNull();

        // Second command (expected Heavy Damage)
        var heavyDamageCommand = results.FirstOrDefault(c => c.FallPilotingSkillRoll?.RollType == PilotingSkillRollType.HeavyDamage);
        heavyDamageCommand.IsPilotingSkillRollRequired.ShouldBeTrue();
        heavyDamageCommand.FallPilotingSkillRoll!.IsSuccessful.ShouldBeTrue();
        heavyDamageCommand.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        heavyDamageCommand.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6);
        heavyDamageCommand.DamageData.ShouldBeNull();
        heavyDamageCommand.IsPilotTakingDamage.ShouldBeFalse();
        heavyDamageCommand.PilotDamagePilotingSkillRoll.ShouldBeNull();

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.GyroHit),
            Arg.Any<IGame>());

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.HeavyDamage),
            Arg.Any<IGame>());

        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            Arg.Any<IGame>());

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldNotReturnCommand_WhenNoFallConditionsMet()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No fall-inducing critical hits

        // Ensure heavy damage PSR is not triggered by mocking RulesProvider if necessary, 
        // or rely on the default ClassicBattletechRulesProvider threshold (usually 20).
        // For this test, 5 damage should be safe.

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.ShouldBeEmpty("No commands should be returned when no fall conditions are met.");

        // Verify no PSR calculations for fall reasons were attempted
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            Arg.Is<Unit>(u => u == _testMech),
            Arg.Is<PilotingSkillRollType>(type => 
                type == PilotingSkillRollType.GyroHit || 
                type == PilotingSkillRollType.LowerLegActuatorHit || 
                type == PilotingSkillRollType.HeavyDamage),
            Arg.Any<IGame>());

        // Verify no pilot damage PSR was attempted
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech,
            Arg.Any<PilotingSkillRollType>(),
            _game);

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommand_WhenHeavyDamagePsrFails()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No critical hits
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        _testMech.ApplyDamage([new HitLocationData(PartLocation.CenterTorso, totalDamageDealt, []),]);
        
        // Heavy Damage PSR: Base 4 + Mod (e.g., 2 for 20 damage) = TN 6. Roll 5 -> Fails.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");
        
        // PilotDamageFromFall PSR: Base 4 + Mod (e.g., 0) = TN 4. Roll 7 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot Damage PSR");

        // Dice rolls: First for HeavyDamage (fails), second for PilotDamage (succeeds)
        SetupDiceRolls(5, 7);
        
        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.ShouldHaveSingleItem("A single command should be returned for a failed Heavy Damage PSR.");
        var command = results.Single();

        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.HeavyDamage);
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeFalse();
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(5);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6); 

        command.DamageData.ShouldBe(fallingDamageData);
        command.IsPilotTakingDamage.ShouldBeFalse();
        command.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        command.PilotDamagePilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        command.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeTrue(); // Rolled 7 vs TN 4
        command.PilotDamagePilotingSkillRoll.DiceResults.Sum().ShouldBe(7);

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type==PilotingSkillRollType.HeavyDamage),
            _game);
        
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type==PilotingSkillRollType.PilotDamageFromFall),
            _game); // totalDamageDealt is passed for context, even if not directly used by PilotDamage PSR modifiers in this setup

        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }
   
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulHeavyDamagePsr_WhenHeavyDamagePsrSucceeds()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No critical hits
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        _testMech.ApplyDamage([new HitLocationData(PartLocation.CenterTorso, totalDamageDealt, []),]);
        
        // Heavy Damage PSR: Base 4 + Mod (e.g., 2 for 20 damage) = TN 6. Roll 7 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");
        
        // Dice roll for HeavyDamage PSR (succeeds)
        SetupDiceRolls(7);
        
        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.ShouldHaveSingleItem("A single command should be returned for a successful Heavy Damage PSR.");
        var command = results.Single();

        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.HeavyDamage);
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeTrue();
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6);

        command.DamageData.ShouldBeNull();
        command.PilotDamagePilotingSkillRoll.ShouldBeNull();
        command.IsPilotTakingDamage.ShouldBeFalse();

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type==PilotingSkillRollType.HeavyDamage),
            _game);
        
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type==PilotingSkillRollType.PilotDamageFromFall),
            _game);

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldNotReturnCommand_WhenGyroHitReportedForMechWithoutGyro()
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

        // Ensure heavy damage PSR is not triggered
        // (RulesProvider is ClassicBattletechRulesProvider, GetHeavyDamageThreshold defaults to 20)

        // Act
        var commands = _sut.ProcessPotentialFall(_testMech,
            _game,
            componentHits);

        // Assert
        commands.ShouldBeEmpty();

        // Ensure no PSR was attempted for a GyroHit because the mech has no gyro
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech, 
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.GyroHit), 
            Arg.Any<IGame>());
        
        // Also ensure no pilot damage PSR was attempted as no fall should be processed
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech, 
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall), 
            Arg.Any<IGame>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedFallAndPilotDamagePsrs_WhenBothPsrsFail()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 3); // Gyro hit

        // Setup GyroHit PSR to fail.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
        
        // Setup PilotDamageFromFall PSR to fail.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage"); 

        // Dice rolls:
        // First roll (6) for GyroHit PSR (6 < 7 fails).
        // Second roll (6) for PilotDamageFromFall PSR (6 < 7 fails).
        SetupDiceRolls(6, 6); 

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .First();

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
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.GyroHit),
            _game);

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            _game);
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedLlaPsrAndPilotDamagePsr_WhenLlaPsrFails()
    {
        // Arrange
        // The FallProcessor uses FallInducingCriticalsMap which maps MakaMekComponent.LowerLegActuator.
        var componentHits = SetupCriticalHits(MakaMekComponent.LowerLegActuator, 1); 

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
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits).First();

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData);

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.LowerLegActuatorHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(4);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (LLA mod)

        result.IsPilotingSkillRollRequired.ShouldBeTrue();
        result.IsPilotTakingDamage.ShouldBeFalse(); 
        
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue(); // Based on dice roll 5 vs target 4
        result.PilotDamagePilotingSkillRoll?.DiceResults.Sum().ShouldBe(5);
        result.PilotDamagePilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(4);

        // Verify GetPsrBreakdown was called for LowerLegActuatorHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.LowerLegActuatorHit),
            _game);

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            _game);
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedUlaPsrAndPilotDamagePsr_WhenUlaPsrFails()
    {
        // Arrange
        // The FallProcessor uses FallInducingCriticalsMap which maps MakaMekComponent.UpperLegActuator.
        var componentHits = SetupCriticalHits(MakaMekComponent.UpperLegActuator, 1); 

        // Setup ULA Hit PSR to fail.
        // BasePilotingSkill = 4. ULA Hit Mod +1 (from memory/typical rules). TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.UpperLegActuatorHit, 1, "Upper Leg Actuator Hit");
        
        // Setup PilotDamageFromFall PSR.
        // BasePilotingSkill = 4. No specific modifier for this example. TargetNumber = 4.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage from fall"); 

        // Dice rolls:
        // First roll (4) for ULA Hit PSR (4 < 5 fails).
        // Second roll (5) for PilotDamageFromFall PSR (5 >= 4 succeeds).
        SetupDiceRolls(4, 5); 

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits).First();

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData);

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.UpperLegActuatorHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(4);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (ULA mod)

        result.IsPilotingSkillRollRequired.ShouldBeTrue();
        result.IsPilotTakingDamage.ShouldBeFalse(); 
        
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue(); // Based on dice roll 5 vs target 4
        result.PilotDamagePilotingSkillRoll?.DiceResults.Sum().ShouldBe(5);
        result.PilotDamagePilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(4);

        // Verify GetPsrBreakdown was called for UpperLegActuatorHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.UpperLegActuatorHit),
            _game);

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            _game);
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulLlaPsr_WhenLlaPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.LowerLegActuator, 1); // LLA hit

        // Setup LLA Hit PSR to succeed.
        // BasePilotingSkill = 4. LLA Hit Mod +1. TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.LowerLegActuatorHit, 1, "Lower Leg Actuator Hit");
        
        // Dice roll: 8 for LLA Hit PSR (8 >= 5 succeeds).
        SetupDiceRolls(8); 

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _game, componentHits).ToList();

        // Assert
        results.ShouldHaveSingleItem("A single command should be returned for a successful LLA PSR.");
        var command = results.Single();

        command.GameOriginId.ShouldBe(_gameId);
        command.UnitId.ShouldBe(_testMech.Id);
        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeTrue();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.LowerLegActuatorHit);
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(8);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (LLA mod)
        command.DamageData.ShouldBeNull();
        command.PilotDamagePilotingSkillRoll.ShouldBeNull();
        command.IsPilotTakingDamage.ShouldBeFalse();

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.LowerLegActuatorHit),
            _game);
            
        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedHipActuatorPsr_WhenHipActuatorPsrFails()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Hip, 1); // Hip actuator hit

        // Setup Hip Actuator Hit PSR to fail.
        // BasePilotingSkill = 4. Hip Actuator Hit Mod +2. TargetNumber = 6.
        SetupPsrFor(PilotingSkillRollType.HipActuatorHit, 2, "Hip Actuator Hit");

        // Setup PilotDamageFromFall PSR.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage from fall");

        // Dice rolls: Hip Actuator PSR (fails), PilotDamage PSR (succeeds)
        SetupDiceRolls(5, 6);

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits).Single();

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData);

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.HipActuatorHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(5);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6); // 4 (base) + 2 (hip actuator mod)

        result.IsPilotingSkillRollRequired.ShouldBe(true);
        result.IsPilotTakingDamage.ShouldBe(false); // Pilot damage PSR succeeded
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue();

        // Verify GetPsrBreakdown was called for HipActuatorHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.HipActuatorHit),
            _game);
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulHipActuatorPsr_WhenHipActuatorPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Hip, 1); // Hip actuator hit

        // Setup Hip Actuator Hit PSR to succeed.
        // BasePilotingSkill = 4. Hip Actuator Hit Mod +2. TargetNumber = 6.
        SetupPsrFor(PilotingSkillRollType.HipActuatorHit, 2, "Hip Actuator Hit");

        // Dice roll: 8 for Hip Actuator Hit PSR (8 >= 6 succeeds).
        SetupDiceRolls(8);

        // Act
        var command = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .First();

        // Assert
        command.GameOriginId.ShouldBe(_gameId);
        command.UnitId.ShouldBe(_testMech.Id);
        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeTrue();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.HipActuatorHit);
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(8);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6); // 4 (base) + 2 (hip actuator mod)
        command.DamageData.ShouldBeNull();
        command.PilotDamagePilotingSkillRoll.ShouldBeNull();
        command.IsPilotTakingDamage.ShouldBe(false);

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.HipActuatorHit),
            _game);

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithFailedFootActuatorPsr_WhenFootActuatorPsrFails()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.FootActuator, 1); // Foot actuator hit

        // Setup Foot Actuator Hit PSR to fail.
        // BasePilotingSkill = 4. Foot Actuator Hit Mod +1. TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.FootActuatorHit, 1, "Foot Actuator Hit");

        // Setup PilotDamageFromFall PSR.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage from fall");

        // Dice rolls: Foot Actuator PSR (fails), PilotDamage PSR (succeeds)
        SetupDiceRolls(4, 5);

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits).Single();

        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.DamageData.ShouldBe(fallingDamageData);

        result.FallPilotingSkillRoll.ShouldNotBeNull();
        result.FallPilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.FootActuatorHit);
        result.FallPilotingSkillRoll?.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll?.DiceResults.Sum().ShouldBe(4);
        result.FallPilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (foot actuator mod)

        result.IsPilotingSkillRollRequired.ShouldBe(true);
        result.IsPilotTakingDamage.ShouldBe(false); // Pilot damage PSR succeeded
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue();

        // Verify GetPsrBreakdown was called for FootActuatorHit
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.FootActuatorHit),
            _game);
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulFootActuatorPsr_WhenFootActuatorPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.FootActuator, 1); // Foot actuator hit

        // Setup Foot Actuator Hit PSR to succeed.
        // BasePilotingSkill = 4. Foot Actuator Hit Mod +1. TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.FootActuatorHit, 1, "Foot Actuator Hit");

        // Dice roll: 7 for Foot Actuator Hit PSR (7 >= 5 succeeds).
        SetupDiceRolls(7);

        // Act
        var command = _sut.ProcessPotentialFall(_testMech, _game, componentHits)
            .First();

        // Assert
        command.GameOriginId.ShouldBe(_gameId);
        command.UnitId.ShouldBe(_testMech.Id);
        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll.ShouldNotBeNull();
        command.FallPilotingSkillRoll.IsSuccessful.ShouldBeTrue();
        command.FallPilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.FootActuatorHit);
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(5); // 4 (base) + 1 (foot actuator mod)
        command.DamageData.ShouldBeNull();
        command.PilotDamagePilotingSkillRoll.ShouldBeNull();
        command.IsPilotTakingDamage.ShouldBe(false);

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.FootActuatorHit),
            _game);

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenLegIsDestroyed(PartLocation destroyedLeg)
    {
        // Arrange
        var componentHits = new List<ComponentHitData>();
        
        // Set up destroyed leg locations
        var destroyedLegLocations = new List<PartLocation> { destroyedLeg };
        
        // Setup piloting skill calculator for pilot damage
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage");
        
        SetupDiceRolls(5); // For pilot damage PSR
        
        // Setup falling damage calculator
        var fallingDamageData = GetFallingDamageData();
        
        _mockFallingDamageCalculator.CalculateFallingDamage(
                Arg.Any<Unit>(),
                Arg.Is<int>(i => i == 0),
                Arg.Is<bool>(b => b == false))
            .Returns(fallingDamageData);
        
        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _game, componentHits, destroyedLegLocations).First();
        
        // Assert
        result.GameOriginId.ShouldBe(_gameId);
        result.UnitId.ShouldBe(_testMech.Id);
        result.LevelsFallen.ShouldBe(0);
        result.WasJumping.ShouldBe(false);
        result.DamageData.ShouldBe(fallingDamageData);
        result.IsPilotTakingDamage.ShouldBe(true);
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeFalse();
        result.FallPilotingSkillRoll.ShouldBeNull(); // No PSR for leg destroyed - automatic fall
        result.IsPilotingSkillRollRequired.ShouldBe(false);
        
        // Verify that GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.PilotDamageFromFall),
            Arg.Any<IGame>());
        
        // Verify falling damage was calculated
        _mockFallingDamageCalculator.Received().CalculateFallingDamage(
            Arg.Any<Unit>(),
            Arg.Is<int>(i => i == 0),
            Arg.Is<bool>(b => b == false));
    }
    
    [Fact]
    public void ProcessStandupAttempt_ShouldReturnFallContextData_WithSuccessfulStandupPsr()
    {
        // Arrange
        // Setup StandupAttempt PSR to succeed
        // BasePilotingSkill = 4. With modifierValue = 2, TargetNumber = 6.
        SetupPsrFor(PilotingSkillRollType.StandupAttempt, 2, "Standing up from prone");
        
        // Dice roll: 7 for StandupAttempt PSR (7 >= 6 succeeds).
        SetupDiceRolls(7);

        // Act
        var result = _sut.ProcessMovementAttempt(_testMech, FallReasonType.StandUpAttempt, _game);

        // Assert
        result.ShouldNotBeNull();
        result.UnitId.ShouldBe(_testMech.Id);
        result.GameId.ShouldBe(_gameId);
        result.ReasonType.ShouldBe(FallReasonType.StandUpAttempt);
        result.IsFalling.ShouldBeFalse("Mech should not be falling when standup PSR succeeds");
        
        result.PilotingSkillRoll.ShouldNotBeNull();
        result.PilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.StandupAttempt);
        result.PilotingSkillRoll.IsSuccessful.ShouldBeTrue();
        result.PilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        result.PilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6); // 4 (base) + 2 (standup mod)
        
        result.PilotDamagePilotingSkillRoll.ShouldBeNull("No pilot damage PSR should be made for successful standup");
        result.FallingDamageData.ShouldBeNull("No falling damage should be calculated for successful standup");
        
        // Verify GetPsrBreakdown was called for StandupAttempt
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.StandupAttempt),
            _game); 
            
        // Verify no falling damage calculation occurred
        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(
            Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessStandupAttempt_ShouldReturnFallContextData_WithFailedStandupPsr()
    {
        // Arrange
        // Setup StandupAttempt PSR to fail
        // BasePilotingSkill = 4. With modifierValue = 2, TargetNumber = 6.
        SetupPsrFor(PilotingSkillRollType.StandupAttempt, 2, "Standing up from prone");
        // Setup PilotDamageFromFall PSR.
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 0, "Pilot taking damage from fall");

        // Dice roll: 5 for StandupAttempt PSR (5 < 6 fails), 6 for pilot damage (6 > 4).
        SetupDiceRolls(5,6);

        // Act
        var result = _sut.ProcessMovementAttempt(_testMech, FallReasonType.StandUpAttempt, _game);

        // Assert
        result.ShouldNotBeNull();
        result.UnitId.ShouldBe(_testMech.Id);
        result.GameId.ShouldBe(_gameId);
        result.ReasonType.ShouldBe(FallReasonType.StandUpAttempt);
        result.IsFalling.ShouldBeTrue("Mech should be considered 'falling' (remaining prone) when standup PSR fails");
        
        result.PilotingSkillRoll.ShouldNotBeNull();
        result.PilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.StandupAttempt);
        result.PilotingSkillRoll.IsSuccessful.ShouldBeFalse();
        result.PilotingSkillRoll.DiceResults.Sum().ShouldBe(5);
        result.PilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(6); // 4 (base) + 2 (standup mod)
        
        result.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        result.PilotDamagePilotingSkillRoll?.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        result.PilotDamagePilotingSkillRoll?.IsSuccessful.ShouldBeTrue(); // Based on dice roll 6 vs target 4
        result.PilotDamagePilotingSkillRoll?.DiceResults.Sum().ShouldBe(6);
        result.PilotDamagePilotingSkillRoll?.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(4);
        result.FallingDamageData.ShouldBeNull("No falling damage should be calculated for failed standup");
        
        // Verify GetPsrBreakdown was called for StandupAttempt
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<PilotingSkillRollType>(type => type == PilotingSkillRollType.StandupAttempt),
            _game); 
            
        // Verify falling damage calculation occurred
        _mockFallingDamageCalculator.Received().CalculateFallingDamage(
            Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    private void SetupPsrFor(PilotingSkillRollType psrType, int modifierValue, string modifierName)
    {
        _mockPilotingSkillCalculator.GetPsrBreakdown(
                Arg.Any<Unit>(),
                Arg.Is<PilotingSkillRollType>(type => type == psrType),
                Arg.Any<IGame>())
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

        public override string Render(ILocalizationService localizationService)
        {
            return Name;
        }
    }
}
