using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Commands.Server;

public class AmmoExplosionCommandTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();
    private readonly IGame _game = Substitute.For<IGame>();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Mech _testMech;

    public AmmoExplosionCommandTests()
    {
        var pilot = new MechWarrior("Test", "Pilot");
        _testMech = new Mech("Test Mech", "TST-1", 50, 4, CreateBasicPartsData(), id: _unitId);
        _testMech.AssignPilot(pilot);

        var player = Substitute.For<IPlayer>();
        player.Units.Returns([_testMech]);

        _game.Players.Returns([player]);
    }
    
    private AmmoExplosionCommand CreateCommand()
    {
        return new AmmoExplosionCommand
        {
            UnitId = _unitId,
            GameOriginId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [3, 4],
                AvoidNumber = 8,
                IsSuccessful = false
            },
            CriticalHits = []
        };
    }

    private static WeaponDefinition CreateLrm5Definition()
    {
        return new WeaponDefinition(
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
    }

    private static List<UnitPart> CreateBasicPartsData()
    {
        var centerTorso = new CenterTorso("CenterTorso", 31, 10, 6);
        centerTorso.TryAddComponent(new Engine(250));
        return
        [
            new Head("Head", 9, 3),
            centerTorso,
            new SideTorso("LeftTorso", PartLocation.LeftTorso, 25, 8, 6),
            new SideTorso("RightTorso", PartLocation.RightTorso, 25, 8, 6),
            new Arm("RightArm", PartLocation.RightArm, 17, 6),
            new Arm("LeftArm", PartLocation.LeftArm, 17, 6),
            new Leg("RightLeg", PartLocation.RightLeg, 25, 8),
            new Leg("LeftLeg", PartLocation.LeftLeg, 25, 8)
        ];
    }

    [Fact]
    public void Render_ShouldReturnEmpty_WhenUnitNotFound()
    {
        // Arrange
        var command = CreateCommand() with { UnitId = Guid.NewGuid() };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Render_ShouldFormatSuccessfulAvoidance_WhenRollSucceeds()
    {
        // Arrange
        var command = CreateCommand() with
        {
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [4, 5],
                AvoidNumber = 6,
                IsSuccessful = true
            }
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("TST-1 avoided ammo explosion due to heat");
        result.ShouldContain("Heat level: 25, Roll: 9 vs 6");
    }

    [Fact]
    public void Render_ShouldFormatFailedAvoidance_WhenRollFails()
    {
        // Arrange
        var command = CreateCommand() with
        {
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [2, 3],
                AvoidNumber = 6,
                IsSuccessful = false
            }
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("TST-1 suffered ammo explosion due to heat");
        result.ShouldContain("Heat level: 25, Roll: 5 vs 6");
    }

    [Fact]
    public void Render_ShouldIncludeCriticalHits_WhenExplosionOccursWithCriticalHits()
    {
        // Arrange
        var centerTorso = _testMech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var ammo = new Ammo(CreateLrm5Definition(), 24);
        centerTorso.TryAddComponent(ammo, [10]).ShouldBeTrue(); // Use slot 10, which is available

        var command = CreateCommand() with
        {
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [2, 3],
                AvoidNumber = 6,
                IsSuccessful = false
            },
            CriticalHits =
            [
                new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4],
                    1,
                    [new ComponentHitData { Slot = 10, Type = MakaMekComponent.ISAmmoLRM5 }],
                    false,
                    [])
            ]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("TST-1 suffered ammo explosion due to heat");
        result.ShouldContain("Explosion caused critical hits:");
        result.ShouldContain("- LRM-5 Ammo in CT destroyed by explosion");
    }

    [Fact]
    public void Render_ShouldNotIncludeCriticalHits_WhenExplosionAvoided()
    {
        // Arrange
        var command = CreateCommand() with
        {
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 25,
                DiceResults = [4, 5],
                AvoidNumber = 6,
                IsSuccessful = true
            },
            CriticalHits =
            [
                new LocationCriticalHitsData(PartLocation.CenterTorso, [4, 4], 1,
                    [new ComponentHitData { Slot = 0, Type = MakaMekComponent.ISAmmoLRM5 }],
                    false,
                    [])
            ]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldContain("TST-1 avoided ammo explosion due to heat");
        result.ShouldNotContain("Explosion caused critical hits:");
    }

    [Fact]
    public void Render_ShouldShowExplosionDetails_WhenComponentIsExplodable()
    {
        // Arrange
        var ct = _testMech.Parts.First(p => p.Location == PartLocation.CenterTorso);
        var ammo = new Ammo(CreateLrm5Definition(), 20);
        ct.TryAddComponent(ammo).ShouldBeTrue();
        
        var criticalHitData = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 5],
            1,
            [new ComponentHitData
            {
                Type = ammo.ComponentType,
                Slot = ammo.MountedAtSlots[0],
                ExplosionDamage = 100
            }],
            false,
            []);

        var command = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _unitId,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 15,
                DiceResults = [3, 4],
                AvoidNumber = 8,
                IsSuccessful = false
            },
            CriticalHits = [criticalHitData]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("exploded, damage: 100");
    }

    [Fact]
    public void Render_ShouldNotShowExplosionDetails_WhenNoExplosionsExist()
    {
        // Arrange
        var criticalHitData = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 5],
            1,
            [new ComponentHitData { Type = MakaMekComponent.Engine, Slot = 1 }],
            false,
            []); 

        var command = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _unitId,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 15,
                DiceResults = [3, 4],
                AvoidNumber = 8,
                IsSuccessful = false
            },
            CriticalHits = [criticalHitData]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldNotContain("exploded");
    }

    [Fact]
    public void Render_ShouldHandleInvalidExplosionSlot_WhenComponentNotFound()
    {
        // Arrange
        var criticalHitData = new LocationCriticalHitsData(
            PartLocation.CenterTorso,
            [4, 5],
            1,
            [new ComponentHitData { Type = MakaMekComponent.ISAmmoAC2, Slot = 1, ExplosionDamage = 100 }],
            false,
            []);

        var command = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _unitId,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 15,
                DiceResults = [3, 4],
                AvoidNumber = 8,
                IsSuccessful = false
            },
            CriticalHits = [criticalHitData]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldNotContain("exploded"); // Should not show explosion for an invalid component
    }

    [Fact]
    public void Render_ShouldShowMultipleExplosions_WhenMultipleExplosionsExist()
    {
        // Arrange
        var leftArm = _testMech.Parts.First(p => p.Location == PartLocation.LeftArm);
        var ac5 = new Ammo(Ac5.Definition, 1);
        var lrm10 = new Ammo(Lrm10.Definition, 1);
        leftArm.TryAddComponent(ac5).ShouldBeTrue();
        leftArm.TryAddComponent(lrm10).ShouldBeTrue();
        var criticalHitData = new LocationCriticalHitsData(
            PartLocation.LeftArm,
            [4, 5],
            2,
            [
                new ComponentHitData
                {
                    Type = ac5.ComponentType,
                    Slot = ac5.MountedAtSlots[0],
                    ExplosionDamage = 5
                },
                new ComponentHitData
                {
                    Type = lrm10.ComponentType,
                    Slot = lrm10.MountedAtSlots[0],
                    ExplosionDamage = 10
                }
            ],
            false,
            []);

        var command = new AmmoExplosionCommand
        {
            GameOriginId = Guid.NewGuid(),
            UnitId = _unitId,
            AvoidExplosionRoll = new AvoidAmmoExplosionRollData
            {
                HeatLevel = 15,
                DiceResults = [3, 4],
                AvoidNumber = 8,
                IsSuccessful = false
            },
            CriticalHits = [criticalHitData]
        };

        // Act
        var result = command.Render(_localizationService, _game);

        // Assert
        result.ShouldNotBeEmpty();
        result.ShouldContain("exploded, damage: 5");
        result.ShouldContain("exploded, damage: 10");
    }
}
