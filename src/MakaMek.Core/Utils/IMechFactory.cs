using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Utils;

public interface IMechFactory
{
    Mech Create(UnitData unitData);
}