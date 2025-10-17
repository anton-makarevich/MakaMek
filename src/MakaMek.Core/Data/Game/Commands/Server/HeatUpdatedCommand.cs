using System.Text;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct HeatUpdatedCommand : IGameCommand
{
    public required Guid UnitId { get; init; }
    public required HeatData HeatData { get; init; }
    public required int PreviousHeat { get; init; }
    
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);
            
        if (unit == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();
        
        // Unit name and previous heat
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_HeatUpdated_Header"),
            unit.Model,
            PreviousHeat).AppendLine();
            
        // Heat sources
        stringBuilder.AppendLine(localizationService.GetString("Command_HeatUpdated_Sources"));
        
        // Movement heat sources
        foreach (var source in HeatData.MovementHeatSources)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_HeatUpdated_MovementHeat"),
                source.MovementType,
                source.MovementPointsSpent,
                source.HeatPoints).AppendLine();
        }
        
        // Weapon heat sources
        foreach (var source in HeatData.WeaponHeatSources)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_HeatUpdated_WeaponHeat"),
                source.WeaponName,
                source.HeatPoints).AppendLine();
        }

        // External heat sources
        foreach (var source in HeatData.ExternalHeatSources)
        {
            stringBuilder.AppendFormat(
                localizationService.GetString("Command_HeatUpdated_ExternalHeat"),
                source.WeaponName,
                source.HeatPoints).AppendLine();
            if (HeatData.ExternalHeatPoints < HeatData.ExternalHeatSources.Sum(s => s.HeatPoints))
            {
                var lostHeat = HeatData.ExternalHeatSources.Sum(s => s.HeatPoints) - HeatData.ExternalHeatPoints;
                stringBuilder.AppendFormat(localizationService.GetString("Command_HeatUpdated_ExternalHeat_Lost"), lostHeat)
                    .AppendLine();
                break;
            }
        }

        // Total heat generated
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_HeatUpdated_TotalGenerated"),
            HeatData.TotalHeatPoints).AppendLine();
            
        // Heat dissipation
        stringBuilder.AppendFormat(
            localizationService.GetString("Command_HeatUpdated_Dissipation"),
            HeatData.DissipationData.HeatSinks,
            HeatData.DissipationData.EngineHeatSinks,
            HeatData.DissipationData.DissipationPoints).AppendLine();
            
        return stringBuilder.ToString().TrimEnd();
    }
}
