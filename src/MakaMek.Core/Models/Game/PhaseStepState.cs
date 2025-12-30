using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.Models.Game;

public record struct PhaseStepState(PhaseNames Phase, IPlayer ActivePlayer, int UnitsToPlay);