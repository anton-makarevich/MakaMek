using System.Text;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Commands.Server;

public record struct MechSkidCommand : IGameCommand
{
    public required Guid UnitId { get; init; }
    public required int SkidDistance { get; init; }
    public required FallingDamageData? DamageData { get; init; }
    public required Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Render(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var unit = game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);

        if (unit == null || DamageData == null)
        {
            return string.Empty;
        }

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_MechSkid_Base"),
            unit.Model,
            command.SkidDistance);

        stringBuilder.AppendFormat(
            localizationService.GetString("Command_MechFalling_Damage"),
            DamageData.HitLocations.TotalDamage);

        if (DamageData.HitLocations.HitLocations.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(localizationService.GetString("Command_WeaponAttackResolution_HitLocations"));

            foreach (var hitLocation in DamageData.HitLocations.HitLocations)
            {
                stringBuilder.Append(hitLocation.Render(localizationService));
            }
        }

        return stringBuilder.ToString().TrimEnd();
    }
}
