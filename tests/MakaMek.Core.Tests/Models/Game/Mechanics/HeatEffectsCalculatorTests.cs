using NSubstitute;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
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

        // Default mocks - can be overridden in specific tests
        _rulesProvider.GetHeatShutdownAvoidNumber(Arg.Any<int>()).Returns(4);
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(4);
    }

    
    private Mech CreateTestMechWithAmmo()
    {
        var mech = CreateTestMech();
        var centerTorso = mech.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add a single ammo component
        var ammo = new Ammo(Lrm5, 24);
        centerTorso.TryAddComponent(ammo, [0]);

        return mech;
    }

    private Mech CreateTestMechWithMultipleAmmo()
    {
        var mech = CreateTestMech();
        var centerTorso = mech.Parts.First(p => p.Location == PartLocation.CenterTorso);

        // Add multiple ammo components with different damage values
        var ammo1 = new Ammo(Lrm5, 24); // 5 * 24 = 120 damage
        var ammo2 = new Ammo(Lrm10, 5); // 10 * 5 = 50 damage (less destructive)
        var ammo3 = new Ammo(Srm2, 50); // 2 * 50 = 100 damage

        centerTorso.TryAddComponent(ammo1, [0]);
        centerTorso.TryAddComponent(ammo2, [1]);
        centerTorso.TryAddComponent(ammo3, [2]);

        return mech;
    }

    // Weapon definitions for testing
    private static readonly WeaponDefinition Lrm5 = new(
        Name: "LRM-5",
        ElementaryDamage: 1,
        Heat: 2,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 45,
        Clusters: 1,
        ClusterSize: 5,
        FullAmmoRounds: 24,
        WeaponComponentType: MakaMekComponent.LRM5,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM5);

    private static readonly WeaponDefinition Lrm10 = new(
        Name: "LRM-10",
        ElementaryDamage: 1,
        Heat: 4,
        MinimumRange: 6,
        ShortRange: 7,
        MediumRange: 14,
        LongRange: 21,
        Type: WeaponType.Missile,
        BattleValue: 90,
        Clusters: 1,
        ClusterSize: 10,
        FullAmmoRounds: 12,
        WeaponComponentType: MakaMekComponent.LRM10,
        AmmoComponentType: MakaMekComponent.ISAmmoLRM10);

    private static readonly WeaponDefinition Srm2 = new(
        Name: "SRM-2",
        ElementaryDamage: 2,
        Heat: 2,
        MinimumRange: 0,
        ShortRange: 3,
        MediumRange: 6,
        LongRange: 9,
        Type: WeaponType.Missile,
        BattleValue: 21,
        Clusters: 1,
        ClusterSize: 2,
        FullAmmoRounds: 50,
        WeaponComponentType: MakaMekComponent.SRM2,
        AmmoComponentType: MakaMekComponent.ISAmmoSRM2);
    
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
        result.Value.ExplosionCriticalHits.ShouldBeNull();
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldReturnFailedCommand_WhenRollFails()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMechWithAmmo();
        SetMechHeat(mech, 25);

        // Setup dice roll that fails (rolls 5, needs 6+)
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Setup critical hits calculator to return some critical hits
        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(PartLocation.CenterTorso, 8, 1, [new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }])
        };
        _criticalHitsCalculator.CalculateCriticalHits(Arg.Any<Unit>(), Arg.Any<PartLocation>(), Arg.Any<int>())
            .Returns(criticalHits);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        result.Value.UnitId.ShouldBe(mech.Id);
        result.Value.AvoidExplosionRoll.ShouldNotBeNull();
        result.Value.AvoidExplosionRoll.IsSuccessful.ShouldBeFalse();
        result.Value.AvoidExplosionRoll.AvoidNumber.ShouldBe(avoidNumber);
        result.Value.AvoidExplosionRoll.DiceResults.ShouldBe([2, 3]);
        result.Value.ExplosionCriticalHits.ShouldNotBeNull();
        result.Value.ExplosionCriticalHits.Count.ShouldBe(1);
    }

    [Fact]
    public void CheckForHeatAmmoExplosion_ShouldSelectMostDestructiveAmmo()
    {
        // Arrange
        const int avoidNumber = 6;
        _rulesProvider.GetHeatAmmoExplosionAvoidNumber(Arg.Any<int>()).Returns(avoidNumber);
        
        var mech = CreateTestMechWithMultipleAmmo();
        SetMechHeat(mech, 25);

        // Setup dice roll that fails to trigger explosion
        var diceResults = new List<DiceResult> { new(2), new(3) };
        _diceRoller.Roll2D6().Returns(diceResults);

        // Setup critical hits calculator
        var criticalHits = new List<LocationCriticalHitsData>
        {
            new(PartLocation.CenterTorso, 8, 1, [new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }])
        };
        _criticalHitsCalculator.CalculateCriticalHits(Arg.Any<Unit>(), Arg.Any<PartLocation>(), Arg.Any<int>())
            .Returns(criticalHits);

        // Act
        var result = _sut.CheckForHeatAmmoExplosion(mech);

        // Assert
        result.ShouldNotBeNull();
        // Verify that the critical hits calculator was called with the most destructive ammo's damage
        // SRM-2 with 50 shots = 4 * 50 = 200 damage (most destructive)
        _criticalHitsCalculator.Received(1).CalculateCriticalHits(mech, Arg.Any<PartLocation>(), 400);
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

    private static Mech CreateTestMech()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        return new MechFactory(new ClassicBattletechRulesProvider(), Substitute.For<ILocalizationService>()).Create(mechData);
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
