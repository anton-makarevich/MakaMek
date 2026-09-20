using Sanet.MakaMek.Core.Models.Game;
using System.Text;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent when a mech experiences heat-triggered ammo explosion
/// </summary>
public record struct AmmoExplosionCommand : IGameCommand
{
    /// <summary>
    /// Gets or sets the identifier of the game that originated the command.
    /// </summary>
    public required Guid GameOriginId { get; set; }

    /// <summary>
    /// Gets or sets the time at which the command was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The ID of the unit that experienced the ammo explosion
    /// </summary>
    public required Guid UnitId { get; init; }

    /// <summary>
    /// The roll data for the ammo explosion avoidance attempt
    /// </summary>
    public required AvoidAmmoExplosionRollData AvoidExplosionRoll { get; init; }

    /// <summary>
    /// Critical hits resolution data for the explosion
    /// </summary>
    public required List<LocationCriticalHitsData> CriticalHits { get; init; }

    /// <summary>
    /// Gets the locations newly destroyed while applying the explosion, or <see langword="null"/> when no location was destroyed.
    /// </summary>
    public List<PartLocation>? DestroyedParts { get; init; }

    /// <summary>
    /// Gets a value indicating whether the explosion destroyed the unit.
    /// </summary>
    public bool UnitDestroyed { get; init; }

    /// <summary>
    /// Renders the heat check, explosion critical hits, and any destruction caused by the explosion for the game log.
    /// </summary>
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var unitId = UnitId; // Copy to a local variable to avoid struct access issues
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == unitId);

        if (unit == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        // Check if an explosion occurred
        var explosionOccurred = !AvoidExplosionRoll.IsSuccessful;

        var rollTotal = AvoidExplosionRoll.DiceResults.Sum();

        if (!explosionOccurred)
        {
            // Explosion avoided
            var successTemplate = localizationService.GetString("Command_AmmoExplosion_Avoided");
            stringBuilder.AppendFormat(successTemplate, unit.Model).AppendLine();

            // Add roll details
        }
        else
        {
            // Explosion occurred due to a failed roll
            var failureTemplate = localizationService.GetString("Command_AmmoExplosion_Failed");
            stringBuilder.AppendFormat(failureTemplate, unit.Model).AppendLine();

            // Add roll details
        }

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_AmmoExplosion_RollDetails"),
            AvoidExplosionRoll.HeatLevel,
            rollTotal,
            AvoidExplosionRoll.AvoidNumber).AppendLine();
        
        // If an explosion occurred, show the critical hits details
        if (!explosionOccurred) return stringBuilder.ToString().TrimEnd();

        stringBuilder.AppendLine(localizationService.GetString("Command_AmmoExplosion_CriticalHits"));

        foreach (var criticalHitData in CriticalHits)
        {
            stringBuilder.Append(criticalHitData.Render(localizationService, unit));
        }

        if (DestroyedParts is { Count: > 0 })
        {
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_DestroyedParts"));
            foreach (var location in DestroyedParts)
            {
                stringBuilder.AppendFormat(
                    localizationService.GetString("Command_WeaponAttackResolution_DestroyedPart"),
                    localizationService.GetString($"MechPart_{location}")).AppendLine();
            }
        }

        if (UnitDestroyed)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_WeaponAttackResolution_UnitDestroyed"),
                unit.Model).AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}
