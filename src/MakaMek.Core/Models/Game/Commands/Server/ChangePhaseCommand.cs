﻿using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Commands.Server;

public record struct ChangePhaseCommand : IGameCommand
{
    public required PhaseNames Phase { get; init; }
    public Guid GameOriginId { get; set; }
    public DateTime Timestamp { get; set; }

    public string Format(ILocalizationService localizationService, IGame game)
    {
        var localizedTemplate = localizationService.GetString("Command_ChangePhase"); 
        
        return string.Format(localizedTemplate, Phase);
    }
}