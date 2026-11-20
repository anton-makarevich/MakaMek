namespace Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;

public abstract class ClientCommandBuilder(Guid gameId, Guid playerId)
{
    protected Guid GameId { get; } = gameId;
    protected Guid PlayerId { get; } = playerId;

    public abstract bool CanBuild { get; }
}
