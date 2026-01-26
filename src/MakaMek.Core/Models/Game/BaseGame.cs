using System.Reactive.Linq;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using System.Reactive.Subjects;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Microsoft.Extensions.Logging;

namespace Sanet.MakaMek.Core.Models.Game;

public abstract class BaseGame : IGame
{
    public ILogger Logger { get; }
    
    internal readonly ICommandPublisher CommandPublisher;
    private readonly List<IPlayer> _players = [];
    private readonly IMechFactory _mechFactory;

    private readonly Subject<int> _turnSubject = new();
    private readonly Subject<PhaseNames> _phaseSubject = new();
    private readonly Subject<PhaseStepState?> _phaseStepStateSubject = new();

    public Guid Id { get; }

    public IObservable<int> TurnChanges => _turnSubject.AsObservable();
    public IObservable<PhaseNames> PhaseChanges => _phaseSubject.AsObservable();
    public IObservable<PhaseStepState?> PhaseStepChanges => _phaseStepStateSubject.AsObservable();
    public IBattleMap? BattleMap { get; protected set; }
    public IToHitCalculator ToHitCalculator { get; }
    public IPilotingSkillCalculator PilotingSkillCalculator { get; }
    public IRulesProvider RulesProvider { get; }
    public IConsciousnessCalculator ConsciousnessCalculator { get; }
    public IHeatEffectsCalculator HeatEffectsCalculator { get; }

    public int Turn
    {
        get;
        protected set
        {
            if (field == value) return;
            field = value;
            _turnSubject.OnNext(value);
        }
    } = 1;

    public virtual PhaseNames TurnPhase
    {
        get;
        protected set
        {
            if (field == value) return;
            field = value;
            _phaseSubject.OnNext(value);
            PhaseStepState = null;

            // Reset phase damage tracking for all units when the phase changes
            foreach (var player in AlivePlayers)
            {
                foreach (var unit in player.AliveUnits)
                {
                    unit.ResetPhaseState();
                }
            }
        }
    } = PhaseNames.Start;

    public virtual PhaseStepState? PhaseStepState
    {
        get;
        protected set
        {
            if (field == value) return;
            field = value;
            if (TurnPhase != PhaseNames.Initiative)
                _phaseStepStateSubject.OnNext(value);
        }
    }

    /// <summary>
    /// Returns only players with at least one alive unit (i.e., those that can act)
    /// </summary>
    public IReadOnlyList<IPlayer> AlivePlayers => _players
        .Where(p => p.CanAct)
        .ToList();

    protected BaseGame(IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        ILogger logger)
    {
        Logger = logger;
        Id = Guid.NewGuid();
        RulesProvider = rulesProvider;
        CommandPublisher = commandPublisher;
        _mechFactory = mechFactory;
        ToHitCalculator = toHitCalculator;
        PilotingSkillCalculator = pilotingSkillCalculator;
        ConsciousnessCalculator = consciousnessCalculator;
        HeatEffectsCalculator = heatEffectsCalculator;
        CommandPublisher.Subscribe(HandleCommand);
    }

    public IReadOnlyList<IPlayer> Players => _players;
    
    public virtual void SetBattleMap(IBattleMap map)
    {
        if (BattleMap != null) return; // Prevent changing map 
        BattleMap = map;
    }

    internal void OnPlayerJoined(JoinGameCommand joinGameCommand)
    {
        if (!ValidateJoinCommand(joinGameCommand)) return;
        
        var controlType = GetLocalPlayerControlType(joinGameCommand.PlayerId) ?? PlayerControlType.Remote;
        var player = new Player(joinGameCommand.PlayerId,
            joinGameCommand.PlayerName,
            controlType,
            joinGameCommand.Tint);

        // Create units from unit data
        foreach (var unitData in joinGameCommand.Units)
        {
            var unit = _mechFactory.Create(unitData);

            // Find and assign a pilot for this unit
            var pilotAssignment = joinGameCommand.PilotAssignments.FirstOrDefault(pa => pa.UnitId == unitData.Id);
            if (pilotAssignment.UnitId != Guid.Empty)
            {
                var pilot = new MechWarrior(pilotAssignment.PilotData);
                unit.AssignPilot(pilot);
            }

            player.AddUnit(unit);
        }

        player.Status = PlayerStatus.Joined;
        _players.Add(player);
    }
    
    protected virtual void OnPlayerLeft(PlayerLeftCommand command)
    {
        // Find the player
        var player = Players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return; // Player already removed - idempotent

        // Mark the player as left by setting status
        player.Status = PlayerStatus.NotJoined;
        _players.Remove(player);
    }
    
    internal void OnPlayerStatusUpdated(UpdatePlayerStatusCommand updatePlayerStatusCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == updatePlayerStatusCommand.PlayerId);
        if (player == null) return;
        player.Status = updatePlayerStatusCommand.PlayerStatus;
    }

    internal void OnDeployUnit(DeployUnitCommand command)
    {
        var player = _players.FirstOrDefault(p => p.Id == command.PlayerId);
        if (player == null) return;
        var unit = player.Units.FirstOrDefault(u => u.Id == command.UnitId && !u.IsDeployed);
        unit?.Deploy(new HexPosition(new HexCoordinates(command.Position), (HexDirection)command.Direction));
    }
    
    internal void OnMoveUnit(MoveUnitCommand moveCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        if (player == null) return;
        var unit = player.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId);
        unit?.Move(new MovementPath(moveCommand.MovementPath, moveCommand.MovementType));
    }
    
    internal void OnWeaponConfiguration(WeaponConfigurationCommand configCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == configCommand.PlayerId);

        var unit = player?.Units.FirstOrDefault(u => u.Id == configCommand.UnitId);

        unit?.ApplyWeaponConfiguration(configCommand.Configuration);
    }
    
    internal void OnWeaponsAttack(WeaponAttackDeclarationCommand attackCommand)
    {
        // Find the attacking player
        var player = _players.FirstOrDefault(p => p.Id == attackCommand.PlayerId);
        if (player == null) return;

        // Find the attacking unit
        var attackerUnit = player.Units.FirstOrDefault(u => u.Id == attackCommand.UnitId);
        if (attackerUnit == null) return;

        // Validate that the unit can fire weapons
        if (!attackerUnit.CanFireWeapons) return;
        
        // Declare the weapon attack
        attackerUnit.DeclareWeaponAttack(attackCommand.WeaponTargets);
    }

    internal void OnWeaponsAttackResolution(WeaponAttackResolutionCommand attackResolutionCommand)
    {
        // Find the attacking unit
        var attackerUnit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == attackResolutionCommand.AttackerId);

        if (attackerUnit == null) return;
        // Fire the weapon from the attacker unit
        attackerUnit.FireWeapon(attackResolutionCommand.WeaponData);
        // Find the target unit with the target ID
        var targetUnit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == attackResolutionCommand.TargetId);

        if (targetUnit == null) return;

        var res = attackResolutionCommand.ResolutionData;
        if (!res.IsHit) return;

        // Apply damage only if we have hit locations
        if (res.HitLocationsData is not null)
        {
            targetUnit.ApplyDamage(res.HitLocationsData.HitLocations, res.AttackDirection);
        }

        // Apply external heat on hit
        if (res.ExternalHeat > 0)
        {
            targetUnit.AddExternalHeat(attackResolutionCommand.WeaponData.Name ?? "Unknown", res.ExternalHeat);
        }
    }

    internal void OnMechFalling(MechFallCommand fallCommand)
    {
        // Find the unit with the given ID across all players
        var mech = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == fallCommand.UnitId) as Mech;
        
        if (fallCommand.FallPilotingSkillRoll?.RollType == PilotingSkillRollType.StandupAttempt)
        {
            mech?.AttemptStandup();
        }

        // Apply falling to the unit using the falling data from the command if present
        if (fallCommand.DamageData is not { HitLocations.HitLocations: var hits }) return;
        mech?.ApplyDamage(hits, fallCommand.DamageData.FallDirection);
        mech?.SetProne();
        if (fallCommand.IsPilotTakingDamage)
        {
            mech?.Pilot?.Hit();
        }
    }

    internal void OnMechStandUp(MechStandUpCommand standUpCommand)
    {
        // Find the unit with the given ID across all players
        var mech = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == standUpCommand.UnitId) as Mech;

        mech?.StandUp(standUpCommand.NewFacing);
        mech?.AttemptStandup();
    }

    internal void OnUnitShutdown(UnitShutdownCommand shutdownCommand)
    {
        // Find the unit with the given ID across all players
        var unit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == shutdownCommand.UnitId);

        if (unit == null) return;

        // Apply shutdown when automatic, voluntary, or avoid-roll failed (i.e., not avoided)
        if (shutdownCommand.IsAutomaticShutdown 
            || shutdownCommand.ShutdownData.Reason == ShutdownReason.Voluntary
            || shutdownCommand.AvoidShutdownRoll?.IsSuccessful == false)
        {
            unit.Shutdown(shutdownCommand.ShutdownData);
        }
    }

    internal void OnMechRestart(UnitStartupCommand restartCommand)
    {
        // Find the unit with the given ID across all players
        var unit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == restartCommand.UnitId);

        if (unit == null
            || restartCommand.IsRestartPossible == false) return;

        // Apply restart to the unit if restart was successful
        if (restartCommand.AvoidShutdownRoll?.IsSuccessful == true
            || restartCommand.IsAutomaticRestart)
        {
            unit.Startup();
        }
    }

    internal void OnHeatUpdate(HeatUpdatedCommand heatUpdatedCommand)
    {
        // Find the unit with the given ID across all players
        var unit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == heatUpdatedCommand.UnitId);

        if (unit == null) return;

        if (unit.HasAppliedHeat) return;

        // Apply heat to the unit using the heat data from the command
        unit.ApplyHeat(heatUpdatedCommand.HeatData);
    }
    
    /// <summary>
    /// Handles a turn ended command by resetting the turn state for all units of the player
    /// </summary>
    /// <param name="playerId">The id of a player who sent a turn ended command</param>
    internal void OnTurnEnded(Guid playerId)
    {
        var player = _players.FirstOrDefault(p => p.Id == playerId);
        if (player == null) return;
        
        // Reset the turn state for all units of the player
        foreach (var unit in player.Units)
        {
            unit.ResetTurnState();
        }
    }
    
    internal void OnPhysicalAttack(PhysicalAttackCommand attackCommand)
    {
        Logger.LogInformation("Physical attacks are not implemented");
    }

    internal void OnAmmoExplosion(AmmoExplosionCommand explosionCommand)
    {
        // Find the unit with the given ID across all players
        var unit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == explosionCommand.UnitId);

        if (unit == null) return;

        // Apply critical hits data
        if (explosionCommand.CriticalHits is { Count: > 0 })
        {
            unit.ApplyCriticalHits(explosionCommand.CriticalHits);
        }
    }

    internal void OnCriticalHitsResolution(CriticalHitsResolutionCommand criticalHitsCommand)
    {
        // Find the target unit with the given ID across all players
        var unit = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == criticalHitsCommand.TargetId);

        if (unit == null) return;

        // Apply the pre-calculated critical hits data
        if (criticalHitsCommand.CriticalHits is { Count: > 0 })
        {
            unit.ApplyCriticalHits(criticalHitsCommand.CriticalHits);
        }
    }

    /// <summary>
    /// Handles a pilot consciousness roll command by updating the pilot's consciousness state
    /// </summary>
    /// <param name="command">The consciousness roll command</param>
    internal void OnPilotConsciousnessRoll(PilotConsciousnessRollCommand command)
    {
        var pilot = _players
            .SelectMany(p => p.Units)
            .Select(u => u.Pilot)
            .FirstOrDefault(p => p?.Id == command.PilotId);
        if (pilot == null) return;

        if (command.IsRecoveryAttempt)
        {
            if (command.IsSuccessful)
                pilot.RecoverConsciousness();
        }
        else
        {
            if (!command.IsSuccessful)
                pilot.KnockUnconscious(Turn);
        }
    }
    
    protected bool ValidateCommand(IGameCommand command)
    {
        return command switch
        {
            JoinGameCommand joinGameCommand => ValidateJoinCommand(joinGameCommand),
            UpdatePlayerStatusCommand playerStateCommand => ValidatePlayer(playerStateCommand),
            DeployUnitCommand deployUnitCommand => ValidateDeployCommand(deployUnitCommand),
            TurnIncrementedCommand turnIncrementedCommand => ValidateTurnIncrementedCommand(turnIncrementedCommand),
            SetBattleMapCommand => true,
            MoveUnitCommand => true,
            WeaponConfigurationCommand => true,
            WeaponAttackDeclarationCommand=> true,
            WeaponAttackResolutionCommand => true,
            HeatUpdatedCommand => true,
            TurnEndedCommand => true,
            RequestGameLobbyStatusCommand => true,
            MechFallCommand => true,
            TryStandupCommand => true,
            MechStandUpCommand => true,
            UnitShutdownCommand => true,
            UnitStartupCommand => true,
            PilotConsciousnessRollCommand => true,
            ShutdownUnitCommand => true,
            StartupUnitCommand => true,
            AmmoExplosionCommand => true,
            CriticalHitsResolutionCommand => true,
            PlayerLeftCommand => true,
            GameEndedCommand => true,
            _ => false
        };
    }

    private bool ValidatePlayer(UpdatePlayerStatusCommand updatePlayerStateCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == updatePlayerStateCommand.PlayerId);
        return player != null;
    }

    protected bool ShouldHandleCommand(IGameCommand command)
    {
        return command.GameOriginId != Id && command.GameOriginId != Guid.Empty;
    }

    private bool ValidateJoinCommand(JoinGameCommand joinCommand)
    {
        var existingPlayer = _players.FirstOrDefault(p => p.Id == joinCommand.PlayerId);
        if (existingPlayer == null) return true;
        Logger.LogInformation("Player {PlayerName} already joined the game.", joinCommand.PlayerName);
        return false;
    }

    private bool ValidateDeployCommand(DeployUnitCommand deployUnitCommand)
    {
        var position = new HexCoordinates(deployUnitCommand.Position);
        if (!position.IsOccupied(this)) return deployUnitCommand.PlayerId != Guid.Empty;
        Logger.LogInformation("Hex {Position} is already occupied.", position);
        return false;
    }
    
    protected bool ValidateTurnIncrementedCommand(TurnIncrementedCommand command)
    {
        // Validate that the turn number is only incremented by 1
        return command.TurnNumber == Turn + 1;
    }

    public abstract void HandleCommand(IGameCommand command);

    protected abstract PlayerControlType? GetLocalPlayerControlType(Guid playerId);
}