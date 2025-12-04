using Sanet.MakaMek.Core.Data.Game.Commands.Client.Builders;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

public class DeploymentState : IUiState
{
    private readonly BattleMapViewModel _viewModel;
    private readonly DeploymentCommandBuilder _builder;
    private readonly ILocalizationService _localizationService;
    private Hex? _selectedHex;
    
    public IClientGame? Game => _viewModel.Game;
    
    private enum SubState
    {
        SelectingUnit,
        SelectingHex,
        SelectingDirection,
        Completed
    }
    
    private SubState _currentSubState = SubState.SelectingUnit;

    public DeploymentState(BattleMapViewModel viewModel)
    {
        _viewModel = viewModel;
        _localizationService = viewModel.LocalizationService;
        if (_viewModel.Game == null)
        {
            throw new InvalidOperationException("Game is null"); 
        }
        if (_viewModel.Game.ActivePlayer == null)
        {
            throw new InvalidOperationException("Active player is null"); 
        }
        _builder = new DeploymentCommandBuilder(_viewModel.Game.Id, _viewModel.Game.ActivePlayer.Id);
    }

    public void  HandleUnitSelection(IUnit? unit)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (_currentSubState != SubState.SelectingUnit) return;
        
        if (unit == null) return;
        
        _builder.SetUnit(unit);
        _currentSubState = SubState.SelectingHex;
        _viewModel.NotifyStateChanged();
    }

    public void HandleHexSelection(Hex hex)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (_currentSubState is SubState.SelectingHex 
                             or SubState.SelectingDirection) HandleHexForDeployment(hex);
    }

    public void HandleFacingSelection(HexDirection direction)
    {
        if (!this.CanHumanPlayerAct()) return;
        if (_currentSubState != SubState.SelectingDirection) return;
        _builder.SetDirection(direction);
        _viewModel.HideDirectionSelector();
        CompleteDeployment();
    }

    private static IEnumerable<HexDirection> GetAllDirections()
    {
        return
        [
            HexDirection.Top,
            HexDirection.TopRight,
            HexDirection.BottomRight,
            HexDirection.Bottom,
            HexDirection.BottomLeft,
            HexDirection.TopLeft
        ];
    }

    private void HandleHexForDeployment(Hex hex)
    {
        if (IsHexOccupied(hex))
            return;
            
        _selectedHex = hex;
        _builder.SetPosition(hex.Coordinates);
        _currentSubState = SubState.SelectingDirection;
        
        _viewModel.ShowDirectionSelector(_selectedHex.Coordinates, GetAllDirections());
    }

    private bool IsHexOccupied(Hex hex)
    {
        return _viewModel.Units.Any(u => u.IsDeployed &&u.Position!.Coordinates == hex.Coordinates);
    }

    private void CompleteDeployment()
    {
        var command = _builder.Build();
        if (command != null && _viewModel.Game is { } clientGame)
        {
            clientGame.DeployUnit(command.Value);
        }
        
        _builder.Reset();
        _selectedHex = null;
        _currentSubState = SubState.Completed;
        _viewModel.NotifyStateChanged();
    }

    public string ActionLabel
    {
        get
        {
            if (!IsActionRequired)
                return string.Empty;
            return _currentSubState switch
            {
                SubState.SelectingUnit => _localizationService.GetString("Action_SelectUnitToDeploy"),
                SubState.SelectingHex => _localizationService.GetString("Action_SelectDeploymentHex"),
                SubState.SelectingDirection => _localizationService.GetString("Action_SelectFacingDirection"),
                _ => string.Empty
            };
        }
    }

    public bool IsActionRequired
    {
        get
        {
            if (_viewModel.Game is not { } clientGame)
                return false;
            return clientGame is { CanActivePlayerAct:true, UnitsToPlayCurrentStep: > 0 }
                   && this.IsActiveHumanPlayer();
        }
    }
}

