using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Runs the torso-rotation configuration step of the weapons-attack phase: the available directions,
/// the configuration command and the direction-selector visibility. Kept out of
/// <see cref="WeaponsAttackState"/> so the state only orchestrates the step transitions.
/// </summary>
public sealed class WeaponsConfigurationController
{
    private readonly BattleMapViewModel _viewModel;
    private readonly IClientGame _game;
    private readonly List<HexDirection> _availableDirections = [];

    public WeaponsConfigurationController(BattleMapViewModel viewModel, IClientGame game)
    {
        _viewModel = viewModel;
        _game = game;
    }

    public void UpdateAvailableDirections(IUnit? attacker)
    {
        _availableDirections.Clear();

        if (attacker == null) return;

        var torsoRotationOption = attacker
            .GetWeaponsConfigurationOptions()
            .FirstOrDefault(o => o.Type == WeaponConfigurationType.TorsoRotation);

        if (torsoRotationOption.AvailableDirections is not { Count: > 0 }) return;

        _availableDirections.AddRange(torsoRotationOption.AvailableDirections.OrderBy(d => (int)d));
    }

    public bool CanRotateTo(HexDirection direction) => _availableDirections.Contains(direction);

    /// <summary>
    /// Applies the selected torso rotation and returns to action selection.
    /// </summary>
    public void ApplyTorsoRotation(IUnit attacker, HexDirection direction)
    {
        _viewModel.HideDirectionSelector();

        var command = new WeaponConfigurationCommand
        {
            GameOriginId = _game.Id,
            PlayerId = _game.PhaseStepState!.Value.ActivePlayer.Id,
            UnitId = attacker.Id,
            Configuration = new WeaponConfiguration
            {
                Type = WeaponConfigurationType.TorsoRotation,
                Value = (int)direction
            }
        };

        _game.ConfigureUnitWeapons(command);
    }

    public void HideDirectionSelector() => _viewModel.HideDirectionSelector();

    /// <summary>
    /// The torso-rotation action for the action menu, when the attacker can twist.
    /// </summary>
    public Action? GetTorsoRotationAction(IUnit attacker, Action onStepChanged)
    {
        var torsoRotationOption = attacker
            .GetWeaponsConfigurationOptions()
            .FirstOrDefault(o => o.Type == WeaponConfigurationType.TorsoRotation);
        if (torsoRotationOption.AvailableDirections is not { Count: > 0 }) return null;

        return () =>
        {
            if (attacker.Position == null) return;
            UpdateAvailableDirections(attacker);
            _viewModel.ShowDirectionSelector(attacker.Position.Coordinates, _availableDirections);
            onStepChanged();
        };
    }
}
