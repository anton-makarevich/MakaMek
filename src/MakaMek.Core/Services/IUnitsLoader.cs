using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Services;

public interface IUnitsLoader
{
    Task<List<UnitData>> LoadUnits();
}
