using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Mech : Unit
{
    public int PossibleTorsoRotation { get; }
    
    public HexDirection? TorsoDirection=> _parts.OfType<Torso>().FirstOrDefault()?.Facing;

    public bool HasUsedTorsoTwist
    {
        get
        {
            if (Position == null) return false;
            var torsos = _parts.OfType<Torso>();
            return torsos.Any(t => t.Facing != Position.Facing);
        }
    }

    public Mech(
        string chassis,
        string model, 
        int tonnage, 
        int walkMp,
        IEnumerable<UnitPart> parts,
        int possibleTorsoRotation = 1,
        Guid? id = null) 
        : base(chassis, model, tonnage, walkMp, parts, id)
    {
        PossibleTorsoRotation = possibleTorsoRotation;
        Status = UnitStatus.Active;
        // Assign a default mechwarrior with a generated name
        var randomId = Guid.NewGuid().ToString()[..6];
        Crew = new MechWarrior($"MechWarrior", randomId);
    }

    public bool CanRotateTorso=> PossibleTorsoRotation > 0 && !HasUsedTorsoTwist;

    public void RotateTorso(HexDirection newFacing)
    {
        if (!CanRotateTorso)
            return;

        var currentUnitFacing = (int)Position!.Facing;
        var newFacingInt = (int)newFacing;
        
        // Calculate steps in both directions (clockwise and counterclockwise)
        var clockwiseSteps = (newFacingInt - currentUnitFacing + 6) % 6;
        var counterClockwiseSteps = (currentUnitFacing - newFacingInt + 6) % 6;
        
        // Use the smaller number of steps
        var steps = Math.Min(clockwiseSteps, counterClockwiseSteps);
        
        // Check if rotation is within allowed range
        if (steps > PossibleTorsoRotation) return;
        foreach (var torso in _parts.OfType<Torso>())
        {
            torso.Rotate(newFacing);
        }
    }

    public override PartLocation? GetTransferLocation(PartLocation location) => location switch
    {
        PartLocation.LeftArm => PartLocation.LeftTorso,
        PartLocation.RightArm => PartLocation.RightTorso,
        PartLocation.LeftLeg => PartLocation.LeftTorso,
        PartLocation.RightLeg => PartLocation.RightTorso,
        PartLocation.LeftTorso => PartLocation.CenterTorso,
        PartLocation.RightTorso => PartLocation.CenterTorso,
        _ => null
    };

    internal override void ApplyArmorAndStructureDamage(int damage, UnitPart targetPart)
    {
        base.ApplyArmorAndStructureDamage(damage, targetPart);
        var head = _parts.Find(p => p.Location == PartLocation.Head);
        if (head is { IsDestroyed: true })
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        var centerTorso = _parts.Find(p => p.Location == PartLocation.CenterTorso);
        if (centerTorso is { IsDestroyed: true })
        {
            Status = UnitStatus.Destroyed;
        }
    }

    protected override void ApplyHeatEffects()
    {
        // Apply effects based on current heat level
        if (CurrentHeat >= 30)
        {
            // Automatic shutdown
            Status = UnitStatus.Shutdown;
        }
        else if (CurrentHeat >= 25)
        {
            // Chance to shutdown, ammo explosion, etc.
            // To be implemented
        }
    }

    public override int CalculateBattleValue()
    {
        var bv = Tonnage * 100; // Base value
        bv += GetAllComponents<Weapon>().Sum(w => w.BattleValue);
        return bv;
    }

    public override bool CanMoveBackward(MovementType type) => type == MovementType.Walk;

    public void SetProne()
    {
        Status |= UnitStatus.Prone;
    }

    public void StandUp()
    {
        Status &= ~UnitStatus.Prone;
    }

    public override HexPosition? Position
    {
        get => base.Position;
        protected set
        {
            base.Position = value;
            // Reset torso rotation when position changes
            foreach (var torso in _parts.OfType<Torso>())
            {
                torso.ResetRotation();
            }
        }
    }
    
    /// <summary>
    /// Resets the turn state for the mech, including torso rotation
    /// </summary>
    public override void ResetTurnState()
    {
        base.ResetTurnState();
        
        // Reset torso rotation
        foreach (var torso in _parts.OfType<Torso>())
        {
            torso.ResetRotation();
        }
    }
}
