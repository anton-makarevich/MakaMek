using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Movement;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Mech : Unit
{
    private const int StandupCost = 2;

    public int StandupAttempts { private set; get; }

    /// <summary>
    /// The actual cost of the next standup attempt, accounting for the minimum movement exception
    /// </summary>
    public int EffectiveStandupCost =>
        IsMinimumMovement 
        && StandupAttempts == 0 
            ? 1 
            : StandupCost;
    public int PossibleTorsoRotation { get; }
    public bool CanFlipArms { get; }

    public HexDirection? TorsoDirection => this.TorsoFacing;

    public bool HasUsedTorsoTwist => this.HasTorsoTwist;

    public Mech(
        string chassis,
        string model,
        int tonnage,
        IEnumerable<UnitPart> parts,
        int possibleTorsoRotation = 1,
        bool canFlipArms = false,
        Guid? id = null,
        string? name = null)
        : base(chassis, model, tonnage, parts, id, name)
    {
        PossibleTorsoRotation = possibleTorsoRotation;
        CanFlipArms = canFlipArms;
        Status = UnitStatus.Active;
    }

    public override int GetMovementPoints(MovementType _) =>
        this.GetMovementPointsFor(_, ModifiedMovement);
    
    public override IReadOnlyList<MovementType> GetAvailableMovementTypes()
    {
        var types = new List<MovementType> { MovementType.Walk };
        if (CanRun) types.Add(MovementType.Run);
        if (CanJump) types.Add(MovementType.Jump);
        return types;
    }

    public override HexDirection? Facing => TorsoDirection?? Position?.Facing;

    public bool CanRotateTorso=> PossibleTorsoRotation > 0 && !HasUsedTorsoTwist;

    public void RotateTorso(HexDirection newFacing) => this.RotateTorsosTo(newFacing);

    /// <summary>
    /// Determines if a weapon configuration is currently applied to this mech
    /// </summary>
    /// <param name="config">The weapon configuration to check</param>
    /// <returns>True if the configuration is applied, false otherwise</returns>
    public override bool IsWeaponConfigurationApplied(WeaponConfiguration config) =>
        this.IsConfigurationApplied(config);

    public override void UpdateDestroyedStatus()
    {
        if (this.IsPilotLost)
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        if (this.IsHeadDestroyed)
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        if (this.IsCenterTorsoDestroyed)
        {
            Status = UnitStatus.Destroyed;
        }

        this.BlowOffArmOnTorsoDestruction(PartLocation.LeftTorso, PartLocation.LeftArm);
        this.BlowOffArmOnTorsoDestruction(PartLocation.RightTorso, PartLocation.RightArm);
    }

    public override PartLocation? GetTransferLocation(PartLocation location) =>
        location.GetDamageTransferLocation();

    public override void ApplyWeaponConfiguration(WeaponConfiguration config) =>
        this.ApplyConfiguration(config);
    
    public override int EngineHeatSinks => this.GetEngineHeatSinkCount();

    protected override int GetWaterHeatDissipationBonus(IRulesProvider rulesProvider) =>
        this.CalculateWaterHeatDissipationBonus(rulesProvider);

    /// <summary>
    /// Gets the heat penalty caused by engine damage
    /// </summary>
    public override EngineHeatPenalty? EngineHeatPenalty => this.GetEngineHeatPenalty();

    /// <summary>
    /// Determines if this Mech can fire weapons. Returns false if sensors are destroyed (2 critical hits).
    /// </summary>
    public override bool CanFireWeapons => base.CanFireWeapons && this.AreSensorsAvailable;

    protected override void ApplyHeatEffects() => this.ApplyHeatDamage();
    
    // Calculate movement penalty based on current heat
    public override HeatMovementPenalty? MovementHeatPenalty => MechHeatExtensions.GetMovementHeatPenalty(CurrentHeat);

    public override int DamageReducedMovement => this.GetDamageReducedMovementValue(BaseMovement);
    
    // Calculate attack penalty based on current heat
    public override HeatRollModifier? AttackHeatPenalty => MechHeatExtensions.GetAttackHeatPenalty(CurrentHeat);

    /// <summary>
    /// Gets all movement penalties currently affecting this mech as a property for UI binding
    /// </summary>
    public override IReadOnlyList<RollModifier> MovementModifiers => this.CalculateMovementModifiers(BaseMovement);

    /// <summary>
    /// Gets all attack penalties currently affecting this mech
    /// </summary>
    public override IReadOnlyList<RollModifier> GetAttackModifiers(PartLocation location) =>
        this.CalculateAttackModifiers(location);

    public override int CalculateBattleValue() => this.GetBattleValue();

    public override bool CanMoveBackward(MovementType type) => type == MovementType.Walk;
    
    public bool CanJump => this.IsJumpPossible;
    
    public bool CanRun => this.IsRunPossible;
    
    public bool CanStandup() => this.IsStandUpPossible();

    /// <summary>
    /// Determines if a piloting skill roll is required for jumping due to damage
    /// </summary>
    public bool IsPsrForJumpRequired() => this.RequiresJumpPsr();

    public void SetProne(HexDirection? facing = null)
    {
        if (facing.HasValue && Position != null) Position = Position with { Facing = facing.Value };
        Status |= UnitStatus.Prone;
    }

    /// <summary>
    /// Determines if the mech can change its facing while prone
    /// </summary>
    public bool CanChangeFacingWhileProne() => this.IsFacingChangeWhilePronePossible();

    public void StandUp(HexDirection newFacing)
    {
        Status &= ~UnitStatus.Prone;

        // update the mech's facing
        if (Position != null) Position = Position with { Facing = newFacing };
    }

    public void RegisterStandupAttempt(MovementType? movementType = null)
    {
        // Must be read before StandupAttempts is incremented
        var standupCost = EffectiveStandupCost;
        StandupAttempts++;
        movementType ??= MovementTaken?.MovementType;
        if (Position == null || movementType == null) return; 
        MovementTaken = (MovementTaken
                ?? MovementPath.CreateSingleSegmentPath(Position, movementType.Value))
            .TagStandupAttempt(standupCost);
    }

    public void ApplyFall(MechFallCommand fallCommand)
    {
        if (fallCommand.DamageData is not { HitLocations.HitLocations: var hits }) return;
        ApplyDamage(hits, fallCommand.DamageData.FallDirection);
        SetProne(fallCommand.DamageData.FacingAfterFall);
        if (fallCommand.IsPilotTakingDamage)
        {
            Pilot?.Hit();
        }

        MovementTaken = MovementTaken?.TagFall();
    }

    public void ApplySkid(MechSkidCommand skidCommand)
    {
        if (skidCommand.DamageData is not { HitLocations.HitLocations: var hits }) return;
        ApplyDamage(hits, skidCommand.DamageData.FallDirection);
    }
    
    
    public override HexPosition? Position
    {
        get => base.Position;
        protected set
        {
            base.Position = value;
            // Reset torso rotation when the position changes
            this.ResetTorsos();
        }
    }

    /// <summary>
    /// Determines if the mech is immobile based on the rules:
    /// </summary>
    public override bool IsImmobile => this.IsMechImmobile;

    /// <summary>
    /// Gets whether the Mech is prone. Only a Mech can be prone, so this is the only override of
    /// <see cref="Unit.IsProne"/>.
    /// </summary>
    public override bool IsProne => (Status & UnitStatus.Prone) == UnitStatus.Prone;

    /// <summary>
    /// Gets the mech's height in levels. Standing mechs are height 2, prone mechs are height 1.
    /// Used for line of sight calculations. A pending DFA attacker stays airborne until the
    /// attack resolves, so it keeps the standing height even if a weapon-phase fall made it
    /// prone: LOS adds the standing height on top of the attacker's two-level mid-air base, and a
    /// fall does not cancel the DFA (see docs/rules/physical-attacks.md).
    /// </summary>
    public override int Height => IsProne && Position?.AirborneLevel is null ? 1 : 2;

    /// <summary>
    /// BattleMechs can change elevation by up to 2 levels per hex when moving forward.
    /// </summary>
    public override int MaxLevelChangeForward => 2;

    /// <summary>
    /// BattleMechs cannot change elevation when moving backward.
    /// </summary>
    public override int MaxLevelChangeBackward => 0;

    /// <summary>
    /// Determines if this mech can have partial cover from elevation.
    /// Only a standing, grounded mech can have partial cover: a prone mech or one that is
    /// airborne (for example a DFA attacker waiting in its approach hex) is disqualified
    /// (Total Warfare, p. 102 &amp; p. 106).
    /// </summary>
    public override bool CanHavePartialCover => !IsProne && Position?.AirborneLevel is null;

    /// <summary>
    /// Resets the turn state for the mech, including torso rotation
    /// </summary>
    public override void ResetTurnState()
    {
        base.ResetTurnState();

        StandupAttempts = 0;

        // Reset torso rotation
        this.ResetTorsos();
    }

    /// <summary>
    /// Calculates critical hit data for a specific location and damage
    /// </summary>
    /// <param name="location">The hit location</param>
    /// <param name="diceRoller">The dice roller to use for critical hit determination</param>
    /// <param name="damageTransferCalculator">Damage transfer calculator to calculate damage distribution</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    public override LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator) =>
        this.ResolveCriticalHits(location, diceRoller, damageTransferCalculator);
}

