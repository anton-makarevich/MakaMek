namespace Sanet.MakaMek.Core.Utils;

/// <summary>
/// Utility class for dice-related calculations
/// </summary>
public static class DiceUtils
{
    /// <summary>
    /// The impossible roll value
    /// </summary>
    public const int Impossible2D6Roll = 13;
    
    public const int Guaranteed2D6Roll = 2;
    
    /// <summary>
    /// Calculates the probability of rolling at least the target number on 2d6
    /// </summary>
    /// <param name="targetNumber">Target number to roll (2-12)</param>
    /// <returns>Probability as a percentage (0-100)</returns>
    public static double Calculate2d6Probability(int targetNumber)
    {
        // For 2d6 roll, calculate probability of rolling >= targetNumber
        // Classic BattleTech uses 2d6 where rolling >= targetNumber means success
        
        // If target number is impossible or guaranteed
        if (targetNumber > 12) return 0.0;
        if (targetNumber <= 2) return 100.0;
        
        // Probabilities for 2d6 roll:
        // 2: 1/36 (2.78%)
        // 3: 2/36 (5.56%)
        // 4: 3/36 (8.33%)
        // 5: 4/36 (11.11%)
        // 6: 5/36 (13.89%)
        // 7: 6/36 (16.67%)
        // 8: 5/36 (13.89%)
        // 9: 4/36 (11.11%)
        // 10: 3/36 (8.33%)
        // 11: 2/36 (5.56%)
        // 12: 1/36 (2.78%)
        
        // Calculate success probability (probability of rolling >= targetNumber)
        Dictionary<int, double> successProbabilities = new Dictionary<int, double>
        {
            { 2, 100.0 },
            { 3, 97.22 },
            { 4, 91.67 },
            { 5, 83.33 },
            { 6, 72.22 },
            { 7, 58.33 },
            { 8, 41.67 },
            { 9, 27.78 },
            { 10, 16.67 },
            { 11, 8.33 },
            { 12, 2.78 }
        };
        
        return successProbabilities.GetValueOrDefault(targetNumber, 0.0);
    }
}
