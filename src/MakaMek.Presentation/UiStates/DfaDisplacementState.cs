using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// The interactive DFA miss displacement: the target's owner highlights the legal adjacent hexes,
/// selects one and confirms it. Every other click is ignored. The state disappears when the
/// server rebroadcasts the accepted selection, the request fails or the phase ends.
/// </summary>
public class DfaDisplacementState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly PendingDfaDisplacementRequest _request;
    private readonly IClientGame _game;
    private readonly HashSet<HexCoordinates> _candidateCoordinates;
    private PhysicalAttackDisplacementCandidateData? _selected;
    private bool _isSubmitting;

    public DfaDisplacementState(
        BattleMapViewModel viewModel,
        PendingDfaDisplacementRequest request)
    {
        _viewModel = viewModel;
        _request = request;
        _game = viewModel.Game as IClientGame
                ?? throw new InvalidOperationException("A DFA displacement requires a client game");

        _candidateCoordinates = request.Candidates
            .Select(candidate => new HexCoordinates(candidate.Coordinates))
            .ToHashSet();

        HighlightCandidates();
    }

    public IClientGame? Game => _game;

    public IUnit? SelectedUnit => _game.Players
        .SelectMany(player => player.Units)
        .FirstOrDefault(unit => unit.Id == _request.TargetUnitId);

    public string ActionLabel => _selected is null
        ? _viewModel.LocalizationService.GetString("Action_SelectDfaDisplacement")
        : _viewModel.LocalizationService.GetString("Action_ConfirmDfaDisplacement");

    public bool IsActionRequired => true;

    public bool CanExecutePlayerAction => _selected is not null && !_isSubmitting;

    public string PlayerActionLabel => ActionLabel;

    public void HandleUnitSelectionFromList(IUnit? unit)
    {
        // The selection is made on the map; the unit list is not part of this interaction.
    }

    public void HandleHexSelection(Hex hex)
    {
        if (_isSubmitting) return;

        var candidate = _request.Candidates.FirstOrDefault(candidate =>
            candidate.Coordinates.Q == hex.Coordinates.Q
            && candidate.Coordinates.R == hex.Coordinates.R);
        if (candidate is null) return;

        _selected = candidate;
        _viewModel.NotifyStateChanged();
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        // A displacement is a single-hex move; facing never changes.
    }

    public IEnumerable<StateAction> GetAvailableActions() => [];

    public void ExecutePlayerAction()
    {
        if (_selected is not { } selected || _isSubmitting) return;

        _isSubmitting = true;
        _viewModel.NotifyStateChanged();

        var command = new SelectPhysicalAttackDisplacementCommand
        {
            GameOriginId = _game.Id,
            Timestamp = DateTime.UtcNow,
            PlayerId = _request.OwnerPlayerId,
            UnitId = _request.TargetUnitId,
            RequestId = _request.RequestId,
            Coordinates = selected.Coordinates,
            Surface = selected.Surface
        };

        _ = _game.ConfirmDfaDisplacement(command).ContinueWith(
            task =>
            {
                if (task.Result) return;
                _isSubmitting = false;
                _viewModel.NotifyStateChanged();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Removes the candidate highlights. Idempotent, so leaving the state twice is safe.
    /// </summary>
    public void ClearHighlights()
    {
        if (_candidateCoordinates.Count == 0) return;
        _viewModel.RemoveHighlight<AttackReachableHighlight>(_candidateCoordinates);
    }

    private void HighlightCandidates()
    {
        var label = _viewModel.LocalizationService.GetString("DfaDisplacementHighlight");
        _viewModel.HighlightCoordinates(
            _candidateCoordinates,
            new AttackReachableHighlight([label]));
    }

    /// <summary>
    /// Whether this client's user controls the requesting player: a local human only. A remote
    /// player or a spectator never enters the selection state.
    /// </summary>
    public static bool IsLocalOwner(IClientGame game, PendingDfaDisplacementRequest request) =>
        game.LocalPlayers.Contains(request.OwnerPlayerId)
        && game.Players.FirstOrDefault(player => player.Id == request.OwnerPlayerId)?.ControlType
           == PlayerControlType.Human;
}
