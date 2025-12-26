using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game;

public record struct PhaseStepState(IPlayer ActivePlayer, int UnitsToPlay);