namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public abstract class ClientCommandBuilder(Guid gameId, Guid playerId)
{
    protected readonly Guid GameId = gameId;
    protected readonly Guid PlayerId = playerId;

    public abstract bool CanBuild { get; }
}
