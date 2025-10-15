using System.Collections.ObjectModel;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// ViewModel for a player in the end game summary
/// </summary>
public class EndGamePlayerViewModel
{
    private readonly IPlayer _player;
    private readonly ILocalizationService _localizationService;

    public EndGamePlayerViewModel(IPlayer player, bool isVictor, ILocalizationService localizationService)
    {
        _player = player;
        IsVictor = isVictor;
        _localizationService = localizationService;

        // Create view models for all units
        Units = new ObservableCollection<EndGameUnitViewModel>(
            player.Units.Select(u => new EndGameUnitViewModel(u)));
    }

    /// <summary>
    /// Gets the player's name
    /// </summary>
    public string Name => _player.Name;

    /// <summary>
    /// Gets the player's color tint
    /// </summary>
    public string Tint => _player.Tint;

    /// <summary>
    /// Gets whether this player is the victor
    /// </summary>
    public bool IsVictor { get; }

    /// <summary>
    /// Gets the list of units for this player
    /// </summary>
    public ObservableCollection<EndGameUnitViewModel> Units { get; }

    public string VictorBadgeText => IsVictor
    ? _localizationService.GetString("EndGame_Victor_Badge")
    : string.Empty;
}

