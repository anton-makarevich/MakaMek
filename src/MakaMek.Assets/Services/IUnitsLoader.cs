using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Assets.Services;

public interface IUnitsLoader
{
    Task<List<UnitData>> LoadUnits();
}
