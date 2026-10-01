using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Controls;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MVVM.Core.Views;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The squad bar and the inspection drawer, with a real game holding real units from the
/// repository's own data folder.
///
/// The other HUD tests run against a view model with no game, so everything driven by
/// <c>LocalUnits</c> renders as nothing. The squad bar is the densest part of this HUD and the part
/// most likely to be too much on a small screen, so it has to be rendered with units in it before
/// anyone can judge it.
/// </summary>
public class SquadHudTests
{
    [Fact]
    public Task SquadBar_RendersACardPerLocalUnit() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            viewModel.LocalUnits.Count().ShouldBe(4, "the fixture joins one player with four units");
            viewModel.IsSquadStatusBarVisible.ShouldBeTrue();

            var cards = view.GetVisualDescendants().OfType<UnitStatusBarItem>().ToList();
            cards.Count.ShouldBe(4, "every local unit should get a card");
            cards.ShouldAllBe(card => card.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task SquadCard_OpensTheDrawer_WithoutChangingTheSelection() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var unit = viewModel.LocalUnits.Last();
            viewModel.IsRecordSheetPanelVisible.ShouldBeFalse();

            viewModel.InspectUnit(unit);
            Settle(window);

            viewModel.InspectedUnit.ShouldBe(unit);
            viewModel.SelectedUnit.ShouldBeNull("inspecting must not change what the phase acts on");

            var sheet = view.GetVisualDescendants().OfType<UnitRecordSheet>().FirstOrDefault();
            sheet.ShouldNotBeNull();
            sheet.IsEffectivelyVisible.ShouldBeTrue("the drawer should be on screen");
            sheet.Unit.ShouldBe(unit, "and should be showing the inspected unit, not the selection");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task PinnedDrawer_SurvivesAPhaseStep_OnScreen() => HarnessSession.Run(async () =>
    {
        var (window, view, viewModel) = await ShowBattleMapWithSquad();
        try
        {
            var unit = viewModel.LocalUnits.First();
            viewModel.InspectUnit(unit);
            viewModel.ToggleRecordSheetPin();
            Settle(window);

            // The same call TransitionToState and ClearSelection make on every phase step.
            viewModel.NotifySelectedUnitChanged();
            Settle(window);

            var sheet = view.GetVisualDescendants().OfType<UnitRecordSheet>().FirstOrDefault();
            sheet.ShouldNotBeNull();
            sheet.IsEffectivelyVisible.ShouldBeTrue("a pinned drawer should still be up");
            sheet.Unit.ShouldBe(unit, "and should still be showing the unit that was pinned");
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task Screenshots_OfTheSquadHud() => HarnessSession.Run(async () =>
    {
        foreach (var (name, width, height) in new[]
                 {
                     ("squad-desktop.png", 1280, 800),
                     ("squad-narrow.png", 430, 880)
                 })
        {
            var (window, _, viewModel) = await ShowBattleMapWithSquad(width, height);
            try
            {
                await SettleUntilTilesDecode(window);
                using var bare = window.CaptureRenderedFrame();
                Save(bare!, name);

                viewModel.InspectUnit(viewModel.LocalUnits.First());
                Settle(window);
                using var withDrawer = window.CaptureRenderedFrame();
                Save(withDrawer!, name.Replace(".png", "-drawer.png"));
            }
            finally
            {
                window.Close();
            }
        }
    });

    private static async Task<(Window Window, BattleMapView View, BattleMapViewModel ViewModel)>
        ShowBattleMapWithSquad(int width = 1280, int height = 800, bool deploy = true)
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = services.GetRequiredService<BattleMapViewModel>();
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

        if (deploy)
        {
            // Undeployed units read as "Off map" and draw nothing, so the map underneath the HUD
            // would stay empty. Put each one on its own hex.
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
        }

        var view = new BattleMapView();
        ((IBaseView)view).ViewModel = viewModel;
        view.DataContext = viewModel;
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Settle(window);
        return (window, view, viewModel);
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

    /// <summary>
    /// Hex tiles are decoded off the UI thread, so a single settle captures the map before any of
    /// them arrive. Pump the dispatcher a few times, yielding in between, so the screenshot shows
    /// the terrain rather than bare canvas.
    /// </summary>
    private static async Task SettleUntilTilesDecode(Window window)
    {
        for (var i = 0; i < 25; i++)
        {
            Settle(window);
            await Task.Delay(40);
        }

        Settle(window);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(WriteableBitmap frame, string fileName)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, fileName));
    }




}
