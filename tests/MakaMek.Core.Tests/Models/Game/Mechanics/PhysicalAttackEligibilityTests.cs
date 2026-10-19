using NSubstitute;
using Sanet.Localization;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
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

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

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

    [Theory]
    [InlineData(PhysicalAttackType.Punch, 0, true)]
    [InlineData(PhysicalAttackType.Punch, 1, true)]
    [InlineData(PhysicalAttackType.Punch, -1, false)]
    [InlineData(PhysicalAttackType.Kick, 0, true)]
    [InlineData(PhysicalAttackType.Kick, -1, false)]
    [InlineData(PhysicalAttackType.Kick, 1, false)]
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

    [Fact]
    public void NoAttack_WhenAHexIsMissingFromTheMap()
    {
        // Arrange
        var (attacker, target) = CreatePair(AttackerHex.GetNeighbour(HexDirection.Top));
        var emptyMap = new BattleMap(10, 10);

        // Act
        var limbs = _sut.GetEligibleLimbs(attacker, target, PhysicalAttackType.Punch, emptyMap);

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

    private (Mech Attacker, Mech Target) CreatePair(HexCoordinates targetHex) =>
        (CreateMech(_attackingPlayer, AttackerHex, HexDirection.Top),
            CreateMech(_defendingPlayer, targetHex, HexDirection.Bottom));

    private static Mech CreateMech(IPlayer owner, HexCoordinates hex, HexDirection facing)
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
        mech.Deploy(new HexPosition(hex, facing), null);
        return mech;
    }

    private void SetLevel(HexCoordinates coordinates, int level)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new ClearTerrain());
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
