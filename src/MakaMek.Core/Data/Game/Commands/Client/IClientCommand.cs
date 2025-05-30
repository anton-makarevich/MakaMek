namespace Sanet.MakaMek.Core.Data.Game.Commands.Client;

public interface IClientCommand: IGameCommand
{
    Guid PlayerId { get; init; }
}