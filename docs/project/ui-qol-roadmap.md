# UI/UX Quality-of-Life Roadmap

This roadmap records improvements identified during repository-wide UI/UX review passes. Items are intentionally scoped around helping players understand the current game state and make fast tactical decisions.

## Status

### Completed

- Initiative outcome visibility: show the winner, roll, and winner color after initiative resolves.
- Initiative progress visibility: show how many initiative rolls have been received.
- Turn ownership guidance: distinguish “your turn” from waiting for another player.
- Turn progress guidance: show remaining units and end-turn guidance.
- Responsive action zones: separate mobile map controls, phase actions, and the squad strip.

### In progress

None.

### Planned

1. ~~Complete the manual initiative workflow~~ — completed in the current UI-overhaul work and covered by the full solution test run.
   - Provide a dedicated initiative state instead of the generic idle/wait state.
   - Expose a clear roll-initiative action to the active human player.
   - Show pending/waiting feedback while other players roll.
   - Preserve the final result and tie/reroll clarity.

2. ~~Improve turn action clarity~~ — completed with phase/action ownership, active-unit, and command-pending guidance.
   - Make the current phase objective, active unit, and primary action visually dominant.
   - Surface command acknowledgement and pending-action feedback.

3. ~~Improve panel management~~ — completed with coordinated panel ownership in `BattleMapViewModel`.
   - Prevent command log, map settings, unit information, and targeting panels from competing for space.
   - Use a single-panel or explicitly coordinated overlay model on small screens.

4. ~~Finish accessibility and localization cleanup~~ — initial tactical-control pass completed.
   - Replace remaining hardcoded labels, tooltips, and automation names.
   - Verify keyboard focus order and screen-reader descriptions for tactical controls.

5. ~~Improve connection and command feedback~~ — completed with rejected-command, live-pending, timeout, and LAN/online retry feedback.
   - Add retry/reconnect affordances.
   - Make rejected, pending, and timed-out commands visible without requiring the command log.

6. ~~Improve combat decision support~~ — completed with per-weapon hit probabilities, restriction explanations, an always-visible selected-attack resource summary, and a map overlay for firing arcs, range bands, and target-specific expected damage.
   - Explain unavailable weapons and invalid attack choices.
   - Surface hit probability, heat, and ammunition consequences before commitment.

7. Improve deployment guidance.
   - Show deployment boundaries, remaining units, valid placement feedback, and readiness state.
