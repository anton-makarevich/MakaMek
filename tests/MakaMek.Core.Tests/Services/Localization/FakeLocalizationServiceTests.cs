using Shouldly;
using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Tests.Services.Localization;

public class FakeLocalizationServiceTests
{
    [Theory]
    [InlineData("Command_JoinGame", "{0} has joined game with {1} units")]
    [InlineData("Command_MoveUnit", "{0} moved {1} to {2} facing {3} using {4}")]
    [InlineData("Command_DeployUnit", "{0} deployed {1} to {2} facing {3}")]
    [InlineData("Command_TryStandup", "{0} attempts to stand up {1}")]
    [InlineData("Command_MechStandup", "{0} Mech stood up successfully. {1}")]
    [InlineData("Command_RollDice", "{0} rolls")]
    [InlineData("Command_DiceRolled", "{0} rolled {1}")]
    [InlineData("Command_UpdatePlayerStatus", "{0}'s status is {1}")]
    [InlineData("Command_ChangePhase", "Game changed phase to {0}")]
    [InlineData("Command_ChangeActivePlayer", "{0}'s turn")]
    [InlineData("Command_ChangeActivePlayerUnits", "{0}'s turn to play {1} units")]
    [InlineData("Command_TurnEnded", "{0} has ended their turn")]
    [InlineData("Command_ShutdownUnit", "{0} requests to shut down {1}")]
    [InlineData("Command_StartupUnit", "{0} requests to start up {1}")]
    [InlineData("Command_TurnIncremented", "Turn {0} has started")]
    [InlineData("Command_RequestGameLobbyStatus", "Client {0} requested game lobby status for game")]
    [InlineData("Command_SetBattleMap", "Battle map has been set")]
    public void GetString_BasicCommands_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Command_WeaponConfiguration_TorsoRotation", "{0}'s {1} rotates torso to face {2}")]
    [InlineData("Command_WeaponConfiguration_ArmsFlip", "{0}'s {1} flips arms {2}")]
    [InlineData("Command_WeaponAttackDeclaration_NoAttacks", "{0}'s {1} declares no attacks")]
    [InlineData("Command_WeaponAttackDeclaration_Header", "{0}'s {1} declares attacks:")]
    [InlineData("Command_WeaponAttackDeclaration_WeaponLine", "- {0} targeting {1}'s {2}")]
    public void GetString_WeaponCommands_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Command_WeaponAttackResolution", "{0}'s {1} attacks with {2} targeting {3}'s {4}. To hit number: {5}")]
    [InlineData("Command_WeaponAttackResolution_Hit", "{0}'s {1} hits {3}'s {4} with {2} (Target: {5}, Roll: {6})")]
    [InlineData("Command_WeaponAttackResolution_Miss", "{0}'s {1} misses {3}'s {4} with {2} (Target: {5}, Roll: {6})")]
    [InlineData("Command_WeaponAttackResolution_Direction", "Attack Direction: {0}")]
    [InlineData("Command_WeaponAttackResolution_TotalDamage", "Total Damage: {0}")]
    [InlineData("Command_WeaponAttackResolution_MissilesHit", "Missiles Hit: {0}")]
    [InlineData("Command_WeaponAttackResolution_ClusterRoll", "Cluster Roll: {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocations", "Hit Locations:")]
    [InlineData("Command_WeaponAttackResolution_HitLocation", "{0} (Roll: {2}): {1} damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocation_ArmorAndStructure", "{0} (Roll: {3}): {1} armor, {2} structure damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocation_ArmorOnly", "{0} (Roll: {2}): {1} armor damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocation_StructureOnly", "{0} (Roll: {2}): {1} structure damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocationTransfer_ArmorAndStructure", "{0} (Roll: {4}) → {1}: {2} armor, {3} structure damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocationTransfer_ArmorOnly", "{0} (Roll: {3}) → {1}: {2} armor damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocationTransfer_StructureOnly", "{0} (Roll: {3}) → {1}: {2} structure damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocationTransfer", "{0} (Roll: {3}) → {1}: {2} damage")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage", "  Excess damage {1} transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorAndStructure", "  Excess damage {1} armor, {2} structure transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorOnly", "  Excess damage {1} armor transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_StructureOnly", "  Excess damage {1} structure transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_AimedShotSuccessful", "Aimed Shot targeting {0} succeeded, Roll: {1}")]
    [InlineData("Command_WeaponAttackResolution_AimedShotFailed", "Aimed Shot targeting {0} failed, Roll: {1}")]
    [InlineData("Command_WeaponAttackResolution_DestroyedParts", "Destroyed parts:")]
    [InlineData("Command_WeaponAttackResolution_DestroyedPart", "- {0} destroyed")]
    [InlineData("Command_WeaponAttackResolution_UnitDestroyed", "{0} has been destroyed!")]
    public void GetString_WeaponAttackResolution_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Command_CriticalHitsResolution_Location", "Critical hits in {0}:")]
    [InlineData("Command_CriticalHitsResolution_CritRoll", "Critical Roll: {0}")]
    [InlineData("Command_CriticalHitsResolution_BlownOff", "Critical hit in {0}, location blown off")]
    [InlineData("Command_CriticalHitsResolution_NumCrits", "Number of critical hits: {0}")]
    [InlineData("Command_CriticalHitsResolution_CriticalHit", "Critical hit in slot {0}: {1}")]
    [InlineData("Command_CriticalHitsResolution_Explosion", "{0} exploded, damage: {1}")]
    public void GetString_CriticalHitsResolution_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Direction_Forward", "forward")]
    [InlineData("Direction_Backward", "backward")]
    // MechFallingCommand strings
    [InlineData("Command_MechFalling_Base", "{0} fell")]
    [InlineData("Command_MechFalling_Levels", " {0} level(s)")]
    [InlineData("Command_MechFalling_Jumping", " while jumping")]
    [InlineData("Command_MechFalling_Damage", " and took {0} damage")]
    [InlineData("Command_MechFalling_PilotInjury", "Pilot was injured")]
    // Piloting Skill Roll Command
    [InlineData("Command_PilotingSkillRoll_Success", "{0} roll succeeded")]
    [InlineData("Command_PilotingSkillRoll_Failure", "{0} roll failed")]
    [InlineData("Command_PilotingSkillRoll_ImpossibleRoll", "{0} roll is impossible")]
    [InlineData("Command_PilotingSkillRoll_BasePilotingSkill", "Base Piloting Skill: {0}")]
    [InlineData("Command_PilotingSkillRoll_Modifiers", "Modifiers:")]
    [InlineData("Command_PilotingSkillRoll_TotalTargetNumber", "Total Target Number: {0}")]
    // Piloting Skill Roll Types
    [InlineData("PilotingSkillRollType_GyroHit", "Gyro Hit")]
    [InlineData("PilotingSkillRollType_GyroDestroyed", "Gyro Destroyed")]
    [InlineData("PilotingSkillRollType_PilotDamageFromFall", "Pilot Damage From Fall")]
    [InlineData("PilotingSkillRollType_LowerLegActuatorHit", "Lower Leg Actuator Hit")]
    [InlineData("PilotingSkillRollType_HeavyDamage", "Heavy Damage")]
    [InlineData("PilotingSkillRollType_HipActuatorHit", "Hip Actuator Hit")]
    [InlineData("PilotingSkillRollType_FootActuatorHit", "Foot Actuator Hit")]
    [InlineData("PilotingSkillRollType_LegDestroyed", "Leg Destroyed")]
    [InlineData("PilotingSkillRollType_StandupAttempt", "Standup Attempt")]
    [InlineData("PilotingSkillRollType_JumpWithDamage", "Jump with damage")]
    // Attack modifiers
    [InlineData("AttackDirection_Left", "Left")]
    [InlineData("AttackDirection_Right", "Right")]
    [InlineData("AttackDirection_Front", "Front")]
    [InlineData("AttackDirection_Rear", "Rear")]
    [InlineData("Modifier_GunnerySkill", "Gunnery Skill: +{0}")]
    [InlineData("Modifier_AttackerMovement", "Attacker Movement ({0}): +{1}")]
    [InlineData("Modifier_TargetMovement", "Target Movement ({0} hexes): +{1}")]
    [InlineData("Modifier_Range", "{0} at {1} hexes ({2} range): +{3}")]
    [InlineData("Modifier_Heat", "Heat Level ({0}): +{1}")]
    [InlineData("Modifier_Terrain", "{0} at {1}: +{2}")]
    [InlineData("Modifier_DamagedGyro", "Damaged Gyro ({0} {1}): +{2}")]
    [InlineData("Modifier_HeavyDamage", "Heavy Damage ({0} points): +{1}")]
    [InlineData("Modifier_FallingLevels", "Falling ({0} {1}): +{2}")]
    [InlineData("Modifier_LowerLegActuatorHit", "Lower Leg Actuator Hit: +{0}")]
    [InlineData("Modifier_HipActuatorHit", "Hip Actuator Hit: +{0}")]
    [InlineData("Modifier_FootActuatorHit", "Foot Actuator Hit: +{0}")]
    [InlineData("Modifier_UpperLegActuatorHit", "Upper Leg Actuator Hit: +{0}")]
    [InlineData("Modifier_LegDestroyed", "Leg Destroyed: +{0}")]
    [InlineData("Modifier_SensorHit", "Sensor Hit: +{0}")]
    [InlineData("Modifier_AimedShotHead", "Aimed Shot ({0}): +{1}")]
    [InlineData("Modifier_AimedShotBodyPart", "Aimed Shot ({0}): {1}")]
    [InlineData("Modifier_ShoulderActuatorHit", "{0} Shoulder Destroyed: +{1}")]
    [InlineData("Modifier_UpperArmActuatorHit", "{0} Upper Arm Actuator Destroyed: +{1}")]
    [InlineData("Modifier_LowerArmActuatorHit", "{0} Lower Arm Actuator Destroyed: +{1}")]
    [InlineData("Modifier_ProneFiring", "Prone Firing: +{0}")]
    [InlineData("WeaponRestriction_NotAvailable", "Weapon not available")]
    [InlineData("WeaponRestriction_ProneLegs", "Cannot fire leg weapons while prone")]
    [InlineData("WeaponRestriction_ProneOtherArm", "Only one arm can fire while prone")]
    [InlineData("Hits", "Hits")]
    [InlineData("Levels", "Levels")]
    // Attack information
    [InlineData("Attack_NoLineOfSight", "No LOS")]
    [InlineData("Attack_TargetNumber", "Target ToHit Number")]
    [InlineData("Attack_OutOfRange", "Target out of range")]
    // Penalty messages
    [InlineData("Penalty_FootActuatorMovement", "{0} destroyed foot actuator(s) | -{1} MP")]
    [InlineData("Penalty_HeatMovement", "Heat level {0} | -{1} MP")]
    [InlineData("Penalty_EngineHeat", "Engine Heat Penalty ({0} hits): +{1} heat/turn")]
    [InlineData("Penalty_LowerLegActuatorMovement", "{0} destroyed lower leg actuator(s) | -{1} MP")]
    [InlineData("Penalty_UpperLegActuatorMovement", "{0} destroyed upper leg actuator(s) | -{1} MP")]
    [InlineData("Penalty_LegDestroyed_Single", "Leg destroyed | -{0} MP")]
    [InlineData("Penalty_LegDestroyed_Both", "Both legs destroyed | No movement")]
    [InlineData("Penalty_HipDestroyed_Single", "Hip destroyed | -{0} MP")]
    [InlineData("Penalty_HipDestroyed_Both", "Both hips destroyed | No movement")]
    [InlineData("Attack_NoModifiersCalculated", "Attack modifiers not calculated")]
    [InlineData("Attack_Targeting", "Already targeting {0}")]
    [InlineData("Attack_NoAmmo", "No ammunition")]
    [InlineData("Attack_WeaponDestroyed", "Weapon is destroyed")]
    [InlineData("Attack_LocationDestroyed", "Location is destroyed")]
    // Secondary target modifiers
    [InlineData("Attack_SecondaryTargetFrontArc", "Secondary target (front arc): +{0}")]
    [InlineData("Attack_SecondaryTargetOtherArc", "Secondary target (other arc): +{0}")]
    // Deployment actions
    [InlineData("Action_SelectUnitToDeploy", "Select Unit")]
    [InlineData("Action_SelectDeploymentHex", "Select Hex")]
    // Weapon attack actions
    [InlineData("Action_SelectUnitToFire", "Select unit to fire weapons")]
    [InlineData("Action_SelectAction", "Select action")]
    [InlineData("Action_ConfigureWeapons", "Configure weapons")]
    [InlineData("Action_SelectTarget", "Select Target")]
    [InlineData("Action_TurnTorso", "Turn Torso")]
    [InlineData("Action_SkipAttack", "Skip Attack")]
    [InlineData("Action_DeclareAttack", "Declare Attack")]
    // Movement actions
    [InlineData("Action_SelectUnitToMove", "Select unit to move")]
    [InlineData("Action_SelectMovementType", "Select movement type")]
    [InlineData("Action_SelectTargetHex", "Select target hex")]
    [InlineData("Action_SelectFacingDirection", "Select facing direction")]
    [InlineData("Action_MoveUnit", "Move Unit")]
    [InlineData("Action_StandStill", "Stand Still")]
    [InlineData("Action_StayProne", "Stay Prone")]
    [InlineData("Action_MovementPoints", "{0} | MP: {1}")]
    [InlineData("Action_AttemptStandup", "Attempt Standup")]
    [InlineData("Action_ChangeFacing", "Change Facing | MP: {0}")]
    // Movement types
    [InlineData("MovementType_Walk", "Walk")]
    [InlineData("MovementType_Run", "Run")]
    [InlineData("MovementType_Jump", "Jump")]
    // Heat update command strings
    [InlineData("Command_HeatUpdated_Header", "Heat update for {0} (Previous: {1})")]
    [InlineData("Command_HeatUpdated_Sources", "Heat sources:")]
    [InlineData("Command_HeatUpdated_MovementHeat", "+ {0} movement ({1} MP): {2} heat")]
    [InlineData("Command_HeatUpdated_WeaponHeat", "+ Firing {0}: {1} heat")]
    [InlineData("Command_HeatUpdated_TotalGenerated", "Total heat generated: {0}")]
    [InlineData("Command_HeatUpdated_Dissipation", "- Heat dissipation from {0} heat sinks and {1} engine heat sinks: -{2} heat")]
    // Start phase
    [InlineData("StartPhase_ActionLabel", "Ready to play")]
    [InlineData("StartPhase_PlayerActionLabel", "Ready")]
    // End phase
    [InlineData("EndPhase_ActionLabel", "End your turn")]
    [InlineData("EndPhase_PlayerActionLabel", "End Turn")]
    [InlineData("Action_Shutdown", "Shutdown")]
    [InlineData("Action_Startup", "Startup")]
    // Mech part names
    [InlineData("MechPart_LeftArm", "Left Arm")]
    [InlineData("MechPart_RightArm", "Right Arm")]
    [InlineData("MechPart_LeftTorso", "Left Torso")]
    [InlineData("MechPart_RightTorso", "Right Torso")]
    [InlineData("MechPart_CenterTorso", "Center Torso")]
    [InlineData("MechPart_Head", "Head")]
    [InlineData("MechPart_LeftLeg", "Left Leg")]
    [InlineData("MechPart_RightLeg", "Right Leg")]
    // Short Mech part names
    [InlineData("MechPart_LeftArm_Short", "LA")]
    [InlineData("MechPart_RightArm_Short", "RA")]
    [InlineData("MechPart_LeftTorso_Short", "LT")]
    [InlineData("MechPart_RightTorso_Short", "RT")]
    [InlineData("MechPart_CenterTorso_Short", "CT")]
    [InlineData("MechPart_Head_Short", "H")]
    [InlineData("MechPart_LeftLeg_Short", "LL")]    
    [InlineData("MechPart_RightLeg_Short", "RL")]
    // UI Events
    [InlineData("Events_Unit_ArmorDamage", "Damage at {0}|-{1}")]
    [InlineData("Events_Unit_StructureDamage", "Damage at {0}|-{1}")]
    [InlineData("Events_Unit_Explosion", "{0} exploded")]
    [InlineData("Events_Unit_CriticalHit", "Critical Hit at {0}")]
    [InlineData("Events_Unit_ComponentDestroyed", "{0} destroyed")]
    [InlineData("Events_Unit_LocationDestroyed", "{0} destroyed")]
    [InlineData("Events_Unit_UnitDestroyed", "{0} has been destroyed!")]
    [InlineData("Events_Unit_PilotDamage", "{0} took damage | -{1}")]
    [InlineData("Events_Unit_PilotDead", "{0} was killed")]
    [InlineData("Events_Unit_PilotUnconscious", "{0} fell unconscious")]
    [InlineData("Events_Unit_PilotRecovered", "{0} regained consciousness")]

    // Consciousness roll commands
    [InlineData("Command_PilotConsciousnessRoll_Consciousness", "consciousness")]
    [InlineData("Command_PilotConsciousnessRoll_Recovery", "consciousness recovery")]
    [InlineData("Command_PilotConsciousnessRoll_Success", "{0} {1} roll succeeded")]
    [InlineData("Command_PilotConsciousnessRoll_Failure", "{0} {1} roll failed")]
    [InlineData("Command_PilotConsciousnessRoll_ConsciousnessNumber", "Consciousness Number: {0}")]
    [InlineData("Command_RollResult", "Roll Result: {0}")]

    // Pilot status
    [InlineData("Pilot_Status_Unknown", "UNKNOWN")]
    [InlineData("Pilot_Status_Conscious", "CONSCIOUS")]
    [InlineData("Pilot_Status_Unconscious", "UNCONSCIOUS")]
    // Default
    [InlineData("Key_Not_Found", "Key_Not_Found")]
    public void GetString_MiscellaneousKeys_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Command_MechRestart_Automatic", "{0} automatically restarted (heat level {1})")]
    [InlineData("Command_MechRestart_Successful", "{0} successfully restarted (heat level {1})")]
    [InlineData("Command_MechRestart_Failed", "{0} failed to restart (heat level {1})")]
    [InlineData("Command_MechRestart_Impossible", "{0} cannot restart (heat level {1})")]
    [InlineData("Command_MechRestart_Generic", "{0} restart attempt")]
    [InlineData("Command_AvoidNumber", "Avoid Number: {0}")]
    [InlineData("Command_MechShutdown_Avoided", "{0} avoided shutdown (heat level {1})")]
    [InlineData("Command_MechShutdown_AutomaticHeat", "{0} automatically shut down due to excessive heat (level {1})")]
    [InlineData("Command_MechShutdown_UnconsciousPilot", "{0} shut down due to unconscious pilot (heat level {1})")]
    [InlineData("Command_MechShutdown_FailedRoll", "{0} shut down due to heat (level {1})")]
    [InlineData("Command_MechShutdown_Voluntary", "{0} voluntarily shut down")]
    [InlineData("Command_MechShutdown_Generic", "{0} shut down")]
    public void GetString_HeatShutdownRestartCommands_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Command_AmmoExplosion_Avoided", "{0} avoided ammo explosion due to heat")]
    [InlineData("Command_AmmoExplosion_Failed", "{0} suffered ammo explosion due to heat")]
    [InlineData("Command_AmmoExplosion_RollDetails", "Heat level: {0}, Roll: {1} vs {2}")]
    [InlineData("Command_AmmoExplosion_CriticalHits", "Explosion caused critical hits:")]
    [InlineData("Command_AmmoExplosion_ComponentDestroyed", "- {0} in {1} destroyed by explosion")]
    [InlineData("Command_AmmoExplosion_Explosion", "{0} exploded, damage: {1}")]
    public void GetString_AmmoExplosion_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
}