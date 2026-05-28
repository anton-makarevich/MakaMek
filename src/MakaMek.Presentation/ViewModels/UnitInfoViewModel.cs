using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Utils;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class UnitInfoViewModel : BaseViewModel, IResultProvider<object?>
{
    private readonly TaskCompletionSource<object?> _resultTaskCompletionSource = new();

    public Unit Unit { get; }
    public bool HasPilot { get; }

    public ICommand CloseCommand { get; }

    public UnitInfoViewModel(UnitData unitData, PilotData? pilotData, IMechFactory mechFactory)
    {
        Unit = mechFactory.Create(unitData);

        if (pilotData.HasValue)
        {
            var pilot = new MechWarrior(pilotData.Value);
            Unit.AssignPilot(pilot);
            HasPilot = true;
        }

        CloseCommand = new AsyncCommand(Close);
    }

    public Task<object?> GetResultAsync()
    {
        return _resultTaskCompletionSource.Task;
    }

    private Task Close()
    {
        _resultTaskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }
}
