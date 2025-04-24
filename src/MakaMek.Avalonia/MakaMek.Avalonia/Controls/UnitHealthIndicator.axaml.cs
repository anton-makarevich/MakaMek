using Avalonia;
using Avalonia.Controls;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Avalonia.Controls
{
    public partial class UnitHealthIndicator : UserControl
    {
        public UnitHealthIndicator()
        {
            InitializeComponent();
        }

        public static readonly DirectProperty<UnitHealthIndicator, Unit?> UnitProperty =
            AvaloniaProperty.RegisterDirect<UnitHealthIndicator, Unit?>(
                nameof(Unit),
                o => o.Unit,
                (o, v) => o.Unit = v);

        private Unit? _unit;
        public Unit? Unit
        {
            get => _unit;
            set
            {
                SetAndRaise(UnitProperty, ref _unit, value);
                UpdateHealthBars();
            }
        }

        private void UpdateHealthBars()
        {
            if (Unit == null)
            {
                ClearHealthBars();
                return;
            }

            foreach (var part in Unit.Parts)
            {
                switch (part.Location)
                {
                    case PartLocation.Head:
                        UpdateBar(HeadArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(HeadStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        break;
                    case PartLocation.CenterTorso:
                        UpdateBar(CenterTorsoArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(CenterTorsoStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        if (part is Torso torso)
                        {
                            UpdateBar(CenterTorsoRearArmor, torso.CurrentRearArmor, torso.MaxRearArmor, part.IsDestroyed);
                        }
                        break;
                    case PartLocation.LeftTorso:
                        UpdateBar(LeftTorsoArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(LeftTorsoStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        if (part is Torso torsoLT)
                        {
                            UpdateBar(LeftTorsoRearArmor, torsoLT.CurrentRearArmor, torsoLT.MaxRearArmor, part.IsDestroyed);
                        }
                        break;
                    case PartLocation.RightTorso:
                        UpdateBar(RightTorsoArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(RightTorsoStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        if (part is Torso torsoRT)
                        {
                            UpdateBar(RightTorsoRearArmor, torsoRT.CurrentRearArmor, torsoRT.MaxRearArmor, part.IsDestroyed);
                        }
                        break;
                    case PartLocation.LeftArm:
                        UpdateBar(LeftArmArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(LeftArmStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        break;
                    case PartLocation.RightArm:
                        UpdateBar(RightArmArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(RightArmStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        break;
                    case PartLocation.LeftLeg:
                        UpdateBar(LeftLegArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(LeftLegStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        break;
                    case PartLocation.RightLeg:
                        UpdateBar(RightLegArmor, part.CurrentArmor, part.MaxArmor, part.IsDestroyed);
                        UpdateBar(RightLegStructure, part.CurrentStructure, part.MaxStructure, part.IsDestroyed);
                        break;
                }
            }
        }

        private void UpdateBar(ProgressBar? bar, int current, int max, bool isDestroyed = false)
        {
            if (bar == null) return;
            bar.Value = current;
            bar.Maximum = max;
            
            // Add or remove the destroyed class based on part status
            if (isDestroyed)
            {
                bar.Classes.Add("destroyed");
            }
            else
            {
                bar.Classes.Remove("destroyed");
            }
        }

        private void ClearHealthBars()
        {
            // Head
            UpdateBar(HeadArmor, 0, 1, false);
            UpdateBar(HeadStructure, 0, 1, false);

            // Center Torso
            UpdateBar(CenterTorsoArmor, 0, 1, false);
            UpdateBar(CenterTorsoStructure, 0, 1, false);
            UpdateBar(CenterTorsoRearArmor, 0, 1, false);

            // Left Torso
            UpdateBar(LeftTorsoArmor, 0, 1, false);
            UpdateBar(LeftTorsoStructure, 0, 1, false);
            UpdateBar(LeftTorsoRearArmor, 0, 1, false);

            // Right Torso
            UpdateBar(RightTorsoArmor, 0, 1, false);
            UpdateBar(RightTorsoStructure, 0, 1, false);
            UpdateBar(RightTorsoRearArmor, 0, 1, false);

            // Left Arm
            UpdateBar(LeftArmArmor, 0, 1, false);
            UpdateBar(LeftArmStructure, 0, 1, false);

            // Right Arm
            UpdateBar(RightArmArmor, 0, 1, false);
            UpdateBar(RightArmStructure, 0, 1, false);

            // Left Leg
            UpdateBar(LeftLegArmor, 0, 1, false);
            UpdateBar(LeftLegStructure, 0, 1, false);

            // Right Leg
            UpdateBar(RightLegArmor, 0, 1, false);
            UpdateBar(RightLegStructure, 0, 1, false);
        }
    }
}
