using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Components;

public class JumpJets : Component
{
    public static readonly EquipmentDefinition Definition = new(
        "Jump Jets",
        MakaMekComponent.JumpJet); // Removable

    public JumpJets(ComponentData? componentData = null)
        : base(Definition, componentData)
    {
        // Each jump jet always provides exactly 1 MP as per BattleTech rules
        JumpMp = 1;
    }

    public int JumpMp { get; }

    public override bool IsAvailable
    {
        get
        {
            if (!base.IsAvailable) return false;
            var hex = MountedOn.FirstOrDefault()?.Unit?.Hex;
            var waterDepth = hex?.GetWaterDepth();
            if (waterDepth is null) return true;
            if (waterDepth >= 2) return false;
            if (waterDepth == 1 && MountedOn.Any(p => p.Location.IsLeg())) return false;
            return true;
        }
    }
}