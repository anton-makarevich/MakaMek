﻿using Sanet.MekForge.Core.Data;

namespace Sanet.MekForge.Core.Models.Game.Commands;

public record MoveUnitCommand(
    Guid UnitId,
    HexCoordinateData Destination) : GameCommand;