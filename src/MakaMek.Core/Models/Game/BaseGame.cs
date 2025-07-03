using System.Reactive.Linq;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Utils.TechRules;
using System.Reactive.Subjects;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Core.Models.Game;

public abstract class BaseGame : IGame
{
    
    internal readonly ICommandPublisher CommandPublisher;
    private readonly List<IPlayer> _players = [];
    private readonly IMechFactory _mechFactory;
    
    private PhaseNames _turnPhases = PhaseNames.Start;
    private int _turn = 1;
    private IPlayer? _activePlayer;
    private int _unitsToPlayCurrentStep;

    private readonly Subject<int> _turnSubject = new();
    private readonly Subject<PhaseNames> _phaseSubject = new();
    private readonly Subject<IPlayer?> _activePlayerSubject = new();
    private readonly Subject<int> _unitsToPlaySubject = new();

    public Guid Id { get; }
    public IObservable<int> TurnChanges => _turnSubject.AsObservable();
    public IObservable<PhaseNames> PhaseChanges => _phaseSubject.AsObservable();
    public IObservable<IPlayer?> ActivePlayerChanges => _activePlayerSubject.AsObservable();
    public IObservable<int> UnitsToPlayChanges => _unitsToPlaySubject.AsObservable();
    public BattleMap? BattleMap { get; protected set; }
    public IToHitCalculator ToHitCalculator { get; }
    public IPilotingSkillCalculator PilotingSkillCalculator { get; }
    public IRulesProvider RulesProvider { get; }
    
    public int Turn
    {
        get => _turn;
        protected set
        {
            if (_turn == value) return;
            _turn = value;
            _turnSubject.OnNext(value);
        }
    }

    public virtual PhaseNames TurnPhase
    {
        get => _turnPhases;
        protected set
        {
            if (_turnPhases == value) return;
            _turnPhases = value;
            _phaseSubject.OnNext(value);
            ActivePlayer = null;
            UnitsToPlayCurrentStep = 0;
            
            // Reset phase damage tracking for all units when phase changes
            foreach (var player in AlivePlayers)
            {
                foreach (var unit in player.AliveUnits)
                {
                    unit.ResetPhaseState();
                }
            }
        }
    }

    public virtual IPlayer? ActivePlayer
    {
        get => _activePlayer;
        protected set
        {
            if (_activePlayer == value) return;
            _activePlayer = value;
            _activePlayerSubject.OnNext(value);
        }
    }

    public int UnitsToPlayCurrentStep
    {
        get => _unitsToPlayCurrentStep;
        protected set
        {
            if (_unitsToPlayCurrentStep == value) return;
            _unitsToPlayCurrentStep = value;
            _unitsToPlaySubject.OnNext(value);
        }
    }

    /// <summary>
    /// Returns only players with at least one alive unit (i.e., those that can act)
    /// </summary>
    public IReadOnlyList<IPlayer> AlivePlayers => _players
        .Where(p => p.CanAct)
        .ToList();

    protected BaseGame(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator)
    {
        Id = Guid.NewGuid(); 
        RulesProvider = rulesProvider;
        CommandPublisher = commandPublisher;
        _mechFactory = mechFactory;
        ToHitCalculator = toHitCalculator;
        PilotingSkillCalculator = pilotingSkillCalculator;
        CommandPublisher.Subscribe(HandleCommand);
    }

    public IReadOnlyList<IPlayer> Players => _players;
    
    public virtual void SetBattleMap(BattleMap map)
    {
        if (BattleMap != null) return; // Prevent changing map 
        BattleMap = map;
    }

    internal void OnPlayerJoined(JoinGameCommand joinGameCommand)
    {
        if (!ValidateJoinCommand(joinGameCommand)) return;
        var player = new Player(joinGameCommand.PlayerId, joinGameCommand.PlayerName,joinGameCommand.Tint);
        foreach (var unit in joinGameCommand.Units.Select(unitData => _mechFactory.Create(unitData)))
        {
            player.AddUnit(unit);
        }

        player.Status = PlayerStatus.Joined;
        _players.Add(player);
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
    
    public void OnMoveUnit(MoveUnitCommand moveCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == moveCommand.PlayerId);
        if (player == null) return;
        var unit = player.Units.FirstOrDefault(u => u.Id == moveCommand.UnitId);
        unit?.Move(
            moveCommand.MovementType,
            moveCommand.MovementPath);
    }
    
    internal void OnWeaponConfiguration(WeaponConfigurationCommand configCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == configCommand.PlayerId);
        if (player == null) return;

        var unit = player.Units.FirstOrDefault(u => u.Id == configCommand.UnitId);
        if (unit == null) return;

        switch (configCommand.Configuration.Type)
        {
            case WeaponConfigurationType.TorsoRotation when unit is Mech mech:
                mech.RotateTorso((HexDirection)configCommand.Configuration.Value);
                break;
            case WeaponConfigurationType.ArmsFlip:
                // Handle arms flip when implemented
                break;
        }
    }
    
    internal void OnWeaponsAttack(WeaponAttackDeclarationCommand attackCommand)
    {
        // Find the attacking player
        var player = _players.FirstOrDefault(p => p.Id == attackCommand.PlayerId);
        if (player == null) return;
        
        // Find the attacking unit
        var attackerUnit = player.Units.FirstOrDefault(u => u.Id == attackCommand.AttackerId);
        if (attackerUnit == null) return;
        
        // Find all target units
        var targetIds = attackCommand.WeaponTargets.Select(wt => wt.TargetId).Distinct().ToList();
        var targetUnits = _players
            .SelectMany(p => p.Units)
            .Where(u => targetIds.Contains(u.Id))
            .ToList();
        
        // Declare the weapon attack
        attackerUnit.DeclareWeaponAttack(attackCommand.WeaponTargets, targetUnits);
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
        
        // Apply damage to the target unit using the hit locations data
        if (attackResolutionCommand.ResolutionData is { IsHit: true, HitLocationsData: not null })
        {
            targetUnit.ApplyDamage(attackResolutionCommand.ResolutionData.HitLocationsData.HitLocations);
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
        mech?.ApplyDamage(hits);
        mech?.SetProne();
        if (fallCommand.IsPilotTakingDamage)
        {
            mech?.Crew?.Hit();
        }
    }

    internal void OnMechStandUp(MechStandUpCommand standUpCommand)
    {
        // Find the unit with the given ID across all players
        var mech = _players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == standUpCommand.UnitId) as Mech;
        
        mech?.StandUp();
        mech?.AttemptStandup();
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
    /// <param name="turnEndedCommand">The turn ended command</param>
    internal void OnTurnEnded(TurnEndedCommand turnEndedCommand)
    {
        var player = _players.FirstOrDefault(p => p.Id == turnEndedCommand.PlayerId);
        if (player == null) return;
        
        // Reset the turn state for all units of the player
        foreach (var unit in player.Units)
        {
            unit.ResetTurnState();
        }
    }
    
    internal void OnPhysicalAttack(PhysicalAttackCommand attackCommand)
    {
        Console.WriteLine("physical attack");
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
        if (existingPlayer != null) throw new InvalidOperationException("Player already exists");
        return joinCommand.PlayerId != Guid.Empty && _players.All(p => p.Id != joinCommand.PlayerId);
}

    private bool ValidateDeployCommand(DeployUnitCommand deployUnitCommand)
    {
        return deployUnitCommand.PlayerId!=Guid.Empty;
    }
    
    protected bool ValidateTurnIncrementedCommand(TurnIncrementedCommand command)
    {
        // Validate that the turn number is only incremented by 1
        return command.TurnNumber == Turn + 1;
    }

    public abstract void HandleCommand(IGameCommand command);
}