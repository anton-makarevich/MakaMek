using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Controls.TemplatedControls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Views;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// Renders the HUD in each state worth looking at, at a desktop and a phone width, and checks the
/// overlays do not sit on top of each other.
///
/// The overlap check is the point. Two of the defects in this work were pieces of chrome pinned to
/// the same edge and drawn over each other, and both looked fine in every unit test: the player
/// action buttons inside the squad bar, and the squad bar under the map controls drawer. Screens
/// are evidence for a human; this fails the build.
/// </summary>
public class HudGalleryTests
{
    /// <summary>Gap below which two pieces of chrome are treated as touching rather than apart.</summary>
    private const double Tolerance = 0.5;

    public static TheoryData<string, int, int, bool> Layouts() => new()
    {
        { "desktop", 1280, 800, false },
        { "narrow", 430, 880, false },
        { "phone", 430, 880, true }
    };

    [Theory]
    [MemberData(nameof(Layouts))]
    public Task Hud_HasNoOverlappingChrome(string name, int width, int height, bool mobile) =>
        HarnessSession.Run(async () =>
        {
            foreach (var state in GalleryStates)
            {
                var (window, view, viewModel) = await ShowHud(width, height, mobile);
                try
                {
                    state.Apply(viewModel);
                    Settle(window);

                    using var frame = window.CaptureRenderedFrame();
                    if (frame is not null) Save(frame, $"gallery-{name}-{state.Name}.png");

                    var overlaps = Overlaps(view);
                    overlaps.ShouldBeEmpty(
                        $"{name}/{state.Name}: {string.Join("; ", overlaps)}");
                }
                finally
                {
                    window.Close();
                }
            }
        });

    /// <summary>
    /// The pieces of chrome that share the screen with the map. Each is found by the element that
    /// identifies it rather than by position, so a layout change cannot quietly drop one from the
    /// check.
    /// </summary>
    private static IReadOnlyList<(string Name, Rect Bounds, Visual Visual)> Chrome(BattleMapView view)
    {
        var found = new List<(string, Rect, Visual)>();

        void Add(string label, Visual? visual)
        {
            if (visual is null || !visual.IsEffectivelyVisible) return;
            if (visual.Bounds is { Width: <= 0 } or { Height: <= 0 }) return;
            var origin = visual.TranslatePoint(new Point(0, 0), view);
            if (origin is null) return;
            found.Add((label, new Rect(origin.Value, visual.Bounds.Size), visual));
        }

        Add("squad bar", view.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(sv => sv.Name == "SquadBar"));
        Add("map controls column", view.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(sp => sp.Name == "MapControlsColumn"));
        Add("action buttons", view.GetVisualDescendants().OfType<StackPanel>()
            .FirstOrDefault(sp => sp.Children.OfType<ItemsControl>()
                .Any(c => c.Name == "MobileActionButtonsPanel")));
        Add("record sheet", view.GetVisualDescendants().OfType<GamePanel>()
            .FirstOrDefault(p => p.Name == "RecordSheetPanel"));
        Add("turn status", view.GetVisualDescendants().OfType<Grid>()
            .FirstOrDefault(g => g.Name == "TurnStatus"));
        Add("drawer toggle", view.GetVisualDescendants().OfType<ActionButton>()
            .FirstOrDefault(a => a.Name == "MapControlsToggle"));

        return found;
    }

    private static List<string> Overlaps(BattleMapView view)
    {
        var chrome = Chrome(view);
        var clashes = new List<string>();

        for (var i = 0; i < chrome.Count; i++)
        for (var j = i + 1; j < chrome.Count; j++)
        {
            var a = chrome[i];
            var b = chrome[j];

            // One inside the other is nesting, not a collision: the toggle lives in the controls
            // column, so the column's bounds contain it by construction.
            if (IsRelated(a.Visual, b.Visual)) continue;

            var shared = a.Bounds.Intersect(b.Bounds);
            if (shared.Width > Tolerance && shared.Height > Tolerance)
                clashes.Add($"{a.Name} {a.Bounds} overlaps {b.Name} {b.Bounds}");
        }

        return clashes;
    }

    private static bool IsRelated(Visual a, Visual b) =>
        a.GetVisualAncestors().Contains(b) || b.GetVisualAncestors().Contains(a);

    private sealed record GalleryState(string Name, Action<BattleMapViewModel> Apply);

    private static readonly GalleryState[] GalleryStates =
    [
        new("base", _ => { }),
        new("acting", viewModel => SetState(viewModel, ActingState())),
        new("controls-open", viewModel => viewModel.ToggleMapControlsDrawer()),
        new("record-sheet", viewModel => viewModel.InspectUnit(viewModel.LocalUnits.First())),
        new("record-sheet-pinned", viewModel =>
        {
            viewModel.InspectUnit(viewModel.LocalUnits.First());
            viewModel.ToggleRecordSheetPin();
        }),
        new("acting-with-controls", viewModel =>
        {
            SetState(viewModel, ActingState());
            viewModel.ToggleMapControlsDrawer();
        })
    ];

    /// <summary>A state that puts the player action button on screen, which is where it overlapped.</summary>
    private static IUiState ActingState()
    {
        var state = Substitute.For<IUiState>();
        state.CanExecutePlayerAction.Returns(true);
        state.PlayerActionLabel.Returns("End Turn");
        state.ActionLabel.Returns("Select a unit to move");
        state.IsActionRequired.Returns(true);
        state.CanSelectUnit(Arg.Any<IUnit>()).Returns(true);
        return state;
    }

    private static void SetState(BattleMapViewModel viewModel, IUiState state)
    {
        typeof(BattleMapViewModel)
            .GetProperty(nameof(BattleMapViewModel.CurrentState))!
            .SetValue(viewModel, state);
        // NotifyStateChanged is what the real transition raises, and it is the only one that
        // reaches IsPlayerActionButtonVisible. Without it the button stays off screen and a test
        // meant to catch it overlapping something proves nothing.
        viewModel.NotifyStateChanged();
        viewModel.NotifySelectedUnitChanged();
    }

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel)>
        ShowHud(int width, int height, bool mobile)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = mobile ? MobileViewModel(services) : services.GetRequiredService<BattleMapViewModel>();

        var game = CreateClientGame(services);
        var units = await LocalGameFixture.LoadBundledUnitsAsync(services);
        var player = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human, "#4A90D9");

        viewModel.Game = game;
        game.SetBattleMap(services.GetRequiredService<IBattleMapFactory>()
            .GenerateMap(12, 10, new SingleTerrainGenerator(12, 10, new ClearTerrain())));
        game.JoinGameWithUnits(player, units.Take(4).ToList(), []);
        game.HandleCommand(new JoinGameCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = player.Id,
            PlayerName = player.Name,
            Tint = player.Tint,
            Units = units.Take(4).ToList(),
            PilotAssignments = []
        });

        var hex = 1;
        foreach (var unit in game.Players.SelectMany(p => p.Units))
        {
            game.HandleCommand(new DeployUnitCommand
            {
                GameOriginId = Guid.NewGuid(),
                PlayerId = player.Id,
                UnitId = unit.Id,
                Position = new HexCoordinates(hex, hex).ToData(),
                Direction = 0
            });
            hex++;
        }

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);
        return (window, view, viewModel);
    }

    /// <summary>
    /// The compact layout keys off IPlatformService.IsMobile, which the desktop graph answers no
    /// to. Building the view model by hand with that one service swapped is the only way to see
    /// the phone layout from here.
    /// </summary>
    private static BattleMapViewModel MobileViewModel(IServiceProvider services)
    {
        var platform = Substitute.For<IPlatformService>();
        platform.IsMobile.Returns(true);

        return new BattleMapViewModel(
            services.GetRequiredService<IImageService>(),
            services.GetRequiredService<ITerrainAssetService>(),
            services.GetRequiredService<ILocalizationService>(),
            services.GetRequiredService<IDispatcherService>(),
            services.GetRequiredService<IRulesProvider>(),
            platform,
            terrainBitmaskService: services.GetService<ITerrainBitmaskService>());
    }

    private static ClientGame CreateClientGame(IServiceProvider services) => new(
        services.GetRequiredService<IRulesProvider>(),
        services.GetRequiredService<IMechFactory>(),
        services.GetRequiredService<ICommandPublisher>(),
        services.GetRequiredService<IToHitCalculator>(),
        services.GetRequiredService<IPilotingSkillCalculator>(),
        services.GetRequiredService<IConsciousnessCalculator>(),
        services.GetRequiredService<IHeatEffectsCalculator>(),
        services.GetRequiredService<IBattleMapFactory>(),
        services.GetRequiredService<IHashService>(),
        services.GetRequiredService<ILogger<ClientGame>>());

    private static void Settle(Window window)
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Save(WriteableBitmap frame, string fileName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, fileName));
    }

}
