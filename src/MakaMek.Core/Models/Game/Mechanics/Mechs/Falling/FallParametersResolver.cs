using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// The geometric inputs of a fall: where the unit lands, how far it drops and whether
/// the fall interrupts a jump.
/// </summary>
public readonly record struct FallParameters(int WaterDepth, int LevelsFallen, bool WasJumping);

/// <summary>
/// Resolves the fall geometry (water depth, levels fallen, jumping flag) for a single
/// piloting-skill-roll context. Extracted from <see cref="FallProcessor"/> so the
/// processor only orchestrates the roll loop.
/// </summary>
public sealed class FallParametersResolver
{
    /// <summary>
    /// How far the unit falls and whether the fall is reported as happening mid-jump. Each roll
    /// context carries its own drop; only a failed jump landing falls back to the water depth, since
    /// the unit drops from the surface to the hex bottom.
    /// </summary>
    public FallParameters Resolve(
        Mech mech,
        IGame game,
        PilotingSkillRollContext context,
        MovementType movementType,
        bool isFalling,
        HexPosition? fallPosition)
    {
        // Water depth of the hex where the fall happens (0 when the hex is dry or the surface is not Ground).
        // For water entry the roll context carries the authoritative depth (it is built from the actual hex).
        var waterDepth = context is EnteringDeepWaterRollContext deepWaterCtx
            ? deepWaterCtx.WaterDepth
            : ResolveWaterDepth(mech, game, fallPosition);

        var wasJumping = movementType == MovementType.Jump;
        if (!isFalling) return new FallParameters(waterDepth, 0, wasJumping);

        return context switch
        {
            // WaterTerrain.Height stores depth as negative integers (0 = shallow, -1 = depth 1, -2 = depth 2).
            // EnteringDeepWaterRollContext.WaterDepth is already the positive depth value (-1 * water.Height).
            // A fall that ends in a water hex drops the unit to the hex bottom, so for a water-entry
            // fall levelsFallen equals the water depth, regardless of the movement type
            // (walk, run or jump all fall the same levels).
            EnteringDeepWaterRollContext waterCtx => new FallParameters(waterDepth, waterCtx.WaterDepth, wasJumping),
            // A collapsing bridge drops the unit from the bridge deck down to the hex bottom
            BridgeCollapseRollContext bridgeCtx => new FallParameters(waterDepth, bridgeCtx.BridgeHeight + waterDepth, wasJumping),
            // A skid is a fall in place on the same level.
            SkidCheckRollContext => new FallParameters(waterDepth, 0, false),
            CliffFallRollContext cliffCtx => new FallParameters(waterDepth, cliffCtx.LevelsFallen, false),
            // The DFA attacker is jumping, so WasJumping stays true and the fall is reported as
            // happening mid-jump. The jump-into-water fallback below is skipped because a DFA fall
            // is a fixed level drop, not a landing at the hex bottom.
            DfaFallRollContext dfaCtx => new FallParameters(waterDepth, dfaCtx.LevelsFallen, wasJumping),
            // A failed jump landing in a water hex drops the unit from the surface to the hex bottom
            _ when movementType == MovementType.Jump && waterDepth > 0 => new FallParameters(waterDepth, waterDepth, wasJumping),
            _ => new FallParameters(waterDepth, 0, wasJumping)
        };
    }

    /// <summary>
    /// Resolves the water depth of the hex where the fall happens.
    /// Uses the explicit landing position when supplied (movement interrupts calculate the fall
    /// before the unit moves), otherwise the mech's current position.
    /// Depth only counts on a Ground surface: a unit on a bridge falls onto the deck, not into the water.
    /// Returns 0 when the hex cannot be resolved or has no water.
    /// </summary>
    private static int ResolveWaterDepth(Mech mech, IGame game, HexPosition? fallPosition)
    {
        var position = fallPosition ?? mech.Position;
        if (position == null) return 0;
        if (position.Surface != HexSurface.Ground) return 0;

        var hex = game.BattleMap?.GetHex(position.Coordinates);
        return hex?.GetWaterDepth() ?? 0;
    }
}
