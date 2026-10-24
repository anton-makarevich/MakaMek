using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackEligibilityTests
{
    private static readonly HexCoordinates AttackerHex = new(5, 5);
    private readonly PhysicalAttackEligibility _sut = new();
    private readonly IPlayer _attackingPlayer = CreatePlayer();
    private readonly IPlayer _defendingPlayer = CreatePlayer();
    private readonly BattleMap _map = CreateFlatMap();

    [Fact]
    public void Punch_TargetInTheForwardArc_BothArms()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
    }

    [Theory]
    [InlineData(HexDirection.BottomLeft, PartLocation.LeftArm)]
    [InlineData(HexDirection.BottomRight, PartLocation.RightArm)]
    public void Punch_TargetInASideArc_OnlyTheMatchingArm(HexDirection side, PartLocation arm)
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(side));

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBe([arm]);
    }

    [Fact]
    public void Punch_TargetBehind_NoArms()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Bottom));

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBeEmpty();
    }

    [Fact]
    public void Punch_ArcsFollowTheTorsoTwist()
    {
        // Arrange: the target is in the right side arc of the feet, so untwisted only the right arm
        // reaches it. Turning the torso one hexside right puts it in the forward arc.
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.BottomRight));
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map).ShouldBe([PartLocation.RightArm]);
        attacker.RotateTorso(HexDirection.TopRight);

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
    }

    [Fact]
    public void Punch_ArmThatFiredAWeapon_CannotPunch()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        FireWeaponIn(attacker, PartLocation.RightArm);

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBe([PartLocation.LeftArm]);
    }

    [Fact]
    public void Punch_ArmWithADestroyedShoulder_CannotPunch()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        attacker.Parts[PartLocation.LeftArm].GetComponent<ShoulderActuator>()!.Hit();

        // Act
        var arms = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        arms.ShouldBe([PartLocation.RightArm]);
    }

    [Theory]
    [InlineData(HexDirection.TopLeft)]
    [InlineData(HexDirection.Top)]
    [InlineData(HexDirection.TopRight)]
    public void Kick_TargetInOneOfTheThreeForwardHexes_BothLegs(HexDirection direction)
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(direction));

        // Act
        var legs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map);

        // Assert
        legs.ShouldBe([PartLocation.LeftLeg, PartLocation.RightLeg], ignoreOrder: true);
    }

    [Fact]
    public void Kick_FollowsTheFeetNotTheTorso()
    {
        // Arrange: a twisted torso faces the target, the feet do not.
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.BottomRight));
        attacker.RotateTorso(HexDirection.TopRight);

        // Act
        var legs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map);

        // Assert
        legs.ShouldBeEmpty();
    }

    [Fact]
    public void Kick_LegThatFiredAWeapon_CannotKickButTheOtherCan()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        FireWeaponIn(attacker, PartLocation.RightLeg);

        // Act
        var legs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map);

        // Assert
        legs.ShouldBe([PartLocation.LeftLeg]);
    }

    [Fact]
    public void Kick_EitherHipDestroyed_NoKick()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        attacker.Parts[PartLocation.LeftLeg].GetComponent<HipActuator>()!.Hit();

        // Act
        var legs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map);

        // Assert
        legs.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    public void NoAttack_WhenAttackerHasAlreadyDeclared(PhysicalAttackType attackType)
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = target.Id
        });

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void NoAttack_WhenAttackerIsShutDown()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        attacker.Shutdown(new ShutdownData { Reason = ShutdownReason.Voluntary, Turn = 1 });

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void NoAttack_WhenAttackerIsProne()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        attacker.SetProne();

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void NoAttack_AgainstAUnitOfTheSamePlayer()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        target.Owner = _attackingPlayer;

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void NoAttack_AgainstATargetTwoHexesAway()
    {
        // Arrange
        var (attacker, target) = CreatePair(
            AttackerHex.GetNeighbour(HexDirection.Top).GetNeighbour(HexDirection.Top));

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void NoAttack_WithAPushNotYetSupported()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Push, _map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void SkiddingAttacker_CanStillAttack()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        var position = attacker.Position!;
        attacker.Move(new MovementPath(
        [
            new PathSegment(position, position,
                [new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 }],
                Events: [new SegmentEvent(SegmentEventType.Skid)])
        ], MovementType.Walk), null);
        attacker.IsSkidding.ShouldBeTrue();

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map);

        // Assert
        limbs.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch, 0, true)]
    [InlineData(PhysicalAttackType.Punch, 1, true)]
    [InlineData(PhysicalAttackType.Punch, -1, false)]
    [InlineData(PhysicalAttackType.Punch, 2, false)]
    [InlineData(PhysicalAttackType.Kick, 0, true)]
    [InlineData(PhysicalAttackType.Kick, -1, true)]
    [InlineData(PhysicalAttackType.Kick, 1, false)]
    [InlineData(PhysicalAttackType.Kick, -2, false)]
    public void Elevation_StandingTarget(PhysicalAttackType attackType, int targetIsHigherBy, bool canAttack)
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetLevel(AttackerHex, 2);
        SetLevel(targetHex, 2 + targetIsHigherBy);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        (limbs.Count > 0).ShouldBe(canAttack);
    }

    /// <summary>
    /// A prone Mech rises one level above its hex, so the table is shifted by one (issue #1504).
    /// </summary>
    [Theory]
    [InlineData(PhysicalAttackType.Punch, 1, false)]
    [InlineData(PhysicalAttackType.Punch, 0, true)]
    [InlineData(PhysicalAttackType.Punch, -1, false)]
    [InlineData(PhysicalAttackType.Punch, -2, false)]
    [InlineData(PhysicalAttackType.Kick, 1, false)]
    [InlineData(PhysicalAttackType.Kick, 0, false)]
    [InlineData(PhysicalAttackType.Kick, -1, true)]
    [InlineData(PhysicalAttackType.Kick, -2, false)]
    public void Elevation_ProneTarget(PhysicalAttackType attackType, int targetIsHigherBy, bool canAttack)
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        target.SetProne();
        SetLevel(AttackerHex, 2);
        SetLevel(targetHex, 2 + targetIsHigherBy);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        (limbs.Count > 0).ShouldBe(canAttack);
    }

    /// <summary>
    /// Water lowers the target's standing level by its depth (TW p.109): a Mech standing in
    /// Depth 1 water stands one level below its hex, so from land only a kick reaches it.
    /// The attacker is on land at level 0; no water rules apply to it (issue #1600).
    /// </summary>
    [Theory]
    [InlineData(0, 0, PhysicalAttackType.Punch, true)]
    [InlineData(0, 0, PhysicalAttackType.Kick, true)]
    [InlineData(1, 0, PhysicalAttackType.Punch, false)]
    [InlineData(1, 0, PhysicalAttackType.Kick, true)]
    [InlineData(1, 1, PhysicalAttackType.Punch, true)]
    [InlineData(1, 1, PhysicalAttackType.Kick, true)]
    public void Elevation_WaterLowersTheTargetStandingLevel(
        int targetWaterDepth, int targetLevelHigher, PhysicalAttackType attackType, bool canAttack)
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(targetHex, targetLevelHigher, targetWaterDepth);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        (limbs.Count > 0).ShouldBe(canAttack);
    }

    /// <summary>
    /// An attacker standing in water stands at the bottom of its hex (TW p.109): Depth 1 lowers
    /// it one level (torso and arms still above the surface), Depth 2+ two levels (fully
    /// submerged). No attack-type restriction applies against surface targets — the
    /// Different-Levels table decides (issue #1600).
    /// </summary>
    [Theory]
    [InlineData(1, 0, PhysicalAttackType.Punch, true)]  // attacker at -1 vs target at 0: +1 → punch
    [InlineData(1, 0, PhysicalAttackType.Kick, false)]
    [InlineData(2, 0, PhysicalAttackType.Punch, false)] // attacker at -2 vs target at 0: +2 → nothing
    [InlineData(2, 0, PhysicalAttackType.Kick, false)]
    [InlineData(2, 1, PhysicalAttackType.Punch, true)]  // attacker at -2 vs target at -1: +1 → punch
    [InlineData(2, 1, PhysicalAttackType.Kick, false)]
    [InlineData(1, 1, PhysicalAttackType.Punch, true)]  // attacker at -1 vs target at -1: 0 → both
    [InlineData(1, 1, PhysicalAttackType.Kick, true)]
    public void Elevation_UsesTheAttackerEffectiveStandingLevelInWater(
        int attackerWaterDepth, int targetLevelLowerBy, PhysicalAttackType attackType, bool canAttack)
    {
        // Arrange: the attacker stands at the bottom of its water hex, the target on land
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, attackerWaterDepth);
        SetLevel(targetHex, -targetLevelLowerBy);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        (limbs.Count > 0).ShouldBe(canAttack);
    }

    /// <summary>
    /// A kick is the only attack that occurs entirely underwater from Depth 1 water (TW p.151).
    /// </summary>
    [Fact]
    public void Water_AttackerInDepth1AgainstSubmergedTarget_KickOnly()
    {
        // Arrange: the target stands in Depth 2 water, so it is fully submerged
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 1);
        SetWaterHex(targetHex, 0, 2);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map).ShouldBeEmpty();
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map)
            .ShouldBe([PartLocation.LeftLeg, PartLocation.RightLeg], ignoreOrder: true);
    }

    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    public void Water_SubmergedTarget_CannotBeAttackedFromLand(PhysicalAttackType attackType)
    {
        // Arrange: the target stands in Depth 2 water, so it is fully submerged
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(targetHex, 0, 2);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        limbs.ShouldBeEmpty();
        _sut.HasAnyOption(attacker, [target], _map).ShouldBeFalse();
    }

    /// <summary>
    /// A prone Mech in Depth 1 water is fully submerged (its height of 1 equals the depth).
    /// </summary>
    [Theory]
    [InlineData(PhysicalAttackType.Punch)]
    [InlineData(PhysicalAttackType.Kick)]
    public void Water_ProneTargetSubmergedInDepth1_CannotBeAttackedFromLand(PhysicalAttackType attackType)
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        target.SetProne();
        SetWaterHex(targetHex, 0, 1);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, attackType, _map);

        // Assert
        limbs.ShouldBeEmpty();
        _sut.HasAnyOption(attacker, [target], _map).ShouldBeFalse();
    }

    /// <summary>
    /// Depth 1 water does not restrict the attacker: any physical attack against a surface target
    /// is allowed (TW p.151). The lowered standing level only shifts the elevation difference.
    /// </summary>
    [Fact]
    public void Water_AttackerInDepth1AgainstLandTarget_PunchOnly()
    {
        // Arrange: the attacker stands one level lower than its hex, so the target is effectively
        // one level higher: a punch reaches it, a kick does not
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 1);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map).ShouldBeEmpty();
    }

    [Fact]
    public void Water_BothStandingInDepth1_AnyAttack()
    {
        // Arrange: both units stand one level lower, so the difference stays 0
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 1);
        SetWaterHex(targetHex, 0, 1);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map)
            .ShouldBe([PartLocation.LeftLeg, PartLocation.RightLeg], ignoreOrder: true);
    }

    /// <summary>
    /// In Depth 2+ water the whole Mech is underwater, so no target-side water restriction applies:
    /// the Different-Levels table decides (TW p.151).
    /// </summary>
    [Fact]
    public void Water_SubmergedAttackerAgainstLandTargetOneLevelLower_PunchAllowed()
    {
        // Arrange: the attacker stands at -2, the target on land one hex level lower, so the
        // effective difference is +1
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 2);
        SetLevel(targetHex, -1);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map).ShouldBeEmpty();
    }

    [Fact]
    public void Water_SubmergedAttackerAgainstStandingDepth1Target_PunchAllowed()
    {
        // Arrange: the attacker stands at -2, the target (not submerged) at -1
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 2);
        SetWaterHex(targetHex, 0, 1);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map).ShouldBeEmpty();
    }

    [Fact]
    public void Water_BothSubmergedInDepth2_AnyAttack()
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        SetWaterHex(AttackerHex, 0, 2);
        SetWaterHex(targetHex, 0, 2);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map)
            .ShouldBe([PartLocation.LeftLeg, PartLocation.RightLeg], ignoreOrder: true);
    }

    /// <summary>
    /// A Mech on a bridge over water is not submerged and stands at the bridge level (issue #1600).
    /// </summary>
    [Fact]
    public void Water_TargetOnABridgeOverDeepWater_NotSubmerged_BridgeLevel()
    {
        // Arrange: the bridge deck stands at level 2 over a Depth 2 water hex of level 0
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var attacker = CreateMech(_attackingPlayer, AttackerHex, HexDirection.Top);
        var target = CreateMech(_defendingPlayer, targetHex, HexDirection.Bottom, HexSurface.Bridge);
        var bridgeHex = new Hex(targetHex);
        bridgeHex.AddTerrain(new WaterTerrain(-2));
        bridgeHex.AddTerrain(new BridgeTerrain(2, 10));
        _map.AddHex(bridgeHex);
        SetLevel(AttackerHex, 2);

        // Act + Assert
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, _map)
            .ShouldBe([PartLocation.LeftArm, PartLocation.RightArm], ignoreOrder: true);
        _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Kick, _map)
            .ShouldBe([PartLocation.LeftLeg, PartLocation.RightLeg], ignoreOrder: true);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoAttack_WhenAHexIsMissingFromTheMap(bool attackerHexOnMap)
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        var map = new BattleMap(10, 10);
        if (attackerHexOnMap) map.AddHex(new Hex(AttackerHex));

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, map);

        // Assert
        limbs.ShouldBeEmpty();
    }

    [Fact]
    public void HasAnyOption_TrueWhenAnyUnitCanBeAttacked()
    {
        // Arrange
        var (attacker, adjacent) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        var farAway = CreateMech(_defendingPlayer, new HexCoordinates(1, 1), HexDirection.Top);

        // Act + Assert
        _sut.HasAnyOption(attacker, [farAway, adjacent], _map).ShouldBeTrue();
        _sut.HasAnyOption(attacker, [farAway], _map).ShouldBeFalse();
    }

    [Fact]
    public void CanResolve_DeclaredAttackThatStillReaches_IsTrue()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        DeclarePunch(attacker, target);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeTrue();
    }

    [Fact]
    public void CanResolve_LimbThatDoesNotReach_IsFalse()
    {
        // Arrange: a target in the right side arc can only be punched with the right arm
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.BottomRight));
        DeclarePunch(attacker, target);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeFalse();
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.RightArm, _map).ShouldBeTrue();
    }

    [Fact]
    public void CanResolve_AttackerThatBecameProne_IsFalse()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        DeclarePunch(attacker, target);
        attacker.SetProne();

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeFalse();
    }

    [Fact]
    public void CanResolve_TargetNoLongerAdjacent_IsFalse()
    {
        // Arrange
        var (attacker, target) = CreatePair(new HexCoordinates(5, 3));
        DeclarePunch(attacker, target);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeFalse();
    }

    [Fact]
    public void CanResolve_TargetBecomesSubmerged_IsFalse()
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        DeclarePunch(attacker, target);

        // the target's hex floods with deep water, so it becomes fully submerged
        SetWaterHex(targetHex, 0, 2);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeFalse();
    }

    [Fact]
    public void CanResolve_TargetBecomesProneInDepth1Water_IsFalse()
    {
        // Arrange: prone in Depth 1 water the target is fully submerged
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        DeclarePunch(attacker, target);
        target.SetProne();
        SetWaterHex(targetHex, 0, 1);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Punch, PartLocation.LeftArm, _map).ShouldBeFalse();
    }

    [Fact]
    public void CanResolve_TargetStandsInShallowDepth1WaterNotSubmerged_KickStillTrue()
    {
        // Arrange
        var targetHex = AttackerHex.GetNeighbour(HexDirection.Top);
        var (attacker, target) = CreatePair(targetHex);
        attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Kick,
            AttackerLimbs = [PartLocation.RightLeg],
            TargetId = target.Id
        });

        // the target stands in Depth 1 water: one level lower, the kick still reaches it
        SetWaterHex(targetHex, 0, 1);

        // Act + Assert
        _sut.CanResolve(attacker, target, PhysicalAttackType.Kick, PartLocation.RightLeg, _map).ShouldBeTrue();
    }

    private static void DeclarePunch(Mech attacker, Mech target) =>
        attacker.DeclarePhysicalAttack(new PhysicalAttackDeclarationData
        {
            AttackType = PhysicalAttackType.Punch,
            AttackerLimbs = [PartLocation.LeftArm],
            TargetId = target.Id
        });

    private (Mech Attacker, Mech Target) CreatePair(HexCoordinates targetHex) =>
        (CreateMech(_attackingPlayer, AttackerHex, HexDirection.Top),
            CreateMech(_defendingPlayer, targetHex, HexDirection.Bottom));

    private static Mech CreateMech(IPlayer owner, HexCoordinates hex, HexDirection facing, HexSurface surface = HexSurface.Ground)
    {
        PartLocation[] limbs = [PartLocation.LeftArm, PartLocation.RightArm, PartLocation.LeftLeg, PartLocation.RightLeg];
        var lasers = limbs.Select(limb => new ComponentData
        {
            Type = MakaMekComponent.MediumLaser,
            Assignments = [new LocationSlotAssignment(limb, 4, 1)]
        }).ToList();
        var mech = new MechFactory(
                new TotalWarfareRulesProvider(),
                new ClassicBattletechComponentProvider(),
                Substitute.For<ILocalizationService>())
            .Create(MechFactoryTests.CreateDummyMechData(lasers));
        mech.Owner = owner;
        mech.Deploy(new HexPosition(hex, facing, surface), null);
        return mech;
    }

    private void SetLevel(HexCoordinates coordinates, int level)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new ClearTerrain());
        _map.AddHex(hex);
    }

    private void SetWaterHex(HexCoordinates coordinates, int level, int depth)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new WaterTerrain(-depth));
        _map.AddHex(hex);
    }

    private static BattleMap CreateFlatMap()
    {
        var map = new BattleMap(10, 10);
        for (var q = 1; q <= 10; q++)
        for (var r = 1; r <= 10; r++)
        {
            var hex = new Hex(new HexCoordinates(q, r));
            hex.AddTerrain(new ClearTerrain());
            map.AddHex(hex);
        }
        return map;
    }

    private static void FireWeaponIn(Mech mech, PartLocation location) =>
        mech.Parts[location].GetComponents<Weapon>().First().MarkAsFired();

    private static IPlayer CreatePlayer()
    {
        var player = Substitute.For<IPlayer>();
        player.Id.Returns(Guid.NewGuid());
        return player;
    }
}
