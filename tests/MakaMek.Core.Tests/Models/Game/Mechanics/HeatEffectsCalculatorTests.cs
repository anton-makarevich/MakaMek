using NSubstitute;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class HeatEffectsCalculatorTests
{
    private readonly IDiceRoller _diceRoller = Substitute.For<IDiceRoller>();
    private readonly IRulesProvider _rulesProvider = Substitute.For<IRulesProvider>();
    private readonly ICriticalHitsCalculator _criticalHitsCalculator = Substitute.For<ICriticalHitsCalculator>();
    private readonly HeatEffectsCalculator _sut;

    public HeatEffectsCalculatorTests()
    {
        _sut = new HeatEffectsCalculator(_rulesProvider, _diceRoller, _criticalHitsCalculator);

        // Default mocks can be overridden in specific tests
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(4);
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(4);
    }

    
    private Mech CreateTestMechWithAmmo()
    {
        var mech = CreateTestMech();
        var centerTorso = mech.Parts[PartLocation.CenterTorso];

        // Add a single ammo component
        var ammo = AmmoTests.CreateAmmo(Lrm5, 24);
        centerTorso.TryAddComponent(ammo, [10]).ShouldBeTrue();

        return mech;
    }

    private Mech CreateTestMechWithMultipleAmmo()
    {
        var mech = CreateTestMech();
        var centerTorso = mech.Parts[PartLocation.CenterTorso];

        // Add multiple ammo components with different damage values
        var ammo1 = AmmoTests.CreateAmmo(Lrm5, 24); // 5 * 24 = 120 damage
        var ammo2 = AmmoTests.CreateAmmo(Lrm5, 24); // 5 * 24 = 120 damage

        centerTorso.TryAddComponent(ammo1, [10]).ShouldBeTrue();
        centerTorso.TryAddComponent(ammo2, [11]).ShouldBeTrue();

        return mech;
    }

    // Weapon definitions for testing
    private static readonly WeaponDefinition Lrm5 = new(
        Name: "LRM-5",
        ElementaryDamage: 1,
        Heat: 2,
        Range: new WeaponRange(6, 7, 14, 21),
        Type: WeaponType.Missile,
        BattleValue: 45,
        Clusters: 1,
        ClusterSize: 5,
        FullAmmoRounds: 24,
        WeaponComponentType: MakaMekComponent.LRM5,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM5);

    [Fact]
    public void GetAmmoExplosionAvoidNumber_ShouldReturnCorrectValue()
    {
        // Arrange
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(20).Returns(6);

        // Act
        var result = _sut.GetAmmoExplosionAvoidNumber(20);

        // Assert
        result.ShouldBe(6);
        _rulesProvider.Received(1).GetHeatAmmoExplosionAvoidNumber(20);
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReturnNull_WhenHeatBelowThreshold()
    {
        // Arrange
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(0);
        var mech = CreateTestMech();
        SetMechHeat(mech, 15);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReturnNull_WhenNoExplodableAmmo()
    {
        // Arrange
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(4);
        var mech = CreateTestMech();
        var ammo = mech.GetAvailableComponents<Ammo>();
        ammo.ToList().ForEach(a => a.UnMount());
        SetMechHeat(mech, 20);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReturnSuccessfulCommand_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMechWithAmmo();
        SetMechHeat(mech, 25);

        // Setup dice roll that succeeds (rolls 7, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(4) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        result.Value.UnitId.ShouldBe(mech.Id);
        result.Value.AvoidExplosionRoll.ShouldNotBeNull();
        result.Value.AvoidExplosionRoll.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidExplosionRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidExplosionRoll.DiceResults.ShouldBe([3, 4]);
        result.Value.CriticalHits.ShouldBeEmpty();
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReturnCommand_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMechWithAmmo();
        SetMechHeat(mech, 25);

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);
        
        // Setup critical hits calculator
        var criticalHits = new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1, [
                new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }
            ],false);
        
        _criticalHitsCalculator.CalculateCriticalHitsForHeatExplosion(
                Arg.Any<Unit>(), Arg.Any<Ammo>())
            .Returns(new HeatExplosionResolution([criticalHits], null, false));

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        result.Value.UnitId.ShouldBe(mech.Id);
        result.Value.AvoidExplosionRoll.ShouldNotBeNull();
        result.Value.AvoidExplosionRoll.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidExplosionRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidExplosionRoll.DiceResults.ShouldBe([2, 3]);
        result.Value.CriticalHits.ShouldNotBeNull();
        result.Value.CriticalHits.Count.ShouldBe(1);
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldCopyCriticalHitDestructionMetadata()
    {
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);

        var mech = CreateTestMechWithAmmo();
        SetMechHeat(mech, 25);
        _diceRoller.Roll2D6().Returns([new DiceResult(2), new DiceResult(3)]);

        var criticalHits = new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1, [
                new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }
            ], false);
        _criticalHitsCalculator.CalculateCriticalHitsForHeatExplosion(
                Arg.Any<Unit>(), Arg.Any<Ammo>())
            .Returns(new HeatExplosionResolution(
                [criticalHits],
                [PartLocation.RightTorso],
                true));

        var result = _sut.CheckForHeatAmmoExplosion(mech);

        result.ShouldNotBeNull();
        result.Value.DestroyedParts.ShouldBe([PartLocation.RightTorso]);
        result.Value.UnitDestroyed.ShouldBeTrue();
        mech.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldSelectMostDestructiveAmmo()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);

        var mech = CreateTestMechWithMultipleAmmo();
        mech.Parts[PartLocation.LeftTorso].TryAddComponent(AmmoTests.CreateAmmo(Lrm5, 2)).ShouldBeTrue();
        SetMechHeat(mech, 25);

        // Setup dice roll that fails to trigger explosion
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);
        _diceRoller.RollD6().Returns(new DiceResult(5));

        // Setup critical hits calculator
        var criticalHits = new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1, [
                new ComponentHitData { Slot = 10, Type = MakaMekComponent.ISAmmoLRM5 }
            ],false);
        
        _criticalHitsCalculator.CalculateCriticalHitsForHeatExplosion(
                Arg.Any<Unit>(), Arg.Any<Ammo>())
            .Returns(new HeatExplosionResolution([criticalHits], null, false));

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        var expectedAmmoFail =
            mech.GetAvailableComponents<Ammo>()
                .OrderByDescending(a => a.GetExplosionDamage())
                .First();
        _criticalHitsCalculator.Received(1).CalculateCriticalHitsForHeatExplosion(mech, expectedAmmoFail);
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldSelectMostDestructiveAmmo_WhenMoreThanOneAvailable()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMechWithMultipleAmmo();
        SetMechHeat(mech, 25);

        // Setup dice roll that fails to trigger explosion
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);
        _diceRoller.RollD6().Returns(new DiceResult(5));

        // Setup critical hits calculator
        var criticalHits = new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1, [
                new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }
            ],false);
        _criticalHitsCalculator.CalculateCriticalHitsForHeatExplosion(Arg.Any<Unit>(),
                Arg.Any<Ammo>())
            .Returns(new HeatExplosionResolution([criticalHits], null, false));

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        var expectedAmmoFail =
            mech.GetAvailableComponents<Ammo>()
                .OrderByDescending(a => a.GetExplosionDamage())
                .First();
        expectedAmmoFail.ComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM5);
        _criticalHitsCalculator.Received(1).CalculateCriticalHitsForHeatExplosion(mech,
            Arg.Any<Ammo>());
        _diceRoller.Received(1).RollD6();
    }
    
    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldNotReportPreexistingDestroyedParts_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);

        var mech = CreateTestMechWithAmmo();
        // Destroy the left arm before the explosion check
        mech.Parts[PartLocation.LeftArm].ApplyDamage(10, HitDirection.Front);
        mech.Parts[PartLocation.LeftArm].IsDestroyed.ShouldBeTrue();
        SetMechHeat(mech, 25);

        // Setup dice roll that succeeds (rolls 7, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(4) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        result.Value.AvoidExplosionRoll.ShouldNotBeNull();
        result.Value.AvoidExplosionRoll.IsSuccessful.ShouldBeTrue();
        result.Value.CriticalHits.ShouldBeEmpty();
        result.Value.DestroyedParts.ShouldBeNull(); // Pre-existing destruction is not reported
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReportDestroyedParts_WhenExplosionDestroysLocation()
    {
        // Arrange
        var damageTransferCalculator = Substitute.For<IDamageTransferCalculator>();
        // Use the real critical hits calculator so the explosion is actually simulated
        var sut = new HeatEffectsCalculator(
            _rulesProvider,
            _diceRoller,
            new CriticalHitsCalculator(_diceRoller, damageTransferCalculator, CreateMechFactory()));

        var mech = CreateTestMech();
        var centerTorso = mech.Parts[PartLocation.CenterTorso];
        var leftTorso = mech.Parts[PartLocation.LeftTorso];

        // Explodable ammo in the center torso (the heat-triggered explosion)
        var explodingAmmo = AmmoTests.CreateAmmo(Lrm5, 24);
        centerTorso.TryAddComponent(explodingAmmo, [10]).ShouldBeTrue();

        // Ammo in the left torso that will be hit by the critical hit caused by the explosion
        var chainAmmo = AmmoTests.CreateAmmo(Lrm5, 1);
        leftTorso.TryAddComponent(chainAmmo, [8]).ShouldBeTrue();
        // Additional 0-shot ammo fillers in the left torso so the critical slot roll
        // uses the group selection logic (> 6 available slots)
        foreach (var slot in new[] { 5, 6, 7, 9, 10, 11 })
        {
            leftTorso.TryAddComponent(AmmoTests.CreateAmmo(Lrm5, 0), [slot]).ShouldBeTrue();
        }

        SetMechHeat(mech, 25);

        // First roll: explosion avoidance check fails (3 < 4)
        // Second roll: critical hit roll for the left torso (8 -> 1 crit)
        _diceRoller.Roll2D6().Returns(
            [new DiceResult(1), new DiceResult(2)],
            [new DiceResult(4), new DiceResult(4)]);
        // First roll: slot group selection (slots >= 6)
        // Second roll: slot 3 -> slot 8 (the chain ammo)
        _diceRoller.RollD6().Returns(new DiceResult(5), new DiceResult(3));

        // The initial blast damages, but does not destroy, the left torso so the chain ammo can still be hit.
        damageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.CenterTorso),
                Arg.Any<int>())
            .Returns([new LocationDamageData(PartLocation.LeftTorso, 0, 1, false)]);
        // ...and the critical hit on the left torso ammo damages the right torso enough to destroy it
        damageTransferCalculator.CalculateExplosionDamage(
                Arg.Any<Unit>(),
                Arg.Is<PartLocation>(l => l == PartLocation.LeftTorso),
                Arg.Any<int>())
            .Returns([new LocationDamageData(PartLocation.RightTorso, 0, 5, false)]);

        // Act
        var result = sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        result.Value.CriticalHits.ShouldNotBeEmpty();
        result.Value.DestroyedParts.ShouldNotBeNull();
        result.Value.DestroyedParts!.ShouldContain(PartLocation.RightTorso);
        result.Value.UnitDestroyed.ShouldBeFalse();
        // The calculator simulates rather than applies: the mech is damaged later, when the
        // AmmoExplosionCommand is handled, so it must still be intact here.
        mech.Parts[PartLocation.RightTorso].IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnNull_WhenMechIsAlreadyShutdown()
    {
        // Arrange
        var mech = CreateTestMech();
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnNull_WhenAvoidNumberIsZero()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(0);
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenAvoidNumberIs13()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(13);
        var mech = CreateTestMech();
        SetMechHeat(mech, 30);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        _diceRoller.DidNotReceive().Roll2D6();
    }
    
    [Fact]
    public void CheckForHeatShutdown_ShouldReturnAutomaticShutdown_WhenPilotIsUnconscious()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(6);
        var mech = CreateTestMech();
        SetMechHeat(mech, 20);
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        mech.AssignPilot(pilot);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnFailedShutdown_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6; // Any number between 4-10 would work here
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([2, 3]);
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.ShutdownData.Reason.ShouldBe(ShutdownReason.Heat);
        result.Value.ShutdownData.Turn.ShouldBe(1);
        _rulesProvider.Received(1).GetHeatShutdownAvoidNumber(mech.CurrentHeat);
        _diceRoller.Received(1).Roll2D6();
    }

    [Fact]
    public void CheckForHeatShutdown_ShouldReturnSuccess_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6; // Any number between 4-10 would work here
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);

        // Setup dice roll that succeeds (rolls 6, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.CheckForHeatShutdown(mech, 1);

        // Assert
        result.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll!.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.IsAutomaticShutdown.ShouldBeFalse();
        result.Value.ShutdownData.Turn.ShouldBe(1);
        _rulesProvider.Received(1).GetHeatShutdownAvoidNumber(mech.CurrentHeat);
        _diceRoller.Received(1).Roll2D6();
    }

    private static MechFactory CreateMechFactory()
    {
        return new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
    }

    private static Mech CreateTestMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return CreateMechFactory().Create(mechData);
    }

    private static void SetMechHeat(Mech mech, int heatLevel)
    {
        var heatData = new HeatData
        {
            MovementHeatSources = [],
            WeaponHeatSources = [
                new WeaponHeatData
                {
                    WeaponName = "TestWeapon",
                    HeatPoints = heatLevel
                }
            ],
            ExternalHeatSources = [],
            DissipationData = default
        };
        mech.ApplyHeat(heatData);
    }

    [Fact]
    public void AttemptRestart_ShouldReturnNull_WhenMechIsNotShutdown()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        // mech is not shut down

        // Act
        var result = _sut.AttemptRestart(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnNull_WhenShutdownInSameTurn()
    {
        // Arrange
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act - Try to restart in the same turn
        var result = _sut.AttemptRestart(mech, 1);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnAutoRestart_WhenAvoidNumberIsZero()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(0);
        var mech = CreateTestMech();
        SetMechHeat(mech, 10);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act - Try to restart in a later turn
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeTrue();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnRestartImpossible_WhenAvoidNumberIs13()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(13);
        var mech = CreateTestMech();
        SetMechHeat(mech, 30);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnRestartImpossible_WhenPilotIsUnconscious()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(6);
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(false);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.ShouldBeNull();
        _diceRoller.DidNotReceive().Roll2D6();
    }

    [Fact]
    public void AttemptRestart_ShouldReturnSuccessfulRestart_WhenRollSucceeds()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Setup dice roll that succeeds (rolls 6, needs 6+)
        var diceResults = new List<DiceResult> { new(3), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll.IsSuccessful.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([3, 3]);
    }

    [Fact]
    public void AttemptRestart_ShouldReturnFailedRestart_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMech();
        var pilot = Substitute.For<IPilot>();
        pilot.IsConscious.Returns(true);
        mech.AssignPilot(pilot);
        SetMechHeat(mech, 20);
        mech.Shutdown(new ShutdownData { Reason = ShutdownReason.Heat, Turn = 1 });

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Act
        var result = _sut.AttemptRestart(mech, 2);

        // Assert
        result.ShouldNotBeNull();
        result.Value.IsAutomaticRestart.ShouldBeFalse();
        result.Value.IsRestartPossible.ShouldBeTrue();
        result.Value.AvoidShutdownRoll.ShouldNotBeNull();
        result.Value.AvoidShutdownRoll.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidShutdownRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidShutdownRoll.DiceResults.ShouldBe([2, 3]);
    }

    [Fact]
    public void GetShutdownAvoidNumber_ShouldReturnCorrectValue()
    {
        // Arrange
        _rulesProvider.GetHeatShutdownAvoidNumber(20).Returns(8);

        // Act
        var result = _sut.GetShutdownAvoidNumber(20);

        // Assert
        result.ShouldBe(8);
        _rulesProvider.Received(1).GetHeatShutdownAvoidNumber(20);
    }
}
