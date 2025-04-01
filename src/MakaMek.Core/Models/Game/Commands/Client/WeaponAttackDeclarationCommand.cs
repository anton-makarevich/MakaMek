using Sanet.MakaMek.Core.Services.Localization;
using System.Text;
using Sanet.MakaMek.Core.Data.Game;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Client;

public record struct WeaponAttackDeclarationCommand : IClientCommand
{
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var command = this;
        var player = game.Players.FirstOrDefault(p => p.Id == command.PlayerId);
        var attacker = player?.Units.FirstOrDefault(u => u.Id == command.AttackerId);
        
        if (attacker == null || player == null) return string.Empty;
        
        if (WeaponTargets.Count == 0)
        {
            var noAttacksTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_NoAttacks");
            return string.Format(noAttacksTemplate, player.Name, attacker.Name);
        }
        
        var stringBuilder = new StringBuilder();
        var headerTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_Header");
        stringBuilder.AppendLine(string.Format(headerTemplate, player.Name, attacker.Name));
        
        var weaponLineTemplate = localizationService.GetString("Command_WeaponAttackDeclaration_WeaponLine");
        
        foreach (var weaponTarget in WeaponTargets)
        {
            var targetUnit = game.Players
                .SelectMany(p => p.Units)
                .FirstOrDefault(u => u.Id == weaponTarget.TargetId);

            var targetPlayer = targetUnit?.Owner;
            if (targetPlayer == null) continue;
            
            stringBuilder.AppendLine(string.Format(weaponLineTemplate, 
                weaponTarget.Weapon.Name, 
                targetPlayer.Name, 
                targetUnit!.Name));
        }
        
        return stringBuilder.ToString().TrimEnd();
    }

    public required Guid AttackerId { get; init; }
    public required List<WeaponTargetData> WeaponTargets { get; init; }
    public Guid PlayerId { get; init; }
}
