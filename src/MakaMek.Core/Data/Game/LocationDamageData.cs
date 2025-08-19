using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Game;

public record LocationDamageData(
    PartLocation Location,
    int ArmorDamage,
    int StructureDamage,
    bool IsLocationDestroyed
);