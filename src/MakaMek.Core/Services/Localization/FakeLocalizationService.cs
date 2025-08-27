namespace Sanet.MakaMek.Core.Services.Localization;

public class FakeLocalizationService: ILocalizationService
{
    public string GetString(string key)
    {
        return key switch
        {
            "Command_JoinGame" => "{0} has joined game with {1} units.",
            "Command_MoveUnit" => "{0} moved {1} to {2} facing {3} using {4}.",
            "Command_DeployUnit" => "{0} deployed {1} to {2} facing {3}.",
            "Command_TryStandup" => "{0} attempts to stand up {1}.",
            "Command_MechStandup" => "{0} Mech stood up successfully. {1}",
            "Command_RollDice" => "{0} rolls",
            "Command_DiceRolled" => "{0} rolled {1}.",
            "Command_UpdatePlayerStatus" => "{0}'s status is {1}.",
            "Command_ChangePhase" => "Game changed phase to {0}.",
            "Command_ChangeActivePlayer" => "{0}'s turn.",
            "Command_ChangeActivePlayerUnits" => "{0}'s turn to play {1} units.",
            "Command_WeaponConfiguration_TorsoRotation" => "{0}'s {1} rotates torso to face {2}",
            "Command_WeaponConfiguration_ArmsFlip" => "{0}'s {1} flips arms {2}",
            "Command_WeaponAttackDeclaration_NoAttacks" => "{0}'s {1} declares no attacks",
            "Command_WeaponAttackDeclaration_Header" => "{0}'s {1} declares attacks:",
            "Command_WeaponAttackDeclaration_WeaponLine" => "- {0} targeting {1}'s {2}",
            "Command_WeaponAttackResolution" => "{0}'s {1} attacks with {2} targeting {3}'s {4}. To hit number: {5}",
            "Command_WeaponAttackResolution_Hit" => "{0}'s {1} hits {3}'s {4} with {2} (Target: {5}, Roll: {6})",
            "Command_WeaponAttackResolution_Miss" => "{0}'s {1} misses {3}'s {4} with {2} (Target: {5}, Roll: {6})",
            "Command_WeaponAttackResolution_Direction" => "Attack Direction: {0}",
            "Command_WeaponAttackResolution_TotalDamage" => "Total Damage: {0}",
            "Command_WeaponAttackResolution_MissilesHit" => "Missiles Hit: {0}",
            "Command_WeaponAttackResolution_ClusterRoll" => "Cluster Roll: {0}",
            "Command_WeaponAttackResolution_HitLocations" => "Hit Locations:",
            "Command_WeaponAttackResolution_HitLocation" => "{0}: {1} damage (Roll: {2})",
            "Command_WeaponAttackResolution_HitLocationTransfer" => "{0} → {1}: {2} damage (Roll: {3})",
            "Command_WeaponAttackResolution_AimedShotSuccessful" => "Aimed Shot targeting {0} succeeded, Roll: {1}",
            "Command_WeaponAttackResolution_HitLocationExcessDamage" => "  Excess damage {1} transferred to {0}",
            "Command_WeaponAttackResolution_AimedShotFailed" => "Aimed Shot targeting {0} failed, Roll: {1}",

            // Critical Hits Resolution Command
            "Command_CriticalHitsResolution_Location" => "Critical hits in {0}:",
            "Command_CriticalHitsResolution_CritRoll" => "Critical Roll: {0}",
            "Command_CriticalHitsResolution_BlownOff" => "Critical hit in {0}, location blown off",
            "Command_CriticalHitsResolution_NumCrits" => "Number of critical hits: {0}",
            "Command_CriticalHitsResolution_CriticalHit" => "Critical hit in {0} slot {1}: {2}",
            "Command_CriticalHitsResolution_Explosion" => "{0} exploded, damage: {1}",
            "Command_WeaponAttackResolution_DestroyedParts" => "Destroyed parts:",
            "Command_WeaponAttackResolution_DestroyedPart" => "- {0} destroyed",
            "Command_WeaponAttackResolution_UnitDestroyed" => "{0} has been destroyed!",
            "Command_TurnEnded" => "{0} has ended their turn.",
            "Command_ShutdownUnit" => "{0} requests to shut down {1}.",
            "Command_StartupUnit" => "{0} requests to start up {1}.",
            "Command_TurnIncremented" => "Turn {0} has started.",
            "Command_RequestGameLobbyStatus" => "Client {0} requested game lobby status for game.",
            "Command_SetBattleMap" => "Battle map has been set.",
            "Direction_Forward" => "forward",
            "Direction_Backward" => "backward",
            
            // MechFallingCommand strings
            "Command_MechFalling_Base" => "{0} fell",
            "Command_MechFalling_Levels" => " {0} level(s)",
            "Command_MechFalling_Jumping" => " while jumping",
            "Command_MechFalling_Damage" => " and took {0} damage",
            "Command_MechFalling_PilotInjury" => "Pilot was injured",
            
            // Piloting Skill Roll Command
            "Command_PilotingSkillRoll_Success" => "{0} roll succeeded",
            "Command_PilotingSkillRoll_Failure" => "{0} roll failed",
            "Command_PilotingSkillRoll_ImpossibleRoll" => "{0} roll is impossible",
            "Command_PilotingSkillRoll_BasePilotingSkill" => "Base Piloting Skill: {0}",
            "Command_PilotingSkillRoll_Modifiers" => "Modifiers:",
            "Command_PilotingSkillRoll_TotalTargetNumber" => "Total Target Number: {0}",
            "Command_PilotingSkillRoll_RollResult" => "Roll Result: {0}",
            
            // Piloting Skill Roll Types
            "PilotingSkillRollType_GyroHit" => "Gyro Hit",
            "PilotingSkillRollType_GyroDestroyed" => "Gyro Destroyed",
            "PilotingSkillRollType_PilotDamageFromFall" => "Pilot Damage From Fall",
            "PilotingSkillRollType_LowerLegActuatorHit" => "Lower Leg Actuator Hit",
            "PilotingSkillRollType_HeavyDamage" => "Heavy Damage",
            "PilotingSkillRollType_HipActuatorHit" => "Hip Actuator Hit",
            "PilotingSkillRollType_FootActuatorHit" => "Foot Actuator Hit",
            "PilotingSkillRollType_LegDestroyed" => "Leg Destroyed",
            "PilotingSkillRollType_StandupAttempt" => "Standup Attempt",
            "PilotingSkillRollType_JumpWithDamage" => "Jump with damage",
            
            // Attack direction strings
            "AttackDirection_Left" => "Left",
            "AttackDirection_Right" => "Right",
            "AttackDirection_Front" => "Front",
            "AttackDirection_Rear" => "Rear",
            
            // Roll modifiers
            "Modifier_GunnerySkill" => "Gunnery Skill: +{0}",
            "Modifier_AttackerMovement" => "Attacker Movement ({0}): +{1}",
            "Modifier_TargetMovement" => "Target Movement ({0} hexes): +{1}",
            "Modifier_Range" => "{0} at {1} hexes ({2} range): +{3}",
            "Modifier_Heat" => "Heat Level ({0}): +{1}",
            "Modifier_Terrain" => "{0} at {1}: +{2}",
            "Modifier_DamagedGyro" => "Damaged Gyro ({0} {1}): +{2}",
            "Modifier_HeavyDamage" => "Heavy Damage ({0} points): +{1}",
            "Modifier_FallingLevels" => "Falling ({0} {1}): +{2}",
            "Modifier_LowerLegActuatorHit" => "Lower Leg Actuator Hit: +{0}",
            "Modifier_HipActuatorHit" => "Hip Actuator Hit: +{0}",
            "Modifier_FootActuatorHit" => "Foot Actuator Hit: +{0}",
            "Modifier_UpperLegActuatorHit" => "Upper Leg Actuator Hit: +{0}",
            "Modifier_LegDestroyed" => "Leg Destroyed: +{0}",
            "Modifier_SensorHit" => "Sensor Hit: +{0}",
            // Aimed Shot Modifiers
            "Modifier_AimedShotHead" => "Aimed Shot ({0}): +{1}",
            "Modifier_AimedShotBodyPart" => "Aimed Shot ({0}): {1}",
            // Arm Critical Hit Modifiers
            "Modifier_ShoulderActuatorHit" => "{0} Shoulder Destroyed: +{1}",
            "Modifier_UpperArmActuatorHit" => "{0} Upper Arm Actuator Destroyed: +{1}",
            "Modifier_LowerArmActuatorHit" => "{0} Lower Arm Actuator Destroyed: +{1}",
            // Prone Firing Modifier
            "Modifier_ProneFiring" => "Prone Firing: +{0}",
            // Weapon Restriction Reasons
            "WeaponRestriction_NotAvailable" => "Weapon not available",
            "WeaponRestriction_ProneLegs" => "Cannot fire leg weapons while prone",
            "WeaponRestriction_ProneOtherArm" => "Only one arm can fire while prone",
            "Hits" => "Hits",
            "Levels" => "Levels",
            // Attack information
            "Attack_NoLineOfSight" => "No LOS",
            "Attack_TargetNumber" => "Target ToHit Number",
            "Attack_OutOfRange" => "Target out of range",
            "Attack_NoModifiersCalculated" => "Attack modifiers not calculated",
            "Attack_Targeting" => "Already targeting {0}",
            "Attack_NoAmmo" => "No ammunition",
            "Attack_WeaponDestroyed" => "Weapon is destroyed",
            "Attack_LocationDestroyed" => "Location is destroyed",
            
            // Secondary target modifiers
            "Attack_SecondaryTargetFrontArc" => "Secondary target (front arc): +{0}",
            "Attack_SecondaryTargetOtherArc" => "Secondary target (other arc): +{0}",

            // Deployment actions
            "Action_SelectUnitToDeploy" => "Select Unit",
            "Action_SelectDeploymentHex" => "Select Hex",

            // Weapon attack actions
            "Action_SelectUnitToFire" => "Select unit to fire weapons",
            "Action_SelectAction" => "Select action",
            "Action_ConfigureWeapons" => "Configure weapons",
            "Action_SelectTarget" => "Select Target",
            "Action_TurnTorso" => "Turn Torso",
            "Action_SkipAttack" => "Skip Attack",
            "Action_DeclareAttack" => "Declare Attack",
            
            // Movement actions
            "Action_SelectUnitToMove" => "Select unit to move",
            "Action_SelectMovementType" => "Select movement type",
            "Action_SelectTargetHex" => "Select target hex",
            "Action_SelectFacingDirection" => "Select facing direction",
            "Action_MoveUnit" => "Move Unit",
            "Action_StandStill" => "Stand Still",
            "Action_StayProne" => "Stay Prone",
            "Action_AttemptStandup" => "Attempt Standup",
            "Action_ChangeFacing" => "Change Facing | MP: {0}",
            "Action_MovementPoints" => "{0} | MP: {1}",
            
            // Movement types
            "MovementType_Walk" => "Walk",
            "MovementType_Run" => "Run",
            "MovementType_Jump" => "Jump",
            
            // Heat update command strings
            "Command_HeatUpdated_Header" => "Heat update for {0} (Previous: {1})",
            "Command_HeatUpdated_Sources" => "Heat sources:",
            "Command_HeatUpdated_MovementHeat" => "+ {0} movement ({1} MP): {2} heat",
            "Command_HeatUpdated_WeaponHeat" => "+ Firing {0}: {1} heat",
            "Command_HeatUpdated_TotalGenerated" => "Total heat generated: {0}",
            "Command_HeatUpdated_Dissipation" => "- Heat dissipation from {0} heat sinks and {1} engine heat sinks: -{2} heat",

            // Consciousness roll command strings
            "Command_PilotConsciousnessRoll_Consciousness" => "consciousness",
            "Command_PilotConsciousnessRoll_Recovery" => "consciousness recovery",
            "Command_PilotConsciousnessRoll_Success" => "{0} {1} roll succeeded",
            "Command_PilotConsciousnessRoll_Failure" => "{0} {1} roll failed",
            "Command_PilotConsciousnessRoll_ConsciousnessNumber" => "Consciousness Number: {0}",
            "Command_PilotConsciousnessRoll_RollResult" => "Roll Result: {0}",
            
            // Start phase
            "StartPhase_ActionLabel" => "Ready to play",
            "StartPhase_PlayerActionLabel" => "Ready",
            
            // End phase
            "EndPhase_ActionLabel" => "End your turn",
            "EndPhase_PlayerActionLabel" => "End Turn",
            "Action_Shutdown" => "Shutdown",
            "Action_Startup" => "Startup",
            
            // Mech part names
            "MechPart_LeftArm" => "Left Arm",
            "MechPart_RightArm" => "Right Arm",
            "MechPart_LeftTorso" => "Left Torso",
            "MechPart_RightTorso" => "Right Torso",
            "MechPart_CenterTorso" => "Center Torso",
            "MechPart_Head" => "Head",
            "MechPart_LeftLeg" => "Left Leg",
            "MechPart_RightLeg" => "Right Leg",
            
            // Short Mech part names
            "MechPart_LeftArm_Short" => "LA",
            "MechPart_RightArm_Short" => "RA",
            "MechPart_LeftTorso_Short" => "LT",
            "MechPart_RightTorso_Short" => "RT",
            "MechPart_CenterTorso_Short" => "CT",
            "MechPart_Head_Short" => "H",
            "MechPart_LeftLeg_Short" => "LL",
            "MechPart_RightLeg_Short" => "RL",
            
            // UI Events
            "Events_Unit_ArmorDamage" => "Damage at {0}|-{1}",
            "Events_Unit_StructureDamage" => "Damage at {0}|-{1}",
            "Events_Unit_Explosion" => "{0} exploded",
            "Events_Unit_CriticalHit" => "Critical Hit at {0}",
            "Events_Unit_ComponentDestroyed" => "{0} destroyed",
            "Events_Unit_LocationDestroyed" => "{0} destroyed",
            "Events_Unit_UnitDestroyed" => "{0} has been destroyed!",
            "Events_Unit_PilotDamage" => "{0} took damage | -{1}",
            "Events_Unit_PilotDead" => "{0} was killed",
            "Events_Unit_PilotUnconscious" => "{0} fell unconscious",
            "Events_Unit_PilotRecovered" => "{0} regained consciousness",
            
            // Penalty messages
            "Penalty_FootActuatorMovement" => "{0} destroyed foot actuator(s) | -{1} MP",
            "Penalty_HeatMovement" => "Heat level {0} | -{1} MP",
            "Penalty_EngineHeat"   => "Engine Heat Penalty ({0} hits): +{1} heat/turn",
            "Penalty_LowerLegActuatorMovement" => "{0} destroyed lower leg actuator(s) | -{1} MP",
            "Penalty_UpperLegActuatorMovement" => "{0} destroyed upper leg actuator(s) | -{1} MP",
            "Penalty_LegDestroyed_Single" => "Leg destroyed | -{0} MP",
            "Penalty_LegDestroyed_Both" => "Both legs destroyed | No movement",
            "Penalty_HipDestroyed_Single" => "Hip destroyed | -{0} MP",
            "Penalty_HipDestroyed_Both" => "Both hips destroyed | No movement",

            // Heat shutdown/restart commands
            "Command_MechRestart_Automatic" => "{0} automatically restarted (heat level {1})",
            "Command_MechRestart_Successful" => "{0} successfully restarted (heat level {1}, roll: [{2}] = {3} vs {4})",
            "Command_MechRestart_Failed" => "{0} failed to restart (heat level {1}, roll: [{2}] = {3} vs {4})",
            "Command_MechRestart_Impossible" => "{0} cannot restart (heat level {1})",
            "Command_MechRestart_Generic" => "{0} restart attempt",
            "Command_MechShutdown_Avoided" => "{0} avoided shutdown (heat level {1}, roll: [{2}] = {3} vs {4})",
            "Command_MechShutdown_AutomaticHeat" => "{0} automatically shut down due to excessive heat (level {1})",
            "Command_MechShutdown_UnconsciousPilot" => "{0} shut down due to unconscious pilot (heat level {1})",
            "Command_MechShutdown_FailedRoll" => "{0} shut down due to heat (level {1}, roll: [{2}] = {3} vs {4})",
            "Command_MechShutdown_Voluntary" => "{0} voluntarily shut down",
            "Command_MechShutdown_Generic" => "{0} shut down",

            // Pilot status
            "Pilot_Status_Unknown" => "UNKNOWN",
            "Pilot_Status_Conscious" => "CONSCIOUS",
            "Pilot_Status_Unconscious" => "UNCONSCIOUS",

            // Ammo explosion commands
            "Command_AmmoExplosion_Avoided" => "{0} avoided ammo explosion due to heat",
            "Command_AmmoExplosion_Failed" => "{0} suffered ammo explosion due to heat",
            "Command_AmmoExplosion_RollDetails" => "Heat level: {0}, Roll: {1} vs {2}",
            "Command_AmmoExplosion_CriticalHits" => "Explosion caused critical hits:",
            "Command_AmmoExplosion_ComponentDestroyed" => "- {0} in {1} destroyed by explosion",
            "Command_AmmoExplosion_Explosion" => "{0} exploded, damage: {1}",
            _ => key
        };
    }
}