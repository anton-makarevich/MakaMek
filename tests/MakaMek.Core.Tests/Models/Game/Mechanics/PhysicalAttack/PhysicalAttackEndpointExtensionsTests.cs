using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.PhysicalAttack;

public class PhysicalAttackEndpointExtensionsTests
{
    private static readonly HexCoordinates TestHex = new(3, 3);
    private const int StandingHeight = 2;
    private const int ProneHeight = 1;

    [Fact]
    public void GetPhysicalAttackEndpoint_ClearGround_ReturnsGroundFacts()
    {
        // Arrange
        var map = new BattleMap(10, 10);
        map.AddHex(ClearHex(TestHex));

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(Position(TestHex), StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(0);
        endpoint.Value.IsSubmerged.ShouldBeFalse();
        endpoint.Value.IsInWater.ShouldBeFalse();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_DepthOneWater_StandingMechIsNotSubmergedButInWater()
    {
        // Arrange
        var map = new BattleMap(10, 10);
        map.AddHex(WaterHex(TestHex, 0, 1));

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(Position(TestHex), StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(-1);
        endpoint.Value.IsSubmerged.ShouldBeFalse();
        endpoint.Value.IsInWater.ShouldBeTrue();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_DepthTwoWater_StandingMechIsSubmerged()
    {
        // Arrange
        var map = new BattleMap(10, 10);
        map.AddHex(WaterHex(TestHex, 0, 2));

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(Position(TestHex), StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(-2);
        endpoint.Value.IsSubmerged.ShouldBeTrue();
        endpoint.Value.IsInWater.ShouldBeTrue();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_ProneMechInDepthOneWater_IsSubmerged()
    {
        // Arrange: a prone Mech is one level high, so Depth 1 water covers it completely
        var map = new BattleMap(10, 10);
        map.AddHex(WaterHex(TestHex, 0, 1));

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(Position(TestHex), ProneHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(-1);
        endpoint.Value.IsSubmerged.ShouldBeTrue();
        endpoint.Value.IsInWater.ShouldBeTrue();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_BridgeOverDeepWater_UsesBridgeDeckAndIsNotInWater()
    {
        // Arrange: a bridge deck at level 2 over a Depth 2 water hex of level 0
        var map = new BattleMap(10, 10);
        var hex = WaterHex(TestHex, 0, 2);
        hex.AddTerrain(new BridgeTerrain(2, 10));
        map.AddHex(hex);
        var position = Position(TestHex, HexSurface.Bridge);

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(position, StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(2);
        endpoint.Value.IsSubmerged.ShouldBeFalse();
        endpoint.Value.IsInWater.ShouldBeFalse();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_MissingHex_ReturnsNull()
    {
        // Arrange
        var map = new BattleMap(10, 10);

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(Position(TestHex), StandingHeight);

        // Assert
        endpoint.ShouldBeNull();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_ReplacedHex_ReflectsTheNewMapHex()
    {
        // Arrange: a position recorded while the hex was clear is resolved again after the map hex
        // is replaced by deep water, proving the lookup is against the map rather than a stale copy
        var map = new BattleMap(10, 10);
        map.AddHex(ClearHex(TestHex));
        var position = Position(TestHex);
        map.GetPhysicalAttackEndpoint(position, StandingHeight)!.Value.IsInWater.ShouldBeFalse();

        map.AddHex(WaterHex(TestHex, 0, 2));

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(position, StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(-2);
        endpoint.Value.IsSubmerged.ShouldBeTrue();
        endpoint.Value.IsInWater.ShouldBeTrue();
    }

    [Fact]
    public void LevelDifferenceTo_ReturnsTargetMinusThisEndpoint()
    {
        // Arrange
        var deeper = new PhysicalAttackEndpoint(-2, true, true);
        var shallower = new PhysicalAttackEndpoint(-1, true, true);
        var higher = new PhysicalAttackEndpoint(2, false, false);

        // Act + Assert
        deeper.LevelDifferenceTo(shallower).ShouldBe(1);
        shallower.LevelDifferenceTo(deeper).ShouldBe(-1);
        deeper.LevelDifferenceTo(deeper).ShouldBe(0);
        higher.LevelDifferenceTo(deeper).ShouldBe(-4);
    }

    /// <summary>
    /// The per-attack water line (Total Warfare p. 144 "Water" and p. 149-151). A submerged endpoint
    /// is always in water; a Depth 1 attacker is in water but not submerged.
    /// </summary>
    [Theory]
    // A Charge keeps both endpoints on the same side of the water line.
    [InlineData(false, false, false, PhysicalAttackType.Charge, true)]
    [InlineData(true, true, true, PhysicalAttackType.Charge, true)]
    [InlineData(false, false, true, PhysicalAttackType.Charge, false)]
    [InlineData(true, true, false, PhysicalAttackType.Charge, false)]
    // A DFA comes from above the surface, so neither endpoint may be submerged.
    [InlineData(false, false, false, PhysicalAttackType.DFA, true)]
    [InlineData(true, true, false, PhysicalAttackType.DFA, false)]
    [InlineData(false, false, true, PhysicalAttackType.DFA, false)]
    // A Kick occurs entirely underwater, so a Depth 1 attacker still reaches a submerged target.
    [InlineData(true, false, true, PhysicalAttackType.Kick, true)]
    [InlineData(false, false, true, PhysicalAttackType.Kick, false)]
    [InlineData(false, false, false, PhysicalAttackType.Kick, true)]
    // Punch and Push use the arms, so only a submerged attacker reaches a submerged target.
    [InlineData(true, false, true, PhysicalAttackType.Punch, false)]
    [InlineData(true, true, true, PhysicalAttackType.Punch, true)]
    [InlineData(true, true, false, PhysicalAttackType.Punch, true)]
    [InlineData(true, false, true, PhysicalAttackType.Push, false)]
    [InlineData(true, true, true, PhysicalAttackType.Push, true)]
    [InlineData(true, true, false, PhysicalAttackType.Push, true)]
    public void IsWaterEligibleAgainst_AppliesThePerAttackWaterLine(bool attackerInWater,
        bool attackerSubmerged, bool targetSubmerged, PhysicalAttackType attackType, bool expected)
    {
        // Arrange
        var attacker = Endpoint(attackerInWater, attackerSubmerged);
        var target = Endpoint(targetSubmerged, targetSubmerged);

        // Act + Assert
        attacker.IsWaterEligibleAgainst(target, attackType).ShouldBe(expected);
    }

    /// <summary>
    /// An attack type without a water rule is never water-eligible.
    /// </summary>
    [Fact]
    public void IsWaterEligibleAgainst_ForAnUnknownAttackType_IsFalse()
    {
        // Arrange
        var dry = Endpoint(inWater: false, submerged: false);

        // Act + Assert
        dry.IsWaterEligibleAgainst(dry, (PhysicalAttackType)99).ShouldBeFalse();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_AirbornePosition_UsesTheAirborneBaseLevel()
    {
        // Arrange: a pending DFA leaves the Mech airborne above its approach hex.
        var map = new BattleMap(10, 10);
        map.AddHex(ClearHex(TestHex));
        var position = new HexPosition(TestHex, HexDirection.Top, AirborneLevel: 2);

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(position, StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(2);
        endpoint.Value.IsSubmerged.ShouldBeFalse();
        endpoint.Value.IsInWater.ShouldBeFalse();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_AirbornePositionOverDeepWater_IsNotSubmergedOrInWater()
    {
        // Arrange: the airborne base level replaces the surface standing level entirely.
        var map = new BattleMap(10, 10);
        map.AddHex(WaterHex(TestHex, 0, 2));
        var position = new HexPosition(TestHex, HexDirection.Top, AirborneLevel: 3);

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(position, StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(3);
        endpoint.Value.IsSubmerged.ShouldBeFalse();
        endpoint.Value.IsInWater.ShouldBeFalse();
    }

    [Fact]
    public void GetPhysicalAttackEndpoint_AirbornePositionAboveARaisedHex_KeepsTheAbsoluteLevel()
    {
        // Arrange: the hex stands at level 1, the airborne base at level 3.
        var map = new BattleMap(10, 10);
        map.AddHex(ClearHex(TestHex, 1));
        var position = new HexPosition(TestHex, HexDirection.Top, AirborneLevel: 3);

        // Act
        var endpoint = map.GetPhysicalAttackEndpoint(position, StandingHeight);

        // Assert
        endpoint.ShouldNotBeNull();
        endpoint.Value.EffectiveStandingLevel.ShouldBe(3);
    }

    private static HexPosition Position(HexCoordinates coordinates,
        HexSurface surface = HexSurface.Ground) =>
        new(coordinates, HexDirection.Top, surface);

    private static PhysicalAttackEndpoint Endpoint(bool inWater, bool submerged) =>
        new(submerged ? -2 : inWater ? -1 : 0, submerged, inWater);

    private static Hex ClearHex(HexCoordinates coordinates, int level = 0)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new ClearTerrain());
        return hex;
    }

    private static Hex WaterHex(HexCoordinates coordinates, int level, int depth)
    {
        var hex = new Hex(coordinates, level);
        hex.AddTerrain(new WaterTerrain(-depth));
        return hex;
    }
}
