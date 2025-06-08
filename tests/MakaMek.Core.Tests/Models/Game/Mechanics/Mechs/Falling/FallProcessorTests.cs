using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
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
    private readonly IRulesProvider _rulesProvider;
    private readonly IPilotingSkillCalculator _mockPilotingSkillCalculator;
    private readonly IDiceRoller _mockDiceRoller;
    private readonly IFallingDamageCalculator _mockFallingDamageCalculator;

    private readonly Mech _testMech;
    private readonly BattleMap _map = BattleMapTests.BattleMapFactory.GenerateMap(10, 10,
        new SingleTerrainGenerator(10,10, new ClearTerrain()));
    private readonly Guid _gameId = Guid.NewGuid();

    public FallProcessorTests()
    {
        _rulesProvider = new ClassicBattletechRulesProvider();
        _mockPilotingSkillCalculator = Substitute.For<IPilotingSkillCalculator>();
        _mockDiceRoller = Substitute.For<IDiceRoller>();
        _mockFallingDamageCalculator = Substitute.For<IFallingDamageCalculator>();

        _sut = new FallProcessor(
            _rulesProvider,
            _mockPilotingSkillCalculator,
            _mockDiceRoller,
            _mockFallingDamageCalculator);

        _testMech = new MechFactory(
            _rulesProvider,
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
            _sut.ProcessPotentialFall(_testMech, _map, componentHits, 10, _gameId).ToList();

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
            Arg.Any<BattleMap>(),
            Arg.Any<int>());
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenGyroIsDestroyed()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro,3);
        const int totalDamageDealt = 5;
    
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
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)
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
        const int totalDamageDealt = 5; // Not enough for heavy damage fall

        // Setup GyroHit PSR to succeed.
        // BasePilotingSkill = 4. With modifierValue = 3, TargetNumber = 7.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Damaged Gyro");
        
        // Dice roll: 8 for GyroHit PSR (8 >= 7 succeeds).
        SetupDiceRolls(8); 

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)
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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types =>
                types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

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
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

        // Assert
        results.Count.ShouldBe(1, "Mech falls on first command.");

        // First command (expected Gyro Hit)
        var command = results.FirstOrDefault(c => c.FallPilotingSkillRoll?.RollType == PilotingSkillRollType.GyroHit);
        command.IsPilotingSkillRollRequired.ShouldBeTrue();
        command.FallPilotingSkillRoll!.IsSuccessful.ShouldBeFalse();
        command.FallPilotingSkillRoll.DiceResults.Sum().ShouldBe(6);
        command.FallPilotingSkillRoll.PsrBreakdown.ModifiedPilotingSkill.ShouldBe(7);
        command.DamageData.ShouldBe(fallingDamageData);
        command.IsPilotTakingDamage.ShouldBeTrue(); // Pilot damage PSR is made because a fall occurred
        command.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        command.PilotDamagePilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        command.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeTrue(); // Rolled 7 vs TN 4
        command.PilotDamagePilotingSkillRoll.DiceResults.Sum().ShouldBe(7);
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.HeavyDamage)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnTwoCommands_WhenGyroHitAndHeavyDamagePsrsSucceed()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.Gyro, 1); // Gyro hit
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        
        // Gyro Hit PSR: Base 4 + Mod 3 (Gyro Hit) = TN 7. Roll 8 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.GyroHit, 3, "Gyro Hit");
        // Heavy Damage PSR: Base 4 + Mod 2 (20 damage) = TN 6. Roll 7 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");

        // Dice rolls: Gyro (succeeds), HeavyDamage (succeeds)
        SetupDiceRolls(8, 7);

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.GyroHit)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.HeavyDamage)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            Arg.Any<Unit>(),
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldNotReturnCommand_WhenNoFallConditionsMet()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No fall-inducing critical hits
        const int totalDamageDealt = 5; // Damage below heavy damage threshold

        // Ensure heavy damage PSR is not triggered by mocking RulesProvider if necessary, 
        // or rely on the default ClassicBattletechRulesProvider threshold (usually 20).
        // For this test, 5 damage should be safe.

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

        // Assert
        results.ShouldBeEmpty("No commands should be returned when no fall conditions are met.");

        // Verify no PSR calculations for fall reasons were attempted
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => 
                types.Contains(PilotingSkillRollType.GyroHit) || 
                types.Contains(PilotingSkillRollType.LowerLegActuatorHit) || 
                types.Contains(PilotingSkillRollType.HeavyDamage)),
            _map,
            totalDamageDealt);

        // Verify no pilot damage PSR was attempted
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            totalDamageDealt);

        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommand_WhenHeavyDamagePsrFails()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No critical hits
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        
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
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

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
        command.IsPilotTakingDamage.ShouldBeTrue();
        command.PilotDamagePilotingSkillRoll.ShouldNotBeNull();
        command.PilotDamagePilotingSkillRoll.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
        command.PilotDamagePilotingSkillRoll.IsSuccessful.ShouldBeTrue(); // Rolled 7 vs TN 4
        command.PilotDamagePilotingSkillRoll.DiceResults.Sum().ShouldBe(7);

        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.HeavyDamage)),
            _map,
            Arg.Any<int>());
        
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            Arg.Any<int>()); // totalDamageDealt is passed for context, even if not directly used by PilotDamage PSR modifiers in this setup

        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }
   
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulHeavyDamagePsr_WhenHeavyDamagePsrSucceeds()
    {
        // Arrange
        var componentHits = new List<ComponentHitData>(); // No critical hits
        const int totalDamageDealt = 20; // Damage at/above heavy damage threshold
        
        // Heavy Damage PSR: Base 4 + Mod (e.g., 2 for 20 damage) = TN 6. Roll 7 -> Succeeds.
        SetupPsrFor(PilotingSkillRollType.HeavyDamage, 2, "Heavy Damage (20pts)");
        
        // Dice roll for HeavyDamage PSR (succeeds)
        SetupDiceRolls(7);
        
        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.HeavyDamage)),
            _map,
            Arg.Any<int>());
        
        _mockPilotingSkillCalculator.DidNotReceive().GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            Arg.Any<int>());

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
        const int totalDamageDealt = 5; // Damage below heavy damage threshold

        // Ensure heavy damage PSR is not triggered
        // (RulesProvider is ClassicBattletechRulesProvider, GetHeavyDamageThreshold defaults to 20)

        // Act
        var commands = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId);

        // Assert
        commands.ShouldBeEmpty();

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
        SetupPsrFor(PilotingSkillRollType.PilotDamageFromFall, 3, "Pilot taking damage"); 

        // Dice rolls:
        // First roll (6) for GyroHit PSR (6 < 7 fails).
        // Second roll (6) for PilotDamageFromFall PSR (6 < 7 fails).
        SetupDiceRolls(6, 6); 

        var fallingDamageData = GetFallingDamageData();
        _mockFallingDamageCalculator.CalculateFallingDamage(_testMech, 0, false)
            .Returns(fallingDamageData);

        // Act
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId)
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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.GyroHit)),
            _map,
            Arg.Any<int>());

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            Arg.Any<int>());
            
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
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).First();

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
            Arg.Any<int>());

        // Verify GetPsrBreakdown was called for PilotDamageFromFall
        _mockPilotingSkillCalculator.Received(1).GetPsrBreakdown(
            _testMech,
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            _map,
            Arg.Any<int>());
            
        _mockFallingDamageCalculator.Received(1).CalculateFallingDamage(_testMech, 0, false);
    }
    
    [Fact]
    public void ProcessPotentialFall_ShouldReturnCommandWithSuccessfulLlaPsr_WhenLlaPsrSucceeds()
    {
        // Arrange
        var componentHits = SetupCriticalHits(MakaMekComponent.LowerLegActuator, 1); // LLA hit
        const int totalDamageDealt = 5; // Not enough for heavy damage

        // Setup LLA Hit PSR to succeed.
        // BasePilotingSkill = 4. LLA Hit Mod +1. TargetNumber = 5.
        SetupPsrFor(PilotingSkillRollType.LowerLegActuatorHit, 1, "Lower Leg Actuator Hit");
        
        // Dice roll: 8 for LLA Hit PSR (8 >= 5 succeeds).
        SetupDiceRolls(8); 

        // Act
        var results = _sut.ProcessPotentialFall(_testMech, _map, componentHits, totalDamageDealt, _gameId).ToList();

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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => types.Contains(PilotingSkillRollType.LowerLegActuatorHit)),
            _map,
            totalDamageDealt);
            
        _mockFallingDamageCalculator.DidNotReceive().CalculateFallingDamage(Arg.Any<Unit>(), Arg.Any<int>(), Arg.Any<bool>());
    }

    [Theory]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void ProcessPotentialFall_ShouldReturnMechFallingCommand_WhenLegIsDestroyed(PartLocation destroyedLeg)
    {
        // Arrange
        var componentHits = new List<ComponentHitData>();
        const int totalDamageDealt = 5;
        
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
        var result = _sut.ProcessPotentialFall(_testMech, _map, componentHits, 
            totalDamageDealt, _gameId, destroyedLegLocations).First();
        
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
            Arg.Is<IEnumerable<PilotingSkillRollType>>(types => 
                types.Contains(PilotingSkillRollType.PilotDamageFromFall)),
            Arg.Any<BattleMap>(),
            Arg.Any<int>());
        
        // Verify falling damage was calculated
        _mockFallingDamageCalculator.Received().CalculateFallingDamage(
            Arg.Any<Unit>(),
            Arg.Is<int>(i => i == 0),
            Arg.Is<bool>(b => b == false));
    }
    
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

        public override string Render(ILocalizationService localizationService)
        {
            return Name;
        }
    }
}
