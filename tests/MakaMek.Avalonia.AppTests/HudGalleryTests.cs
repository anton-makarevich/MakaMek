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
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Data;
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
                var (window, view, viewModel, localPlayerId) = await ShowHud(width, height, mobile);
                try
                {
                    await state.Apply(viewModel, localPlayerId);
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
        Add("record sheet", Rendered(view.GetVisualDescendants().OfType<GamePanel>()
            .FirstOrDefault(p => p.Name == "RecordSheetPanel")));
        Add("turn status", view.GetVisualDescendants().OfType<Grid>()
            .FirstOrDefault(g => g.Name == "TurnStatus"));
        Add("drawer toggle", view.GetVisualDescendants().OfType<ActionButton>()
            .FirstOrDefault(a => a.Name == "MapControlsToggle"));

        // The utility panels are mutually exclusive with the record sheet by design, so a clash
        // here means that contract has come apart rather than the layout being tight.
        foreach (var panel in view.GetVisualDescendants().OfType<GamePanel>()
                     .Where(p => p.Name != "RecordSheetPanel"))
            Add($"panel '{panel.Title}'", Rendered(panel));

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

    /// <summary>
    /// What a GamePanel actually puts on screen. The control itself is a ContentControl with no
    /// size of its own, so it stretches to the window while its template draws a bordered card in
    /// one corner. Measuring the control makes it look like it covers everything.
    /// </summary>
    private static Visual? Rendered(GamePanel? panel) =>
        panel?.GetVisualDescendants().OfType<Border>().FirstOrDefault();

    private static bool IsRelated(Visual a, Visual b) =>
        a.GetVisualAncestors().Contains(b) || b.GetVisualAncestors().Contains(a);

    private sealed record GalleryState(string Name, Func<BattleMapViewModel, Guid, Task> Apply);

    private static readonly GalleryState[] GalleryStates =
    [
        new("base", (_, _) => Task.CompletedTask),
        new("acting", (viewModel, _) => { SetState(viewModel, ActingState()); return Task.CompletedTask; }),
        new("controls-open", (viewModel, _) => { viewModel.ToggleMapControlsDrawer(); return Task.CompletedTask; }),
        new("record-sheet", (viewModel, _) => { viewModel.InspectUnit(viewModel.LocalUnits.First()); return Task.CompletedTask; }),
        new("record-sheet-pinned", (viewModel, _) =>
        {
            viewModel.InspectUnit(viewModel.LocalUnits.First());
            viewModel.ToggleRecordSheetPin();
            return Task.CompletedTask;
        }),
        new("command-log", (viewModel, _) => { viewModel.ToggleCommandLog(); return Task.CompletedTask; }),
        new("map-settings", (viewModel, _) => { viewModel.ToggleMapSettings(); return Task.CompletedTask; }),
        new("acting-with-controls", (viewModel, _) =>
        {
            SetState(viewModel, ActingState());
            viewModel.ToggleMapControlsDrawer();
            return Task.CompletedTask;
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

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel, Guid LocalPlayerId)>
        ShowHud(int width, int height, bool mobile)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = mobile ? MobileViewModel(services) : services.GetRequiredService<BattleMapViewModel>();

        var game = CreateClientGame(services);
        var units = await LocalGameFixture.LoadBundledUnitsAsync(services);
        var player = new Player(Guid.NewGuid(), "Local", PlayerControlType.Human, "#4A90D9");

        // An opponent, so the weapon selection panel has something to target. Its units are
        // deployed a couple of hexes away, inside the range of anything the attacker carries.
        var enemy = new Player(Guid.NewGuid(), "Opponent", PlayerControlType.Remote, "#C0504D");

        viewModel.Game = game;
        game.SetBattleMap(services.GetRequiredService<IBattleMapFactory>()
            .GenerateMap(12, 10, new SingleTerrainGenerator(12, 10, new ClearTerrain())));

        // Bundled unit data carries no id until one is assigned, and the pilot assignments below
        // have to name the same units the roster does.
        List<UnitData> Roster(int skip, int take) => units
            .Skip(skip).Take(take)
            .Select(unit => unit with { Id = Guid.NewGuid() })
            .ToList();

        // A mech with no pilot is immobile and cannot fire, which leaves the whole squad greyed
        // out and the weapon panel unreachable.
        List<PilotAssignmentData> Pilots(List<UnitData> roster) => roster
            .Select(unit => new PilotAssignmentData
            {
                UnitId = unit.Id!.Value,
                PilotData = PilotData.CreateDefaultPilot("Test", "Pilot")
            })
            .ToList();

        var localRoster = Roster(0, 4);
        var enemyRoster = Roster(4, 2);

        // JoinGameWithUnits is what marks the player as local, which is what the squad bar reads.
        await game.JoinGameWithUnits(player, localRoster, Pilots(localRoster));

        foreach (var (joining, roster) in new[] { (player, localRoster), (enemy, enemyRoster) })
        {
            game.HandleCommand(new JoinGameCommand
            {
                GameOriginId = Guid.NewGuid(),
                PlayerId = joining.Id,
                PlayerName = joining.Name,
                Tint = joining.Tint,
                Units = roster,
                PilotAssignments = Pilots(roster)
            });
        }

        // Facing north in a row, with the opponent directly ahead: a target off the firing arc is
        // not selectable, so the weapon panel would never open.
        Deploy(game, player.Id, startColumn: 3, row: 6);
        Deploy(game, enemy.Id, startColumn: 3, row: 3);

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);
        return (window, view, viewModel, player.Id);
    }

    private static void Deploy(IClientGame game, Guid playerId, int startColumn, int row)
    {
        var column = startColumn;
        foreach (var unit in game.Players.First(p => p.Id == playerId).Units)
        {
            game.HandleCommand(new DeployUnitCommand
            {
                GameOriginId = Guid.NewGuid(),
                PlayerId = playerId,
                UnitId = unit.Id,
                Position = new HexCoordinates(column, row).ToData(),
                Direction = 0
            });
            column++;
        }
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
            terrainBitmaskService: services.GetService<ITerrainBitmaskService>(),
            commandPublisher: services.GetService<ICommandPublisher>(),
            connectionLogger: services.GetRequiredService<ILoggerFactory>().CreateLogger<BattleMapViewModel>());
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
        services.GetRequiredService<ILogger<ClientGame>>(),
        // Nothing acknowledges a published command here, and a pending one blocks
        // CanActivePlayerAct, which gates every selection the HUD depends on. A short timeout lets
        // it clear instead.
        ackTimeoutMilliseconds: 20);

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





    /// <summary>Records a unit as having held its ground, so to-hit has a movement to read.</summary>
    private static void StandStill(IClientGame game, Guid playerId, IUnit unit)
    {
        var position = unit.Position!;
        game.HandleCommand(new MoveUnitCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerId,
            UnitId = unit.Id,
            MovementType = MovementType.StandingStill,
            IsCompleted = true,
            MovementPath =
            [
                new PathSegmentData
                {
                    From = position.ToData(),
                    To = position.ToData(),
                    Costs = []
                }
            ]
        });
    }

    /// <summary>
    /// Lets the deferred command handlers run.
    ///
    /// Commands reach the subscription from off the UI thread, so draining the dispatcher is not
    /// enough on its own and sleeping on it blocks the delivery being waited for. Awaiting yields
    /// the thread, which is what lets them arrive.
    /// </summary>
    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();
    }
}
