using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
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

    public HexDirection? TorsoDirection => MechTorsoController.GetTorsoDirection(this);

    public bool HasUsedTorsoTwist => MechTorsoController.HasUsedTorsoTwist(this);

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

    public override int GetMovementPoints(MovementType type) =>
        MechMobilityRules.GetMovementPoints(this, type, ModifiedMovement);
    
    public override IReadOnlyList<MovementType> GetAvailableMovementTypes()
    {
        var types = new List<MovementType> { MovementType.Walk };
        if (CanRun) types.Add(MovementType.Run);
        if (CanJump) types.Add(MovementType.Jump);
        return types;
    }

    public override HexDirection? Facing => TorsoDirection?? Position?.Facing;

    public bool CanRotateTorso=> PossibleTorsoRotation > 0 && !HasUsedTorsoTwist;

    public void RotateTorso(HexDirection newFacing) => MechTorsoController.RotateTorso(this, newFacing);

    /// <summary>
    /// Determines if a weapon configuration is currently applied to this mech
    /// </summary>
    /// <param name="config">The weapon configuration to check</param>
    /// <returns>True if the configuration is applied, false otherwise</returns>
    public override bool IsWeaponConfigurationApplied(WeaponConfiguration config) =>
        MechWeaponConfigurationApplier.IsApplied(this, config);

    public override void UpdateDestroyedStatus()
    {
        if (MechStructureRules.IsPilotLost(this))
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        if (MechStructureRules.IsHeadDestroyed(this))
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        if (MechStructureRules.IsCenterTorsoDestroyed(this))
        {
            Status = UnitStatus.Destroyed;
        }

        MechStructureRules.BlowOffArmOnTorsoDestruction(this, PartLocation.LeftTorso, PartLocation.LeftArm);
        MechStructureRules.BlowOffArmOnTorsoDestruction(this, PartLocation.RightTorso, PartLocation.RightArm);
    }

    public override PartLocation? GetTransferLocation(PartLocation location) =>
        MechStructureRules.GetTransferLocation(location);

    public override void ApplyWeaponConfiguration(WeaponConfiguration weaponConfiguration) =>
        MechWeaponConfigurationApplier.Apply(this, weaponConfiguration);
    
    public override int EngineHeatSinks => MechHeatEffects.GetEngineHeatSinks(this);

    protected override int GetWaterHeatDissipationBonus(IRulesProvider rulesProvider) =>
        MechHeatEffects.GetWaterHeatDissipationBonus(this, rulesProvider);

    /// <summary>
    /// Gets the heat penalty caused by engine damage
    /// </summary>
    public override EngineHeatPenalty? EngineHeatPenalty => MechHeatEffects.GetEngineHeatPenalty(this);

    /// <summary>
    /// Determines if this Mech can fire weapons. Returns false if sensors are destroyed (2 critical hits).
    /// </summary>
    public override bool CanFireWeapons =>
        base.CanFireWeapons && MechStructureRules.AreSensorsAvailable(this);

    protected override void ApplyHeatEffects() => MechHeatEffects.ApplyHeatEffects(this);
    
    // Calculate movement penalty based on current heat
    public override HeatMovementPenalty? MovementHeatPenalty => MechHeatEffects.GetMovementHeatPenalty(CurrentHeat);

    public override int DamageReducedMovement => MechMovementModifierCalculator.GetDamageReducedMovement(this, BaseMovement);
    
    // Calculate attack penalty based on current heat
    public override HeatRollModifier? AttackHeatPenalty => MechHeatEffects.GetAttackHeatPenalty(CurrentHeat);

    /// <summary>
    /// Gets all movement penalties currently affecting this mech as a property for UI binding
    /// </summary>
    public override IReadOnlyList<RollModifier> MovementModifiers => MechMovementModifierCalculator.Calculate(this, BaseMovement);

    /// <summary>
    /// Gets all attack penalties currently affecting this mech
    /// </summary>
    public override IReadOnlyList<RollModifier> GetAttackModifiers(PartLocation location) =>
        MechAttackModifierCalculator.Calculate(this, location);

    public override int CalculateBattleValue() => MechStructureRules.CalculateBattleValue(this);

    public override bool CanMoveBackward(MovementType type) => type == MovementType.Walk;
    
    public bool CanJump => MechMobilityRules.CanJump(this);
    
    public bool CanRun => MechMobilityRules.CanRun(this);
    
    public bool CanStandup() => MechMobilityRules.CanStandup(this);

    /// <summary>
    /// Determines if a piloting skill roll is required for jumping due to damage
    /// </summary>
    public bool IsPsrForJumpRequired() => MechMobilityRules.IsPsrForJumpRequired(this);

    public void SetProne(HexDirection? facing = null)
    {
        if (facing.HasValue && Position != null) Position = Position with { Facing = facing.Value };
        Status |= UnitStatus.Prone;
    }

    private bool IsGyroAvailable => MechMobilityRules.IsGyroAvailable(this);

    /// <summary>
    /// Determines if the mech can change its facing while prone
    /// </summary>
    public bool CanChangeFacingWhileProne() => MechMobilityRules.CanChangeFacingWhileProne(this);

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
        MovementTaken ??= MechMovementTags.CreateSingleSegmentPath(Position, movementType.Value);
        MovementTaken = MechMovementTags.TagStandupAttempt(MovementTaken, standupCost);
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

        MovementTaken = MechMovementTags.TagFall(MovementTaken);
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
            MechTorsoController.ResetTorsos(this);
        }
    }

    /// <summary>
    /// Determines if the mech is immobile based on the rules:
    /// </summary>
    public override bool IsImmobile => MechStructureRules.IsImmobile(this);

    public bool IsProne => (Status & UnitStatus.Prone) == UnitStatus.Prone;

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
        MechTorsoController.ResetTorsos(this);
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
        MechCriticalHitCalculator.Calculate(this, location, diceRoller, damageTransferCalculator);

    /// <summary>
    /// Determines the number of critical hits based on the roll
    /// </summary>
    /// <param name="roll">The 2d6 roll result</param>
    /// <returns>Number of critical hits (0-3)</returns>
    internal int GetNumCriticalHits(int roll) => MechCriticalHitCalculator.GetNumCriticalHits(roll);
}

