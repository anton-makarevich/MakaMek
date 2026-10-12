using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The turn info panel sets its text colour once, on the panel, and every label in it inherits
/// that. A text style that sets a Foreground of its own would silently win over the inherited
/// value and leave that label unreadable on some player tints.
/// </summary>
public class TurnInfoPanelTests
{
    [Fact]
    public Task EveryLabel_TakesItsColour_FromThePanel() => HarnessSession.Run(() =>
    {
        var services = ((App)Application.Current!).ServiceProvider!;
        var viewModel = ActivatorUtilities.CreateInstance<BattleMapViewModel>(
            services, new TestDispatcherService());
        var view = new BattleMapView { DataContext = viewModel };
        var window = new Window { Width = 1100, Height = 700, Content = view };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();

            var panel = view.FindControl<Grid>("TurnInfoPanel");
            panel.ShouldNotBeNull();
            var panelForeground = TextElement.GetForeground(panel);
            panelForeground.ShouldNotBeNull("the panel should carry the contrasting colour for the active player's tint");
            panel.IsSet(TextElement.ForegroundProperty).ShouldBeTrue();

            var labels = panel.GetVisualDescendants().OfType<TextBlock>().ToList();
            labels.Count.ShouldBeGreaterThanOrEqualTo(6);
            foreach (var label in labels)
            {
                label.IsSet(TextBlock.ForegroundProperty).ShouldBeFalse(
                    $"'{label.Text}' sets its own Foreground and no longer follows the panel");
                label.Foreground.ShouldBeSameAs(panelForeground);
            }
        }
        finally
        {
            window.Close();
        }
    });
}
