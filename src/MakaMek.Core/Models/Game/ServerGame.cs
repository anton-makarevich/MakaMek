using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

namespace Sanet.MakaMek.Core.Models.Game;

public class ServerGame : BaseGame, IDisposable
{
    private IReadOnlyList<IPlayer> _initiativeOrder = BaseGame.NoPlayers;
    private readonly ServerCommandProcessor _commandProcessor;
    private readonly ServerPhaseCoordinator _phaseCoordinator;

    public bool IsAutoRoll { get; set; } = true;
    public IDiceRoller DiceRoller { get; }

    public ServerGame(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IDiceRoller diceRoller,
        IToHitCalculator toHitCalculator,
        IDamageTransferCalculator damageTransferCalculator,
        ICriticalHitsCalculator criticalHitsCalculator,
        IHullBreachCalculator hullBreachCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IPhysicalAttackEligibility physicalAttackEligibility,
        IPhysicalAttackCalculator physicalAttackCalculator,
        IMovementAttackDeclarationValidator movementAttackDeclarationValidator,
        IFallProcessor fallProcessor,
        IWeaponAttackResolver weaponAttackResolver,
        IPhysicalAttackResolver physicalAttackResolver,
        ILogger<ServerGame> logger,
        IPhaseManager? phaseManager = null)
        : base(rulesProvider, mechFactory, commandPublisher, toHitCalculator, pilotingSkillCalculator, consciousnessCalculator, heatEffectsCalculator, physicalAttackEligibility, physicalAttackCalculator, movementAttackDeclarationValidator, logger)
    {
        DiceRoller = diceRoller;
        DamageTransferCalculator = damageTransferCalculator;
        CriticalHitsCalculator = criticalHitsCalculator;
        HullBreachCalculator = hullBreachCalculator;
        FallProcessor = fallProcessor;
        WeaponAttackResolver = weaponAttackResolver;
        PhysicalAttackResolver = physicalAttackResolver;
        _commandProcessor = new ServerCommandProcessor(this);
        _phaseCoordinator = new ServerPhaseCoordinator(this, phaseManager);
    }
    
    public bool IsDisposed { get; private set; }

    public IDamageTransferCalculator DamageTransferCalculator { get; }

    public ICriticalHitsCalculator CriticalHitsCalculator { get; }

    public IHullBreachCalculator HullBreachCalculator { get; }

    public IFallProcessor FallProcessor { get; }

    public IWeaponAttackResolver WeaponAttackResolver { get; }

    public IPhysicalAttackResolver PhysicalAttackResolver { get; }

    public IReadOnlyList<IPlayer> InitiativeOrder => _initiativeOrder;
    
    public bool IsGameOver { get; private set; }

    public override void SetBattleMap(IBattleMap map)
    {
        if (TurnPhase!= PhaseNames.Start) return; // Prevent changing map mid-game
        BattleMap = map;
        
        // Create and publish a command to send the map to all clients
        CommandPublisher.PublishCommand(ServerGameCommandFactory.SetBattleMap(Id, map));
    }

    /// <summary>
    /// Attempts to transition the server game out of the Start phase. Call this
    /// explicitly when the host starts the game; <see cref="SetBattleMap"/> only
    /// broadcasts the map and never advances the phase on its own.
    /// </summary>
    public void TryStartGame()
    {
        _phaseCoordinator.TryStartGame();
    }
    
    public void SetInitiativeOrder(IReadOnlyList<IPlayer> order)
    {
        _initiativeOrder = order;
    }

    public void TransitionToNextPhase(PhaseNames currentPhase)
    {
        _phaseCoordinator.TransitionToNextPhase(currentPhase);
    }

    public override void HandleCommand(IGameCommand command)
    {
        _commandProcessor.Process(command);
    }

    // Bridge used by the extracted collaborators, which are not derived from BaseGame.
    internal void DispatchToPhase(IGameCommand command) => _phaseCoordinator.Dispatch(command);
    internal void HandlePlayerLeft(PlayerLeftCommand command) => OnPlayerLeft(command);

    protected override PlayerControlType? GetLocalPlayerControlType(Guid playerId)
    {
        return null;
    }

    protected override void OnPlayerLeft(PlayerLeftCommand command)
    {
        base.OnPlayerLeft(command);
        CommandPublisher.PublishCommand(command with { GameOriginId = Id });
        // At the moment it's not possible to continue even if one player is leaving
        StopGame(GameEndReason.PlayersLeft, command.PlayerId);
    }

    /// <summary>
    /// Stops the game and notifies all clients
    /// </summary>
    public void StopGame(GameEndReason reason, Guid? playerId = null)
    {
        if (IsDisposed || IsGameOver) return;

        // Publish GameEndedCommand to all clients
        CommandPublisher.PublishCommand(ServerGameCommandFactory.GameEnded(Id, reason, playerId));

        // Mark the game as over (will exit Start() loop)
        IsGameOver = true;
    }

    public void SetActivePlayer(IPlayer? player, int unitsToMove)
    {
        _phaseCoordinator.SetActivePlayer(player, unitsToMove);
    }

    public void SetPhase(PhaseNames phase)
    {
        TurnPhase = phase;

        // Clear processed command keys when phase changes
        _commandProcessor.ResetDuplicateTracking();

        CommandPublisher.PublishCommand(ServerGameCommandFactory.Phase(Id, phase));
    }

    public void IncrementTurn()
    {
        Turn++;
        _initiativeOrder = BaseGame.NoPlayers; // Clear initiative order at the start of new turn

        // Send a turn increment command to all clients
        CommandPublisher.PublishCommand(ServerGameCommandFactory.TurnIncremented(Id, Turn));
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        IsGameOver = true; // Ensure the loop in Start() exits
        CommandPublisher.Unsubscribe(HandleCommand);
        DisposeCommandResources();
    }
}

