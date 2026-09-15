# MakaMek local bug inventory

> Local working document. Do not commit yet.
>
> This inventory was created on 2026-09-15 from branch `fix/695-conditional-join-broadcast`.
> Findings are static-scan candidates until fixed and covered by regression tests. Recheck each
> item against the current branch before acting because later issue work may resolve it.

## Scan record

Five repository-wide scans were completed:

1. Command/state mutation and lifecycle flow.
2. Null handling, exceptions, degraded paths, and suspicious LINQ usage.
3. Serialization, deserialization, and external-data boundaries.
4. Concurrency, async operations, cancellation, and disposal.
5. Test/build/documentation gaps, TODOs, unimplemented paths, and targeted suspicious patterns.

The available solution test sweep passed during the scan: Core 3,040 and Map 882.

## High-confidence findings

### BUG-001 — rejected commands consume idempotency keys

- Severity: Medium
- Area: server command validation / retries
- Location: `src/MakaMek.Core/Models/Game/ServerGame.cs:125-162`
- Evidence: `_processedCommandKeys.TryAdd(...)` runs before `ValidateCommand(command)`. A command
  with an invalid payload is rejected, but its key remains recorded. Retrying the corrected request
  with the same idempotency key then receives `DuplicateCommand` instead of being validated again.
- Impact: transient or client-side validation failures can make a legitimate retry impossible until
  the phase key set is cleared.
- Suggested fix: define the intended idempotency contract, then either reserve keys only after
  validation succeeds or remove the key when validation fails. Add tests for invalid-then-corrected
  retries with the same key and for concurrent duplicate requests.
- Status: Fixed locally in BUG-001 work: invalid commands release their reservation so a corrected
  retry can reuse the same key, while valid duplicate-key precedence remains unchanged. Regression
  coverage is present; the inventory entry remains uncommitted by design.

### BUG-002 — malformed command JSON can deserialize to null instead of failing

- Severity: Medium
- Area: transport deserialization boundary
- Location: `src/MakaMek.Core/Data/Serialization/Converters/GameCommandJsonConverter.cs:21-24`
- Evidence: `Read` returns `null` when the JSON token is not `StartObject`, despite implementing a
  converter for `IGameCommand`. The transport boundary can therefore pass a null command to code
  that expects an `IGameCommand`, producing a later null-reference or silent drop rather than a
  protocol error.
- Suggested fix: throw `JsonException` for every invalid root token, and add tests for `null`, array,
  string, number, and empty JSON inputs. Confirm the transport adapter converts the exception into a
  controlled connection/message failure.
- Status: Fixed locally in BUG-002 work: non-object command JSON roots now throw `JsonException`
  instead of returning `null`, with coverage for null, array, string, number, and empty inputs.
  The transport adapter's controlled failure path remains covered; the inventory entry remains
  uncommitted by design.

### BUG-003 — weapon attack payloads assume at least one assignment

- Severity: Medium
- Area: command validation / combat presentation
- Locations:
  - `src/MakaMek.Core/Models/Game/Phases/WeaponAttackResolutionPhase.cs:83`
  - `src/MakaMek.Presentation/ViewModels/BattleMapViewModel.cs:448-482`
- Evidence: both paths index weapon assignment collections with `[0]`/`First()` without checking
  that the serialized command contains an assignment. A malformed, stale, or incompatible command
  can throw `IndexOutOfRangeException`/`InvalidOperationException` while processing combat.
- Impact: one bad attack command can abort server resolution or break the client combat-log/map
  update path.
- Suggested fix: validate weapon assignments at the command boundary and make both consumers handle
  empty assignments defensively. Add server and presentation tests for empty and missing assignment
  payloads.
- Status: Fixed locally in #299 work: server validation rejects weapon targets without assignments,
  the resolution queue skips malformed stale payloads, and both Core and Presentation paths have
  regression coverage. The inventory entry remains uncommitted by design.

### BUG-004 — remote resource loading accepts URLs outside the discovered resource set

- Severity: Medium
- Area: remote asset loading / input boundary
- Location: `src/MakaMek.Assets/ResourceProviders/RemoteResourceStreamProvider.cs:82-124`
- Evidence: `GetResourceStream` accepts any non-empty `resourceId`. It looks up the ID in the
  discovered listing, but even when no match exists it continues with a default empty hash and
  calls `CreateRequest(resourceId)`, allowing an arbitrary URL to be fetched by the provider.
- Impact: callers that can supply a resource ID may turn an asset provider into an unintended URL
  fetcher, bypassing the provider's allowlisted listing and cache/version assumptions.
- Suggested fix: reject IDs that are not present in the loaded listing, or explicitly document and
  secure arbitrary URL support. Add tests for an unknown URL and for a URL with a different host.
- Status: Fixed locally in BUG-004 work: remote providers now reject resource IDs absent from
  the discovered listing before cache lookup or HTTP download. Regression coverage verifies an
  unknown external URL is not requested, while listing-backed downloads and offline cache paths
  remain covered; the inventory entry remains uncommitted by design.

### BUG-005 — movement-path cache is not safe for concurrent map queries

- Severity: Medium
- Area: map pathfinding / concurrency
- Location: `src/MakaMek.Map/Models/MovementPathCache.cs:8-38`
- Evidence: `MovementPathCache` uses a mutable `Dictionary` without synchronization, while the map
  exposes pathfinding services used by UI and bot/game flows that can run asynchronously. Concurrent
  `Get`, `Add`, `Invalidate`, or `Clear` operations can race and corrupt or throw from the cache.
- Suggested fix: establish whether `IBattleMap` pathfinding is single-threaded by contract. If not,
  use a lock or concurrent structure and add parallel query/invalidation tests. Preserve cache
  invalidation semantics while hardening it.
- Status: Fixed locally in BUG-005 work: all movement-path cache operations are synchronized,
  including atomic invalidate enumeration/removal, with concurrent stress coverage. The inventory
  entry remains uncommitted by design.

### BUG-006 — several heat effects bypass the rules-provider abstraction

- Severity: Medium
- Area: rules customization / heat mechanics
- Locations:
  - `src/MakaMek.Core/Models/Units/Mechs/Mech.cs:244-268`
  - `src/MakaMek.Core/Models/Game/Rules/IRulesProvider.cs`
- Evidence: life-support pilot damage uses fixed thresholds `15` and `26`, and movement heat
  penalties use fixed thresholds `5`, `10`, `15`, `20`, and `25`. These values are applied directly
  by `Mech`, while related heat shutdown, ammo explosion, movement heat, and dissipation values use
  `IRulesProvider`.
- Impact: custom rules providers cannot change all heat consequences consistently; behavior can
  diverge between projected/UI values and authoritative resolution.
- Suggested fix: confirm the intended Total Warfare constants, add provider methods for configurable
  thresholds/penalties, and cover a custom provider end to end. This may overlap future rules
  unification issues and should be coordinated before implementation.
- Status: Deferred after consumer audit: fixing this correctly requires a shared rules-aware heat
  modifier service or explicit rule context in the parameterless IUnit movement/attack APIs. A
  partial IRulesProvider expansion would leave UI, bots, and authoritative calculations divergent.

## Lower-confidence boundary candidates

### BUG-007 — map-data factory trusts external shape and dimensions

- Severity: Low/Medium
- Area: map import and network/file boundary
- Location: `src/MakaMek.Map/Factories/BattleMapFactory.cs:36-57`
- Evidence: `CreateFromData` dereferences `mapData`, assumes `Coordinates` and `Terrains` are
  present, and derives dimensions only from maximum Q/R values. It does not reject null data,
  malformed coordinates, duplicate hexes, or negative/non-contiguous dimensions.
- Impact: malformed imported or remote map data can throw or construct a map whose dimensions do not
  match its contents.
- Suggested validation: inspect the map-data contract and existing import expectations first; then
  add a focused validation policy and tests rather than silently changing accepted map formats.
- Status: Fixed locally in BUG-007 work: map import now rejects null entries, missing collections,
  non-positive coordinates, and duplicate coordinates while preserving sparse valid maps. Regression
  coverage is present; the inventory entry remains uncommitted by design.

### BUG-008 — command subscriber registration is not idempotent

- Severity: Low/Medium
- Area: lifecycle and event subscription
- Location: `src/MakaMek.Core/Services/Transport/CommandPublisher.cs:48-62`
- Evidence: subscribing the same delegate twice appends it twice, while adding a transport mapping
  uses dictionary `Add`, which throws if that delegate already has a mapping. Current callers often
  unsubscribe first, but an exception or re-entrant initialization could still expose this seam.
- Suggested validation: add a repeated-subscribe lifecycle test. Decide whether duplicate
  subscriptions should be rejected explicitly, ignored, or represented as separate subscriptions;
  then make the implementation and tests agree.
- Status: Fixed locally in BUG-008 work: duplicate delegate registrations are now ignored, the
  first transport scope is preserved, and duplicate registration regression coverage is present.
  The inventory entry remains uncommitted by design.

## Cross-check protocol for future issue work

Before implementing a GitHub issue:

1. Compare the issue's files and data paths against every inventory item.
2. Add a regression test for any inventory item touched by the change, even if the issue does not
   explicitly mention it.
3. Re-run all five scan categories against the changed area.
4. Mark an item resolved only after the fix, focused regression tests, and the full available test
   sweep pass.
5. Keep this file uncommitted until the project owner decides to publish the inventory.
