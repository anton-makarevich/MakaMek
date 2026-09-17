using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

/// <summary>
/// Presents the small set of combat-relevant facts a player needs while choosing a
/// unit. Values come from the constructed unit so this preview cannot drift from the
/// rules used after the match starts.
/// </summary>
public sealed class UnitSelectionPreviewViewModel
{
    private readonly IUnit _unit;

    public UnitSelectionPreviewViewModel(UnitData unitData, IUnit unit)
    {
        UnitData = unitData;
        _unit = unit;
    }

    public UnitData UnitData { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(UnitData.Name)
        ? $"{UnitData.Chassis} {UnitData.Model}"
        : UnitData.Name!;
    public string WeightClass => _unit.Class.ToString();
    public string Era => UnitData.AdditionalAttributes is null
        ? "—"
        : UnitData.AdditionalAttributes.FirstOrDefault(attribute =>
              attribute.Key.Equals("era", StringComparison.OrdinalIgnoreCase)
              || attribute.Key.Equals("introduction-era", StringComparison.OrdinalIgnoreCase)).Value
          ?? "—";
    public int Tonnage => _unit.Tonnage;
    public int BattleValue => _unit.CalculateBattleValue();
    public int WalkingPoints => _unit.AvailableWalkingPoints;
    public int RunningPoints => _unit.AvailableRunningPoints;
    public int JumpingPoints => _unit.AvailableJumpingPoints;
    public string MovementSummary => $"{WalkingPoints} / {RunningPoints} / {JumpingPoints}";
    public int MaxArmor => _unit.TotalMaxArmor;
    public double ArmorPercent => MaxArmor == 0 ? 0 : 100;
    public string ArmorSummary => $"{MaxArmor} armor";
    public int MaxStructure => _unit.TotalMaxStructure;
    public double StructurePercent => MaxStructure == 0 ? 0 : 100;
    public string StructureSummary => $"{MaxStructure} structure";
    public int HeatDissipation => _unit.HeatDissipation;
    public string HeatSummary => $"{HeatDissipation} heat sinks";
    public int WeaponCount => _unit.GetAvailableWeapons().Count;
    public int TotalWeaponDamage => _unit.GetAvailableWeapons().Sum(weapon => weapon.Damage);
}
