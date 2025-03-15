namespace Sanet.MekForge.Core.Services.Localization;

public class FakeLocalizationService: ILocalizationService
{
    public string GetString(string key)
    {
        return key switch
        {
            "Command_JoinGame" => "{0} has joined game with {1} units.",
            "Command_MoveUnit" => "{0} moved {1} to {2} facing {3} using {4}.",
            "Command_DeployUnit" => "{0} deployed {1} to {2} facing {3}.",
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
            "Direction_Forward" => "forward",
            "Direction_Backward" => "backward",
            
            // Attack direction strings
            "AttackDirection_Left" => "Left",
            "AttackDirection_Right" => "Right",
            "AttackDirection_Forward" => "Front",
            "AttackDirection_Rear" => "Rear",
            
            // Attack modifiers
            "Modifier_GunnerySkill" => "Gunnery Skill: +{0}",
            "Modifier_AttackerMovement" => "Attacker Movement ({0}): +{1}",
            "Modifier_TargetMovement" => "Target Movement ({0} hexes): +{1}",
            "Modifier_Range" => "{0} at {1} hexes ({2} range): +{3}",
            "Modifier_Heat" => "Heat Level ({0}): +{1}",
            "Modifier_Terrain" => "{0} at {1}: +{2}",
            
            // Attack information
            "Attack_NoLineOfSight" => "No LOS",
            "Attack_TargetNumber" => "Target ToHit Number",
            "Attack_OutOfRange" => "Target out of range",
            "Attack_NoModifiersCalculated" => "Attack modifiers not calculated",
            "Attack_Targeting" => "Already targeting {0}",
            "Attack_NoAmmo" => "No ammunition",
            
            // Secondary target modifiers
            "Attack_SecondaryTargetFrontArc" => "Secondary target (front arc): +{0}",
            "Attack_SecondaryTargetOtherArc" => "Secondary target (other arc): +{0}",
            
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
            "Action_StandStill" => "Stand Still",
            "Action_MovementPoints" => "{0} | MP: {1}",
            
            // Movement types
            "MovementType_Walk" => "Walk",
            "MovementType_Run" => "Run",
            "MovementType_Jump" => "Jump",
            
            _ => key
        };
    }
}