namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Rich result of a <see cref="IBattleMap.GetLineOfSight"/> calculation.
/// Carries the boolean outcome plus contextual data about how and where the result was determined.
/// </summary>
public record LineOfSightResult
{
    /// <summary>Source hex coordinates for this calculation.</summary>
    public required HexCoordinates From { get; init; }

    /// <summary>Target hex coordinates for this calculation.</summary>
    public required HexCoordinates To { get; init; }

    /// <summary>Height of the attacking unit (in levels, added to attacker hex level).</summary>
    public required int AttackerLosLevel { get; init; }

    /// <summary>Height of the target unit (in levels, added to target hex level).</summary>
    public required int TargetLosLevel { get; init; }

    /// <summary>True when there is a clear line of sight from <see cref="From"/> to <see cref="To"/>.</summary>
    public required bool HasLineOfSight { get; init; }

    /// <summary>
    /// Intervening hexes (excluding attacker and target) along the resolved LOS path, in order.
    /// Contains all hexes traversed up to and including any blocking hex.
    /// </summary>
    public required IReadOnlyList<LineOfSightHexInfo> HexPath { get; init; }

    /// <summary>
    /// Coordinates of the first hex that blocked the LOS; null when unblocked.
    /// </summary>
    public HexCoordinates? BlockingHexCoordinates { get; init; }

    /// <summary>
    /// The reason LOS was blocked; null when unblocked.
    /// </summary>
    public LineOfSightBlockReason? BlockReason { get; init; }

    /// <summary>
    /// Total accumulated intervening terrain factor across all hexes on <see cref="HexPath"/>.
    /// </summary>
    public required int TotalInterveningFactor { get; init; }

    // -------------------------------------------------------------------------
    // Static factory helpers — intended for mocks and simple early-exit cases.
    // -------------------------------------------------------------------------
    /// <summary>
    /// Creates a minimal unblocked result. Useful for NSubstitute mock setups and early-exit
    /// cases where full context is not needed.
    /// </summary>
    public static LineOfSightResult Unblocked(
        HexCoordinates from,
        HexCoordinates to,
        int attackerLosLevel = 0,
        int targetLosLevel = 0,
        IReadOnlyList<LineOfSightHexInfo>? hexPath = null) => new()
    {
        From = from ,
        To = to,
        AttackerLosLevel = attackerLosLevel,
        TargetLosLevel = targetLosLevel,
        HasLineOfSight = true,
        HexPath = hexPath ?? [],
        TotalInterveningFactor = hexPath?.Sum(h => h.InterveningFactor) ?? 0
    };

    /// <summary>
    /// Creates a minimal blocked result. Useful for NSubstitute mock setups and early-exit
    /// cases where full context is not needed.
    /// </summary>
    public static LineOfSightResult Blocked(
        HexCoordinates from,
        HexCoordinates to,
        HexCoordinates blockingHex,
        LineOfSightBlockReason reason = LineOfSightBlockReason.InvalidCoordinates,
        int attackerLosLevel = 0,
        int targetLosLevel = 0,
        IReadOnlyList<LineOfSightHexInfo>? hexPath = null) => new()
    {
        From = from,
        To = to,
        AttackerLosLevel = attackerLosLevel,
        TargetLosLevel = targetLosLevel,
        HasLineOfSight = false,
        HexPath = hexPath ?? [],
        BlockingHexCoordinates = blockingHex,
        BlockReason = reason,
        TotalInterveningFactor = hexPath?.Sum(h => h.InterveningFactor) ?? 0
    };
}

