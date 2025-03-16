using Shouldly;
using Sanet.MekForge.Core.Services.Localization;

namespace Sanet.MekForge.Core.Tests.Services.Localization;

public class FakeLocalizationServiceTests
{
    [Theory]
    [InlineData("Command_JoinGame", "{0} has joined game with {1} units.")]
    [InlineData("Command_MoveUnit", "{0} moved {1} to {2} facing {3} using {4}.")]
    [InlineData("Command_DeployUnit", "{0} deployed {1} to {2} facing {3}.")]
    [InlineData("Command_RollDice", "{0} rolls")]
    [InlineData("Command_DiceRolled", "{0} rolled {1}.")]
    [InlineData("Command_UpdatePlayerStatus", "{0}'s status is {1}.")]
    [InlineData("Command_ChangePhase", "Game changed phase to {0}.")]
    [InlineData("Command_ChangeActivePlayer", "{0}'s turn.")]
    [InlineData("Command_ChangeActivePlayerUnits", "{0}'s turn to play {1} units.")]
    [InlineData("Command_WeaponConfiguration_TorsoRotation", "{0}'s {1} rotates torso to face {2}")]
    [InlineData("Command_WeaponConfiguration_ArmsFlip", "{0}'s {1} flips arms {2}")]
    [InlineData("Command_WeaponAttackDeclaration_NoAttacks", "{0}'s {1} declares no attacks")]
    [InlineData("Command_WeaponAttackDeclaration_Header", "{0}'s {1} declares attacks:")]
    [InlineData("Command_WeaponAttackDeclaration_WeaponLine", "- {0} targeting {1}'s {2}")]
    [InlineData("Command_WeaponAttackResolution", "{0}'s {1} attacks with {2} targeting {3}'s {4}. To hit number: {5}")]
    [InlineData("Command_WeaponAttackResolution_Hit", "{0}'s {1} hits {3}'s {4} with {2} (Target: {5}, Roll: {6})")]
    [InlineData("Command_WeaponAttackResolution_Miss", "{0}'s {1} misses {3}'s {4} with {2} (Target: {5}, Roll: {6})")]
    [InlineData("Command_WeaponAttackResolution_Direction", "Attack Direction: {0}")]
    [InlineData("Command_WeaponAttackResolution_TotalDamage", "Total Damage: {0}")]
    [InlineData("Command_WeaponAttackResolution_MissilesHit", "Missiles Hit: {0}")]
    [InlineData("Command_WeaponAttackResolution_ClusterRoll", "Cluster Roll: {0}")]
    [InlineData("Command_WeaponAttackResolution_HitLocations", "Hit Locations:")]
    [InlineData("Command_WeaponAttackResolution_HitLocation", "{0}: {1} damage (Roll: {2})")]
    [InlineData("Direction_Forward", "forward")]
    [InlineData("Direction_Backward", "backward")]
    // Attack modifiers
    [InlineData("AttackDirection_Left", "Left")]
    [InlineData("AttackDirection_Right", "Right")]
    [InlineData("AttackDirection_Forward", "Front")]
    [InlineData("AttackDirection_Rear", "Rear")]
    [InlineData("Modifier_GunnerySkill", "Gunnery Skill: +{0}")]
    [InlineData("Modifier_AttackerMovement", "Attacker Movement ({0}): +{1}")]
    [InlineData("Modifier_TargetMovement", "Target Movement ({0} hexes): +{1}")]
    [InlineData("Modifier_Range", "{0} at {1} hexes ({2} range): +{3}")]
    [InlineData("Modifier_Heat", "Heat Level ({0}): +{1}")]
    [InlineData("Modifier_Terrain", "{0} at {1}: +{2}")]
    // Attack information
    [InlineData("Attack_NoLineOfSight", "No LOS")]
    [InlineData("Attack_TargetNumber", "Target ToHit Number")]
    [InlineData("Attack_OutOfRange", "Target out of range")]
    [InlineData("Attack_NoModifiersCalculated", "Attack modifiers not calculated")]
    [InlineData("Attack_Targeting", "Already targeting {0}")]
    [InlineData("Attack_NoAmmo", "No ammunition")]
    // Secondary target modifiers
    [InlineData("Attack_SecondaryTargetFrontArc", "Secondary target (front arc): +{0}")]
    [InlineData("Attack_SecondaryTargetOtherArc", "Secondary target (other arc): +{0}")]
    // Weapon attack actions
    [InlineData("Action_SelectUnitToFire", "Select unit to fire weapons")]
    [InlineData("Action_SelectAction", "Select action")]
    [InlineData("Action_ConfigureWeapons", "Configure weapons")]
    [InlineData("Action_TurnTorso", "Turn Torso")]
    [InlineData("Action_SelectTarget", "Select Target")]
    [InlineData("Action_SkipAttack", "Skip Attack")]
    [InlineData("Action_DeclareAttack", "Declare Attack")]
    // Movement actions
    [InlineData("Action_SelectUnitToMove", "Select unit to move")]
    [InlineData("Action_SelectMovementType", "Select movement type")]
    [InlineData("Action_SelectTargetHex", "Select target hex")]
    [InlineData("Action_SelectFacingDirection", "Select facing direction")]
    [InlineData("Action_StandStill", "Stand Still")]
    [InlineData("Action_MovementPoints", "{0} | MP: {1}")]
    // Movement types
    [InlineData("MovementType_Walk", "Walk")]
    [InlineData("MovementType_Run", "Run")]
    [InlineData("MovementType_Jump", "Jump")]
    // Heat update command strings
    [InlineData("Command_HeatUpdated_Header", "Heat update for {0} (Previous: {1})")]
    [InlineData("Command_HeatUpdated_Sources", "Heat sources:")]
    [InlineData("Command_HeatUpdated_MovementHeat", "  + {0} movement ({1} MP): {2} heat")]
    [InlineData("Command_HeatUpdated_WeaponHeat", "  + Firing {0}: {1} heat")]
    [InlineData("Command_HeatUpdated_TotalGenerated", "Total heat generated: {0}")]
    [InlineData("Command_HeatUpdated_Dissipation", "  - Heat dissipation from {0} heat sinks and {1} engine heat sinks: -{2} heat")]
    [InlineData("Command_HeatUpdated_Final", "Final heat level: {0}")]
    [InlineData("Key_Not_Found", "Key_Not_Found")]
    public void GetString_ReturnsExpectedString(string key, string expected)
    {
        // Arrange
        var localizationService = new FakeLocalizationService();

        // Act
        var result = localizationService.GetString(key);

        // Assert
        result.ShouldBe(expected);
    }
}