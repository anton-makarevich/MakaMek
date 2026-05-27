# Command Race Condition Analyses and Extension

Extends proposed soutions in `command-race-condition-analysis.md`:
```text
All client commands to introduce IdempotencyKey (or CommandId) which is a combined hash of PlayerId, UnitId, Phase, Turn, and CommandType. In this case every unit will be able to send only one command of a type per phase of a turn. 1. On the server (extension of solution1) we can keep a HashSet of these keys of commands received, if a duplicate - we should not proceed but reply with a new ErrorCommand. 2. On the client we can implement Solution 2 but use new key instead of AttackerId.
```
---

## 🔍 1. Summary of AI Proposals

| Solution                            | Core Idea                                                                                          | Strength                                                            | Weakness                                                                                                               |
| ----------------------------------- | -------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **1. Server-side idempotency**      | Check `HasDeclaredWeaponAttack` on the server and make `DeclareWeaponAttack()` safe to call twice. | Simple, authoritative, low-risk.                                    | Does not detect true duplicates across multiple types or phases. No concept of “already processed this exact command.” |
| **2. Client-side pending tracking** | Maintain a client-side set of “commands in flight” to prevent duplicates being sent.               | Prevents most duplicates before they reach the server; improves UX. | Relies on local state sync and server feedback.                                                                        |
| **3. Command sequence numbers**     | Add a monotonically increasing sequence per player/unit to detect out-of-order/duplicate.          | Handles ordering and duplicates across unreliable networks.         | More complex; needs persistent counters.                                                                               |
| **4. Hybrid (1 + 2)**               | Defense in depth: prevent on client, reject on server.                                             | Best practical combination.                                         | Still uses implicit semantics (“unit already acted”) instead of explicit deduplication keys.                           |

---

## 💡 2. Your Proposal: Explicit **IdempotencyKey**

You propose introducing a **CommandId / IdempotencyKey**:

> Deterministically computed as a hash of `(PlayerId, UnitId, Phase, Turn, CommandType)`.

and then:

1. **Server side:** Maintain a `HashSet<IdempotencyKey>` of processed commands and reject duplicates (responding with `ErrorCommand`).
2. **Client side:** Use the same key for pending tracking (instead of just `AttackerId`).

---

## ✅ 3. Strengths of Your Proposal

### 3.1. Explicit, Extensible Idempotency Layer

* Unlike checking `HasDeclaredWeaponAttack`, this makes *deduplication an explicit protocol concern*, not hidden in game logic.
* Can be reused across **all command types**, not just weapon attacks.

### 3.2. Phase/Turn Context Awareness

* Including `(Phase, Turn)` ensures the same unit can issue commands again in a later phase without stale duplicates being blocked.

### 3.3. Stateless Server Idempotency

* Because the key is *deterministic*, the server doesn’t need per-player sequence tracking or persistent counters — only a short-lived set per game context.

### 3.4. Enables Error Feedback

* Returning an explicit `ErrorCommand` improves UX and allows UI to re-enable buttons or retry safely.

### 3.5. Client & Server Alignment

* Both sides derive the same key; the client can *know* whether a command is “still pending” or “already rejected”.

### 3.6. Security/Robustness

* Prevents malicious or buggy clients from replaying commands for prior turns or phases.

---

## ⚠️ 4. Potential Gaps and Edge Cases

| Area                                 | Potential Gap                                                                                                                                                 | Discussion / Mitigation                                                                                                                                                                                                                       |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A. Key uniqueness scope**          | Hash collision or ambiguous scope if different games or sessions reuse same `Turn/Phase` numbers.                                                             | Include `GameId` (or `MatchId`) in the hash. That guarantees uniqueness across concurrent games.                                                                                                                                              |
| **B. Phase transitions**             | Late-arriving commands from a previous phase (due to latency) might still be considered valid if server hasn't switched yet.                                  | Tie validation strictly to *server’s authoritative phase/turn*. Reject if the key’s phase/turn don’t match current server state.                                                                                                              |
| **C. Command variation**             | Two logically different commands of the *same type* for same unit/phase (e.g., attack with different targets) will hash to same key and be silently rejected. | This is intentional (idempotency) but must be clearly defined — “only one of this command type per phase allowed.” If the design later needs “multiple per phase” (e.g., `FireWeaponCommand` for each weapon), the key structure must change. |
| **D. Memory retention / cleanup**    | If the server keeps all IdempotencyKeys indefinitely, the set could grow.                                                                                     | Maintain them per game phase or with expiry — e.g., clear set when moving to next phase/turn.                                                                                                                                                 |
| **E. Client desync**                 | If the client’s understanding of `Turn` or `Phase` is stale, it may generate wrong key and send invalid command.                                              | Ensure phase/turn come from server-authoritative state, not local prediction. Possibly include them in each broadcast update.                                                                                                                 |
| **F. Deterministic hash collisions** | Depending on hash algorithm, minimal but possible collision risk.                                                                                             | Use a UUIDv5 (namespace-based) or strong SHA-256 and truncate to GUID-sized ID; collision risk becomes negligible.                                                                                                                            |
| **G. Acknowledgment timing**         | The proposal still doesn’t guarantee the client knows *when* command was accepted/rejected.                                                                   | Combine with ACK/NACK messages (Solution 4 phase 4). The ErrorCommand covers NACK partially; you could extend it to include `IdempotencyKey` to correlate.                                                                                    |

---

## 🧩 5. Integration with AI Solutions

| Layer                        | Existing Solution                       | How IdempotencyKey Integrates                                                                                                                       |
| ---------------------------- | --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Server validation**        | Solution 1 (idempotency via unit state) | Replace or complement `HasDeclaredWeaponAttack` check with `processedCommandKeys.Contains(key)`. Adds flexibility across command types.             |
| **Client pending**           | Solution 2                              | Replace `AttackerId` set with `pendingCommandKeys`. Handles more cases (e.g., multiple command types).                                              |
| **Sequence numbers (Sol 3)** | Can be dropped                          | The key itself encodes temporal ordering through `(Turn, Phase)`. Sequence numbers unnecessary unless you want *ordering* not just *deduplication*. |
| **Hybrid (Sol 4)**           | Stronger together                       | You now have deterministic deduplication (your key) *plus* optimistic local prevention (pending tracking) *plus* server rejection (truth).          |

---

## 🧠 6. Implementation Recommendations

1. **Key computation**

   ```csharp
   public static Guid ComputeIdempotencyKey(Guid gameId, Guid playerId, Guid unitId, GamePhase phase, int turn, Type commandType)
   {
       using var sha = SHA256.Create();
       var input = $"{gameId}:{playerId}:{unitId}:{phase}:{turn}:{commandType.Name}";
       var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
       return new Guid(hash[..16]);
   }
   ```

   Deterministic and compact.

2. **Server tracking**

   ```csharp
   private readonly HashSet<Guid> _processedCommandKeys = new();

   private bool ValidateCommand(IClientCommand cmd)
   {
       if (!_processedCommandKeys.Add(cmd.IdempotencyKey))
       {
           SendErrorCommand(cmd.IdempotencyKey, "Duplicate command");
           return false;
       }
       ...
   }
   ```

3. **Client tracking**

   ```csharp
   private readonly HashSet<Guid> _pendingCommandKeys = [];

   public void SendCommand(IClientCommand cmd)
   {
       if (_pendingCommandKeys.Contains(cmd.IdempotencyKey)) return;
       _pendingCommandKeys.Add(cmd.IdempotencyKey);
       Publish(cmd);
   }
   ```

4. **Lifecycle management**

    * Clear server `_processedCommandKeys` when phase or turn increments.
    * Clear client `_pendingCommandKeys` on ACK or ErrorCommand.

---

## 🧾 7. Comparison Summary

| Feature                        | AI Hybrid (Sol 4)           | Your IdempotencyKey              |
| ------------------------------ | --------------------------- | -------------------------------- |
| Duplicate detection            | Implicit via unit state     | Explicit via deterministic key   |
| Works across all command types | Needs manual per-type logic | Yes, generic                     |
| Network robustness             | Medium                      | High                             |
| Implementation complexity      | Low-medium                  | Medium (key generation, storage) |
| Error feedback                 | Optional                    | Built-in with ErrorCommand       |
| Long-term scalability          | Good                        | Excellent                        |

---

## 🏁 8. Conclusion

Your **IdempotencyKey-based** approach is a **strong architectural improvement** and a **natural generalization** of AI’s hybrid solution.

✅ **Keeps benefits** of both client and server safeguards.
✅ **Adds explicit, cross-type deduplication**.
✅ **Scales cleanly** with more command types and future network patterns.

**Gaps are minor and manageable**:

* include `GameId` to avoid collisions,
* clear key sets at phase boundaries,
* use server-authoritative phase/turn values,
* optionally add ACK/NACK correlation for user feedback.

If you implement this, it effectively subsumes AI’s Solution 1 + 2 while providing a foundation for later extensions (e.g., retries, persistence, replay protection).
