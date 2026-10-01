using Avalonia.Controls;
using Avalonia.Headless;
using Sanet.MakaMek.Avalonia.Views.StartNewGame.Fragments;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Views;

public class PlayersFragmentTests
{
    /// <summary>
    /// NewGameViewModel.BattleValueLimit shipped with nothing to set it, so the budget defaulted to
    /// unrestricted and no player could change it. The binding path itself is checked at build time
    /// because the fragment declares x:DataType; this is here so deleting the control fails rather
    /// than quietly stranding the property again.
    /// </summary>
    [Fact]
    public async Task BattleValueBudget_HasAnInput_ThatCanBeReturnedToUnrestricted()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(typeof(PlayersFragmentTests).Assembly);

        await session.Dispatch(() =>
        {
            var input = new PlayersFragment().FindControl<NumericUpDown>("BattleValueBudgetInput");

            input.ShouldNotBeNull();
            input.Minimum.ShouldBe(0, "zero is what turns the budget off, so it has to be reachable");
        }, CancellationToken.None);
    }
}
