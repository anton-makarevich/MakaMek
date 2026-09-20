using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

/// <summary>
/// Command sent from server to clients to apply critical hits resolution data
/// </summary>
public record CriticalHitsResolutionCommand : IGameCommand
{
    /// <summary>
    /// Gets or sets the identifier of the game that originated the command.
    /// </summary>
    public required Guid GameOriginId { get; set; }

    /// <summary>
    /// Gets the identifier of the unit that received the critical hits.
    /// </summary>
    public required Guid TargetId { get; init; }

    /// <summary>
    /// Gets the critical-hit results that were applied to the target.
    /// </summary>
    public required List<LocationCriticalHitsData> CriticalHits { get; init; }

    /// <summary>
    /// Gets the locations newly destroyed while applying these critical hits, or <see langword="null"/> when no location was destroyed.
    /// </summary>
    public List<PartLocation>? DestroyedParts { get; init; }

    /// <summary>
    /// Gets a value indicating whether these critical hits destroyed the target unit.
    /// </summary>
    public bool UnitDestroyed { get; init; }

    /// <summary>
    /// Gets or sets the time at which the command was created.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Renders the critical-hit results and any destruction caused by them for the game log.
    /// </summary>
    public string Render(ILocalizationService localizationService, IGame game)
    {
        var target = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == TargetId);

        if (target == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        // Add a header message
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_CriticalHitsResolution_Header"),
            target.Model).AppendLine();

        foreach (var criticalHitData in CriticalHits)
        {
            // Use the centralized rendering method
            stringBuilder.Append(criticalHitData.Render(localizationService, target));
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
                target.Model).AppendLine();
        }

        return stringBuilder.ToString();
    }
}
