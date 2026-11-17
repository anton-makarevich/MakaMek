using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.UiStates;

/// <summary>
/// Movement-attack highlights, enemy-hex handling and declaration descriptions, expressed as
/// extensions over <see cref="MovementState"/>.
/// </summary>
public static class MovementAttackHighlightExtensions
{
    extension(MovementState state)
    {
    public void ClearMovementAttackHighlights()
    {
        if (state._movementAttackCandidates.Count > 0)
        {
            var coordinates = state._movementAttackCandidates.Keys
                .Where(unit => unit.Position != null)
                .Select(unit => unit.Position!.Coordinates)
                .ToHashSet();
            state._viewModel.RemoveHighlight<AttackReachableHighlight>(coordinates);
        }

        state._movementAttackCandidates = new Dictionary<IUnit, MovementAttackCandidate>();
    }

    public bool TryHandleEnemyHex(Hex hex)
    {
        if (state.Game?.PhaseStepState?.ActivePlayer is not { } activePlayer) return false;

        var enemies = state._viewModel.Units
            .Where(unit =>
                unit.Position?.Coordinates == hex.Coordinates
                && unit.Owner?.Id != activePlayer.Id)
            .ToList();
        if (enemies.Count == 0) return false;

        // Several enemies can share a hex; prefer the first one that is a legal target.
        foreach (var enemy in enemies)
        {
            if (!state._movementAttackCandidates.TryGetValue(enemy, out var candidate)) continue;
            state.OpenMovementAttackDeclaration(candidate);
            break;
        }

        return true;
    }

    public void HandlePendingMovementAttackHexSelection(Hex hex)
    {
        if (state._pendingAttackTarget == null) return;

        var otherUnit = state._viewModel.Units.FirstOrDefault(unit =>
            unit.Position?.Coordinates == hex.Coordinates
            && unit.Id != state._pendingAttackTarget.Id);
        if (otherUnit == null) return;

        if (state._movementAttackCandidates.TryGetValue(otherUnit, out var candidate))
            state.OpenMovementAttackDeclaration(candidate);
    }

    public IReadOnlyList<string> BuildMovementAttackDescription(
        IUnit target,
        PhysicalAttackDeclarationData declaration)
    {
        var lines = new List<string>();
        if (state._selectedUnit == null || state.Game == null) return lines;

        var hexesMoved = declaration.HexesMoved ?? 0;
        lines.Add(string.Format(state.GetString("PhysicalAttackDeclaration_MovementHexesMoved"), hexesMoved));

        var rules = state.Game.RulesProvider;
        if (declaration.AttackType == PhysicalAttackType.Charge)
        {
            lines.Add(string.Format(
                state.GetString("PhysicalAttackDeclaration_MovementDamage_Target"),
                rules.GetChargeDamage(state._selectedUnit.Tonnage, hexesMoved)));
            lines.Add(string.Format(
                state.GetString("PhysicalAttackDeclaration_MovementDamage_Attacker"),
                rules.GetChargeCollisionDamage(target.Tonnage)));
            lines.Add(state.GetString("PhysicalAttackDeclaration_ChargePsr"));
            lines.Add(state.GetString("PhysicalAttackDeclaration_ChargeMiss"));
        }
        else
        {
            lines.Add(string.Format(
                state.GetString("PhysicalAttackDeclaration_MovementDamage_Target"),
                rules.GetDfaDamage(state._selectedUnit.Tonnage)));
            lines.Add(string.Format(
                state.GetString("PhysicalAttackDeclaration_MovementDamage_Attacker"),
                rules.GetDfaAttackerDamage(state._selectedUnit.Tonnage)));
            lines.Add(state.GetString("PhysicalAttackDeclaration_DfaPsr"));
            lines.Add(state.GetString("PhysicalAttackDeclaration_DfaMiss"));
        }

        lines.Add(state.GetString("PhysicalAttackDeclaration_MovementFacing"));
        lines.Add(state.GetString("PhysicalAttackDeclaration_NoWeaponOrPhysicalAttacks"));
        return lines;
    }

    public string GetMovementAttackActionLabel()
    {
        var attackType = state._pendingAttackDeclaration?.AttackType;
        return attackType switch
        {
            PhysicalAttackType.Charge => state._viewModel.LocalizationService.GetString("Action_DeclareCharge"),
            PhysicalAttackType.DFA => state._viewModel.LocalizationService.GetString("Action_DeclareDfa"),
            _ => state._viewModel.LocalizationService.GetString("Action_ConfirmMovementAttack")
        };
    }
    }
}
