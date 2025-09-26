namespace Sanet.MakaMek.Core.Data.Units;

public interface IUnitDataProvider
{
    UnitData LoadMechFromTextData(IEnumerable<string> lines);
}