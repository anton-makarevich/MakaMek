namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public interface IClientUnitCommand : IClientCommand
{
    /// <summary>
    /// The ID of the unit this command operates on.
    /// </summary>
    Guid UnitId { get; init; }
}
