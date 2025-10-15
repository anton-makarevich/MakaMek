using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for the end game summary screen
/// Displays all players, their units, and the victory status
/// </summary>
public class EndGameViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private ClientGame? _game;
    private GameEndReason _endReason;

    public EndGameViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        ReturnToMenuCommand = new AsyncCommand(ReturnToMenu);
    }

    /// <summary>
    /// Initialize the view model with the game and end reason
    /// </summary>
    public void Initialize(ClientGame game, GameEndReason endReason)
    {
        _game = game;
        _endReason = endReason;
        InitializePlayers();
        NotifyPropertyChanged(nameof(TitleText));
        NotifyPropertyChanged(nameof(SubtitleText));
    }

    /// <summary>
    /// Gets the list of players ordered by victory status (victor first, then others)
    /// </summary>
    public ObservableCollection<EndGamePlayerViewModel> Players { get; } = [];

    /// <summary>
    /// Gets the title text for the end game screen
    /// </summary>
    public string TitleText => _endReason == GameEndReason.Victory
        ? _localizationService.GetString("EndGame_Victory_Title")
        : _localizationService.GetString("EndGame_Title");

    /// <summary>
    /// Gets the subtitle text describing the outcome
    /// </summary>
    public string SubtitleText
    {
        get
        {
            if (_endReason != GameEndReason.Victory)
                return _localizationService.GetString($"EndGame_{_endReason}_Subtitle");

            var victor = Players.FirstOrDefault(p => p.IsVictor);
            if (victor != null)
            {
                return _localizationService.GetString("EndGame_Victory_Subtitle")
                    .Replace("{PlayerName}", victor.Name);
            }

            return _localizationService.GetString("EndGame_Draw_Subtitle");
        }
    }

    /// <summary>
    /// Command to return to the main menu
    /// </summary>
    public ICommand ReturnToMenuCommand { get; }

    private void InitializePlayers()
    {
        if (_game == null) return;

        // Determine the victor (player with alive units, if any)
        var alivePlayers = _game.AlivePlayers.ToList();
        var victorId = alivePlayers.Count == 1 ? alivePlayers[0].Id : (Guid?)null;

        // Create view models for all players, ordered by victory status
        var playerViewModels = _game.Players
            .Select(p => new EndGamePlayerViewModel(p, p.Id == victorId))
            .OrderByDescending(p => p.IsVictor)
            .ThenBy(p => p.Name);

        foreach (var playerVm in playerViewModels)
        {
            Players.Add(playerVm);
        }
    }

    private async Task ReturnToMenu()
    {
        // Dispose the game
        _game?.Dispose();

        // Navigate back to the main menu
        await NavigationService.NavigateToRootAsync();
    }
}

