using Sanet.MakaMek.Core.Models.Game.Phases;
using Shouldly;

namespace Sanet.MakaMek.Localization.Tests;

public class FakeLocalizationServiceTests
{
    [Theory]
    [InlineData("Command_JoinGame", "{0} has joined game with {1} units")]
    [InlineData("Command_PlayerLeft", "{0} has left the game")]
    [InlineData("Command_MoveUnit", "{0} moved {1} to {2} facing {3} using {4}")]
    [InlineData("Command_DeployUnit", "{0} deployed {1} to {2} facing {3}")]
    [InlineData("Command_TryStandup", "{0} attempts to stand up {1}")]
    [InlineData("Command_MechStandup", "{0} Mech stood up successfully. {1}")]
    [InlineData("Command_RollDice", "{0} rolls")]
    [InlineData("Command_DiceRolled", "{0} rolled {1}")]
    [InlineData("Command_UpdatePlayerStatus", "{0}'s status is {1}")]
    [InlineData("Command_ChangePhase", "Game changed phase to {0}")]
    [InlineData("Command_StartPhase", "Phase {0} started")]
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
    [InlineData("Command_WeaponAttackResolution_DamageAbsorbedByTerrain", "Damage absorbed by terrain at {0}: {1}")]
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
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorAndStructure", "  Excess damage {1} armor, {2} structure transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_ArmorOnly", "  Excess damage {1} armor transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExcessDamage_StructureOnly", "  Excess damage {1} structure transferred to {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExplosionDamage_ArmorAndStructure", "  {0}: {1} armor, {2} structure")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExplosionDamage_ArmorOnly", "  {0}: {1} armor")]
    [InlineData("Command_WeaponAttackResolution_HitLocationExplosionDamage_StructureOnly", "  {0}: {1} structure")]
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
    [InlineData("Command_CriticalHitsResolution_Header", "{0} suffered structure damage and requires critical hit rolls")]
    [InlineData("Command_CriticalHitsResolution_ExplosionDamageDistribution", "Explosion damage distribution:")]
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
    [InlineData("Command_GameEnded_Unknown", "Game aborted")]
    [InlineData("Command_GameEnded_Victory", "Game ended: Victory")]
    [InlineData("Command_GameEnded_PlayersLeft", "Players left. Game ended")]
    public void GetString_GameEnded_ReturnsExpectedString(string key, string expected)
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
    [InlineData("Command_MechFalling_PsrIntro", "{0} may fall")]
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
    [InlineData("PilotingSkillRollType_UpperLegActuatorHit", "Upper Leg Actuator Hit")]
    [InlineData("PilotingSkillRollType_HeavyDamage", "Heavy Damage")]
    [InlineData("PilotingSkillRollType_HipActuatorHit", "Hip Actuator Hit")]
    [InlineData("PilotingSkillRollType_FootActuatorHit", "Foot Actuator Hit")]
    [InlineData("PilotingSkillRollType_LegDestroyed", "Leg Destroyed")]
    [InlineData("PilotingSkillRollType_StandupAttempt", "Standup Attempt")]
    [InlineData("PilotingSkillRollType_JumpWithDamage", "Jump with damage")]
    [InlineData("PilotingSkillRollType_WaterEntry", "Water Entry")]
    [InlineData("PilotingSkillRollType_WaterEntry_WithDepth", "{0} (Depth {1})")]
    [InlineData("PilotingSkillRollType_PilotDamageFromFall_WithLevels", "{0} ({1} levels)")]
    // Attack modifiers
    [InlineData("AttackDirection_Left", "Left")]
    [InlineData("AttackDirection_Right", "Right")]
    [InlineData("AttackDirection_Front", "Front")]
    [InlineData("AttackDirection_Rear", "Rear")]
    [InlineData("Modifier_GunnerySkill", "Gunnery Skill: +{0}")]
    [InlineData("Modifier_AttackerMovement", "Attacker Movement ({0}): +{1}")]
    [InlineData("Modifier_TargetMovement", "Target Movement ({0} hexes): +{1}")]
    [InlineData("Modifier_Range", "{0} at {1} hexes ({2} range): +{3}")]
    [InlineData("Modifier_Heat", "Heat ({0}) Attack Modifier: +{1}")]
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
    [InlineData("Modifier_PartialCover", "Partial Cover: +{0}")]
    [InlineData("Modifier_WaterDepth", "Water Depth ({0}): +{1}")]
    [InlineData("WeaponRestriction_NotAvailable", "Weapon not available")]
    [InlineData("WeaponRestriction_PartialCoverLegs", "Cannot fire leg weapons while in partial cover")]
    [InlineData("WeaponRestriction_ProneLegs", "Cannot fire leg weapons while prone")]
    [InlineData("WeaponRestriction_ProneOtherArm", "Only one arm can fire while prone")]
    [InlineData("Hits", "Hits")]
    [InlineData("Levels", "Levels")]
    // Attack information
    [InlineData("Attack_NoLineOfSight", "No LOS")]
    [InlineData("Attack_TargetNumber", "Target ToHit Number")]
    [InlineData("Attack_OutOfRange", "Target out of range")]
    [InlineData("Attack_NoModifiersCalculated", "Attack modifiers not calculated")]
    [InlineData("Attack_Targeting", "Already targeting {0}")]
    [InlineData("Attack_NoAmmo", "No ammunition")]
    [InlineData("Attack_WeaponDestroyed", "Weapon is destroyed")]
    [InlineData("Attack_LocationDestroyed", "Location is destroyed")]
    [InlineData("Attack_OutsideFiringArc", "Outside firing arc")]
    [InlineData("Attack_ImpossibleToHit", "Impossible to hit")]
    
    // Penalty messages
    [InlineData("Penalty_FootActuatorMovement", "{0} destroyed foot actuator(s) | -{1} MP")]
    [InlineData("Penalty_HeatMovement", "Heat ({0}) MP Penalty | -{1} MP")]
    [InlineData("Penalty_EngineHeat", "Engine Heat Penalty ({0} hits): +{1} heat/turn")]
    [InlineData("Penalty_LowerLegActuatorMovement", "{0} destroyed lower leg actuator(s) | -{1} MP")]
    [InlineData("Penalty_UpperLegActuatorMovement", "{0} destroyed upper leg actuator(s) | -{1} MP")]
    [InlineData("Penalty_LegDestroyed_Single", "Leg destroyed | -{0} MP")]
    [InlineData("Penalty_LegDestroyed_Both", "Both legs destroyed | No movement")]
    [InlineData("Penalty_HipDestroyed_Single", "Hip destroyed | -{0} MP")]
    [InlineData("Penalty_HipDestroyed_Both", "Both hips destroyed | No movement")]
    
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
    [InlineData("Action_ConfirmMovement", "Confirm movement")]
    [InlineData("Action_ConfirmOrSelectNextHex", "Confirm or select next hex")]
    [InlineData("Action_StandStill", "Stand Still")]
    [InlineData("Action_StayProne", "Stay Prone")]
    [InlineData("Action_MovementPoints", "{0} | MP: {1}")]
    [InlineData("Action_AttemptStandup", "Attempt Standup")]
    [InlineData("Action_ChangeFacing", "Change Facing | MP: {0}")]
    // Movement types
    [InlineData("MovementType_Walk", "Walk")]
    [InlineData("MovementType_Run", "Run")]
    [InlineData("MovementType_Jump", "Jump")]
    // Hex highlight (LOS / tooltips)
    [InlineData("HexHighlight_LosBlocked_Elevation", "Elevation at {0}")]
    [InlineData("HexHighlight_LosBlocked_InterveningTerrain", "Terrain at {0}")]
    [InlineData("HexHighlight_LosBlocked_InvalidCoordinates", "Invalid coordinates")]
    // Heat update command strings
    [InlineData("Command_HeatUpdated_Header", "Heat update for {0} (Previous: {1})")]
    [InlineData("Command_HeatUpdated_Sources", "Heat sources:")]
    [InlineData("Command_HeatUpdated_MovementHeat", "+ {0} movement ({1} MP): {2} heat")]
    [InlineData("Command_HeatUpdated_WeaponHeat", "+ Firing {0}: {1} heat")]
    [InlineData("Command_HeatUpdated_ExternalHeat", "+ External heat from {0}: {1} heat")]
    [InlineData("Command_HeatUpdated_ExternalHeat_Lost", "- Wasted {0} points of external heat")]
    [InlineData("Command_HeatUpdated_TotalGenerated", "Total heat generated: {0}")]
    [InlineData("Command_HeatUpdated_Dissipation", "- Heat dissipation from {0} heat sinks and {1} engine heat sinks: -{2} heat")]
    // Start phase
    [InlineData("StartPhase_ActionLabel", "Ready to play")]
    [InlineData("StartPhase_PlayerActionLabel", "Ready")]
    // End phase
    [InlineData("EndPhase_ActionLabel", "End your turn")]
    [InlineData("EndPhase_PlayerActionLabel", "End Turn")]
    [InlineData("EndPhase_EndGameLabel", "End Game")]
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
    public void GetString_Commands_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
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
    public void GetString_AmmoExplosion_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData("Command_Error_DuplicateCommand", "Duplicate command detected")]
    [InlineData("Command_Error_ValidationFailed", "Validation failed")]
    [InlineData("Command_Error_InvalidGameState", "Invalid game state")]
    public void GetString_ErrorCommands_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("MainMenu_Loading_Content", "Loading content...")]
    [InlineData("MainMenu_Loading_Units", "Loading units...")]
    [InlineData("MainMenu_Loading_Biomes", "Loading biomes...")]
    [InlineData("MainMenu_Loading_NoUnitsFound", "No units found")]
    [InlineData("MainMenu_Loading_NoBiomesFound", "No biomes found")]
    [InlineData("MainMenu_Loading_UnitsLoaded", "Loaded {0} units")]
    [InlineData("MainMenu_Loading_BiomesLoaded", "Loaded {0} biomes")]
    [InlineData("MainMenu_Loading_UnitsError", "Error loading units: {0}")]
    [InlineData("MainMenu_Loading_BiomesError", "Error loading biomes: {0}")]
    [InlineData("MainMenu_Loading_NoItemsFound", "No items found")]
    [InlineData("MainMenu_Loading_ItemsLoaded", "Loaded {0} items")]
    [InlineData("MainMenu_Loading_Error", "Error loading content: {0}")]
    public void GetString_MainMenuLoadingMessages_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData("HeatProjection_ProjectionText", "Heat: {0} → {1}")]
    [InlineData("HeatProjection_CurrentHeatText", "Heat: {0}")]
    [InlineData("HeatProjection_DissipationText", "Dissipation: {0}")]
    public void GetString_HeatProjection_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData("EndGame_Victory_Title", "Victory!")]
    [InlineData("EndGame_Title", "Game Over")]
    [InlineData("EndGame_Victory_Subtitle", "{0} is victorious!")]
    [InlineData("EndGame_Draw_Subtitle", "The battle ended in a draw")]
    [InlineData("EndGame_PlayersLeft_Subtitle", "All players have left the game")]
    [InlineData("EndGame_ReturnToMenu", "Return to Menu")]
    [InlineData("EndGame_Victor_Badge", "VICTOR")]
    public void GetString_EndGame_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("About_GameDescription", "MakaMek is an open-source tactical combat game that follows Classic BattleTech rules. The game is inspired by another computer implementation of BattleTech called MegaMek but focusing on simplicity and accessibility for all players. We aim to keep gameplay simple and prioritize a mobile-first and web-first user experience.")]
    [InlineData("About_MegaMekAttribution", "Some art and assets used in this project—specifically unit and terrain images—are taken from the MegaMek Data Repository. These materials are used as-is without any modifications and are distributed under the Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.")]
    [InlineData("About_ContactStatement", "If there are any problems using any in-game material, or you have any questions, please feel free to contact me.")]
    [InlineData("About_FreeAndOpenSourceStatement", "This game is free, open source, not affiliated with any copyright or trademark holders and distributed under the GPLv3 license.")]
    [InlineData("About_TrademarkNotice1", "MechWarrior and BattleMech are registered trademarks of The Topps Company, Inc.")]
    [InlineData("About_TrademarkNotice2", "Microsoft holds the license for MechWarrior computer games. This game is NOT affiliated with Microsoft.")]
    [InlineData("About_GameContentRulesNotice", "This game follows Microsoft's \"Game Content Usage Rules\".")]
    public void GetString_About_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData("Dialog_Yes", "Yes")]
    [InlineData("Dialog_No", "No")]
    [InlineData("Dialog_LeaveGame_Title", "Leave Game")]
    [InlineData("Dialog_LeaveGame_Message", "WARNING: This action ends the game for all players")]
    public void GetString_Dialogs_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("ConnectFragment_ServerAddress", "Server Address:")]
    [InlineData("ConnectFragment_EnterServerIP", "Enter Server IP")]
    [InlineData("ConnectFragment_Connect", "Connect")]
    [InlineData("ConnectFragment_Connected", "Connected")]
    public void GetString_ConnectFragment_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("BattleMap_Turn", "TURN")]
    [InlineData("BattleMap_Phase", "PHASE")]
    [InlineData("BattleMap_ActivePlayer", "ACTIVE PLAYER")]
    [InlineData("BattleMap_SelectUnitToDeploy", "Select a Unit to deploy")]
    [InlineData("BattleMap_SelectTargetLocation", "Select Target Location")]
    [InlineData("BattleMap_ResetMap", "Reset Map")]
    [InlineData("BattleMap_UnitInfo", "Unit Info")]
    [InlineData("BattleMap_Commands", "Commands")]
    [InlineData("BattleMap_MapSettings", "Map Settings")]
    [InlineData("BattleMap_CommandLog", "Command Log")]
    [InlineData("BattleMap_Settings", "Settings")]
    [InlineData("BattleMap_ShowLabels", "Show Labels")]
    [InlineData("BattleMap_ShowHexOutlines", "Show Hex Outlines")]
    [InlineData("BattleMap_ShowHexHighlightText", "Show Hex Highlight Text")]
    public void GetString_BattleMap_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitBasicInfo_TurnIndicator", "T")]
    public void GetString_UnitBasicInfo_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitComponents_Component", "Component")]
    [InlineData("UnitComponents_Slots", "Slots")]
    [InlineData("UnitComponents_Hits", "Hits")]
    [InlineData("UnitComponents_Status", "Status")]
    public void GetString_UnitComponents_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitMovement_WalkMP", "Walk MP")]
    [InlineData("UnitMovement_RunMP", "Run MP")]
    [InlineData("UnitMovement_JumpMP", "Jump MP")]
    [InlineData("UnitMovement_Type", "Type")]
    [InlineData("UnitMovement_Points", "Points")]
    [InlineData("UnitMovement_Traversed", "Traversed")]
    public void GetString_UnitMovement_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitPilot_PilotInformation", "Pilot Information")]
    [InlineData("UnitPilot_Name", "Name:")]
    [InlineData("UnitPilot_Skills", "Skills")]
    [InlineData("UnitPilot_Gunnery", "Gunnery:")]
    [InlineData("UnitPilot_Piloting", "Piloting:")]
    [InlineData("UnitPilot_HealthStatus", "Health Status")]
    [InlineData("UnitPilot_Injuries", "Injuries: {0}/{1}")]
    [InlineData("UnitPilot_Status", "Status")]
    [InlineData("UnitPilot_Dead", "DEAD")]
    public void GetString_UnitPilot_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitWeapons_Weapon", "Weapon")]
    [InlineData("UnitWeapons_Damage", "DMG")]
    [InlineData("UnitWeapons_Heat", "HT")]
    [InlineData("UnitWeapons_Range", "Range")]
    public void GetString_UnitWeapons_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("WeaponSelection_SelectWeapons", "Select weapons to attack")]
    [InlineData("WeaponSelection_Primary", "PRIMARY")]
    [InlineData("WeaponSelection_SetPrimary", "Set Primary")]
    [InlineData("WeaponSelection_AimedShot", "Aimed Shot")]
    public void GetString_WeaponSelection_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("UnitItem_RemoveUnit", "Remove unit")]
    public void GetString_UnitItem_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("AboutView_Title", "About MakaMek")]
    [InlineData("AboutView_AboutTheGame", "About the Game")]
    [InlineData("AboutView_VisitGitHub", "Visit GitHub Repository")]
    [InlineData("AboutView_AssetAttribution", "Assets Attribution")]
    [InlineData("AboutView_VisitMegaMek", "Visit MegaMek Website")]
    [InlineData("AboutView_Contact", "Contact")]
    [InlineData("AboutView_SendEmail", "Send Email")]
    [InlineData("AboutView_License", "License")]
    [InlineData("AboutView_TrademarkNotices", "Trademark Notices")]
    [InlineData("AboutView_ViewContentRules", "View Game Content Usage Rules")]
    [InlineData("AboutView_SourceCode", "Source Code")]
    public void GetString_AboutView_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("SettingsView_Title", "Settings")]
    [InlineData("Settings_Data_SectionTitle", "Data")]
    [InlineData("Settings_Data_CacheStatus", "Loaded units: {0}, Loaded biomes: {1}")]
    [InlineData("Settings_Data_ClearCache", "Clear Cache")]
    [InlineData("Settings_Data_ClearCacheDescription", "Note: An app restart is required after clearing the cache.")]
    [InlineData("Settings_Data_Clearing", "Clearing cache...")]
    [InlineData("Settings_Data_Cleared", "Cache cleared successfully")]
    public void GetString_SettingsView_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("AvailableUnits_Title", "Available Units")]
    [InlineData("AvailableUnits_Class", "Class:")]
    [InlineData("AvailableUnits_Chassis", "Chassis")]
    [InlineData("AvailableUnits_Model", "Model")]
    [InlineData("AvailableUnits_Mass", "Mass")]
    [InlineData("AvailableUnits_Cancel", "Cancel")]
    [InlineData("AvailableUnits_AddUnit", "Add Unit")]
    public void GetString_AvailableUnits_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("JoinGame_Title", "Join Game")]
    public void GetString_JoinGame_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("MainMenu_StartNewGame", "Start New Game")]
    [InlineData("MainMenu_JoinGame", "Join Game")]
    [InlineData("MainMenu_About", "About")]
    [InlineData("MainMenu_Settings", "Settings")]
    public void GetString_MainMenu_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("StartNewGame_Title", "Start New Game")]
    [InlineData("StartNewGame_Players", "Players")]
    [InlineData("StartNewGame_Map", "Map")]
    [InlineData("StartNewGame_Network", "Network")]
    [InlineData("StartNewGame_StartGame", "Start Game")]
    public void GetString_StartNewGame_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("MapConfig_SelectMap", "Select Map")]
    [InlineData("MapConfig_LoadMap", "Load Map from File")]
    [InlineData("MapConfig_GenerateMap", "Generate Map")]
    [InlineData("MapConfig_Width", "Map Width")]
    [InlineData("MapConfig_Height", "Map Height")]
    [InlineData("MapConfig_ForestCoverage", "Forest Coverage")]
    [InlineData("MapConfig_LightWoods", "Light Woods Percentage")]
    [InlineData("MapConfig_HillCoverage", "Hill Coverage")]
    [InlineData("MapConfig_MaxElevation", "Max Elevation")]
    [InlineData("MapConfig_Width_Formatted", "Width: {0} hexes")]
    [InlineData("MapConfig_Height_Formatted", "Height: {0} hexes")]
    [InlineData("MapConfig_ForestCoverage_Formatted", "Forest Coverage: {0}%")]
    [InlineData("MapConfig_LightWoods_Formatted", "Light Woods: {0}%")]
    [InlineData("MapConfig_HillCoverage_Formatted", "Hill Coverage: {0}%")]
    [InlineData("MapConfig_MaxElevation_Formatted", "Max Elevation: {0}")]
    [InlineData("MapConfig_RoughTerrain", "Rough Terrain Coverage")]
    [InlineData("MapConfig_RoughCoverage_Formatted", "Rough Coverage: {0}%")]
    [InlineData("MapConfig_LakeCoverage", "Lake Coverage")]
    [InlineData("MapConfig_LakeMaxDepth", "Max Lake Depth")]
    [InlineData("MapConfig_LakeCoverage_Formatted", "Lake Coverage: {0}%")]
    [InlineData("MapConfig_LakeMaxDepth_Formatted", "Max Lake Depth: {0}")]
    public void GetString_MapConfig_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Network_NetworkSettings", "Network Settings")]
    [InlineData("Network_Multiplayer", "Multiplayer")]
    [InlineData("Network_ServerAddress", "Server Address")]
    [InlineData("Network_ShareAddress", "Share this address with other players to connect")]
    public void GetString_Network_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Players_AddPlayer", "Add Player")]
    [InlineData("Players_AddBot", "Add Bot")]
    [InlineData("Players_EditName", "Edit player name")]
    [InlineData("Players_SaveName", "Save name")]
    [InlineData("Players_Cancel", "Cancel")]
    [InlineData("Players_RemovePlayer", "Remove player")]
    [InlineData("Players_AddUnit", "Add Unit")]
    [InlineData("Players_JoinGame", "Join Game")]
    [InlineData("Players_SetReady", "Set Ready")]
    [InlineData("Players_Aggressiveness", "Aggressiveness")]
    public void GetString_Players_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Window_Title", "MakaMek")]
    public void GetString_Window_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData(PhaseNames.Start, "Start")]
    [InlineData(PhaseNames.Deployment, "Deployment")]
    [InlineData(PhaseNames.Initiative, "Initiative")]
    [InlineData(PhaseNames.Movement, "Movement")]
    [InlineData(PhaseNames.WeaponsAttack, "Weapon Attack")]
    [InlineData(PhaseNames.WeaponAttackResolution, "Weapon Attack Resolution")]
    [InlineData(PhaseNames.PhysicalAttack, "Physical Attack")]
    [InlineData(PhaseNames.Heat, "Heat")]
    [InlineData(PhaseNames.End, "End")]
    public void GetString_PhaseNames_ReturnsExpectedString(PhaseNames phase, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();
        var key = $"Phase_{phase}";

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
}
