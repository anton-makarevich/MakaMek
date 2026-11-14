# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Overview

MakaMek is a cross-platform, turn-based tactical BattleTech implementation built with **.NET 10** and **AvaloniaUI**. It is inspired by [MegaMek](https://megamek.org/) but prioritizes simplicity and a mobile-/web-first UX. Runs on Windows, Linux, macOS, Web (WASM), Android, and iOS.

## Build & Test Commands

- **Build the whole solution:** `dotnet build MakaMek.slnx`
- **Run all tests:** `dotnet test MakaMek.slnx`
- **Run one test project:** `dotnet test tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj`
- **Run a single test / filter:** `dotnet test tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj --filter "FullyQualifiedName~SomeTestClass"`
- **Coverage (mirrors CI):**
  ```bash
  dotnet test tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj \
    /p:CollectCoverage=true /p:CoverletOutputFormat=opencover \
    /p:ExcludeByAttribute=GeneratedCodeAttribute /p:Include=[Sanet.MakaMek.Core]*
  ```
  The coverage filter uses the source assembly name (test assembly name minus `.Tests`). See `skills/coverage-check`.
- **Diff-coverage (PR pipeline):** [Cocodif](https://github.com/sanet/Cocodif) runs as a composite GitHub Action in each coverage workflow. It parses the coverlet OpenCover XML, computes `git diff --merge-base` against the PR base branch, and posts a per-file diff-coverage report as a sticky PR comment (one per module). The action is informational only — no `fail-under` gate. Each module workflow passes its own `coverage.opencover.xml`, `include` globs scoped to `src/<Module>/**`, and a unique `comment-marker` so reports don't collide. For local diff-coverage, install the CLI: `dotnet tool install --global Sanet.Cocodif`.
- **Run the desktop app:** `dotnet run --project src/MakaMek.Avalonia/MakaMek.Avalonia.Desktop`
- **Analyzer gate (new/changed files only):** `mise run analyzer-gate`
  Analyzes changed C# files against `origin/main` (override with `ANALYZER_BASE`, or pass explicit files: `mise run analyzer-gate -- src/MakaMek.Core/SomeFile.cs`). CI calls this same task. Analyzers are enabled only for the reusable libraries (`Core`, `Presentation`, `Map`, `Services`, `Assets`, `Localization`, `Bots`); tests, Avalonia, tools, source generators and infra are excluded. Rules and thresholds: root `.editorconfig` (`CA1501`/`CA1502`/`CA1505`/`CA1506`, Sonar `S3776`), `.globalconfig` (`CA1509`), and `CodeMetricsConfig.txt` (`CA1502: 15`, `CA1506: 40`). When `CodeMetricsConfig.txt` changes, the gate validates it via `dotnet build -warnaserror:CA1509`.
  The gate is **baseline-aware**: it runs the same `dotnet format analyzers` command in the working tree and in a `git archive` snapshot of `git merge-base`, then fails only on diagnostics that are new (or worsened) relative to the base. Pre-existing findings in a touched file are reported as "baselined" and do not fail the PR, so there is no reason to add `#pragma warning disable`/`[SuppressMessage]` for pre-existing debt — suppressions in `src/` should be zero. The implementation is the .NET file-based app `tools/analyzer-gate/AnalyzerGate.cs` (no bash/jq dependency, identical behaviour on Ubuntu CI and Windows).
- **Architecture rules:** `mise run test-architecture` — ArchUnitNET checks for the Avalonia → Presentation → Core direction, UiState/command/mechanics placement, and ViewModel base types.

Assembly/root namespaces are prefixed `Sanet.` (e.g. `MakaMek.Core` → `Sanet.MakaMek.Core`), even though project/folder names omit it.

## Testing Conventions

Tests use **xUnit** + **Shouldly** (assertions) + **NSubstitute** (mocking). UI (Avalonia) is intentionally excluded from coverage; presentation logic lives in `MakaMek.Presentation` (ViewModels/UiStates) specifically so it *can* be unit-tested without the UI. Prefer adding logic there over the Avalonia layer.

## Dependency injection

- Do not instantiate shared services, resolvers, calculators, or other collaborators inside a
  class. Inject them through the constructor so the dependency graph is explicit, testable, and
  replaceable.
- Composition roots and factories and the DI registrations
  are the only places that wire the object graph. A service that needs another service must
  receive it, not create it.
- Collections, locks, value objects, and stateless internal policy objects may still be created
  inline. If an internal handler needs a service, inject that service into the handler and pass it
  from the composition root.

## Code style: extensions over static helpers

- Do not add free-standing static helper classes whose methods take a domain type as their first
  parameter. Expose type-specific behavior as extension methods on that type in a
  `<Type>Extensions` class.
- Use the C# 14 extension-block form already used across the codebase
  (`public static class FooExtensions { extension(Foo foo) { ... } }`) and place the extension in
  the layer that owns the added behavior.
- `static` classes remain appropriate for genuine services, factories, and calculators that are
  injected or operate across several unrelated types. The rule targets helpers that only exist to
  graft methods onto one type.
- Example: `MovementPath.GetStopPosition(...)` and `PhysicalAttackDeclarationData.IsMovementAttack()`
  live in `MovementPathExtensions` / `PhysicalAttackDeclarationExtensions`, not in a
  `MovementStopRule` static helper.

## Versioning (required for PRs)

`Directory.Build.props` holds a single `<VersionPrefix>` for all packages. **Every PR that modifies files under `src/` must bump this version** — `pr-version-check.yml` fails the PR if the version is not greater than `main`. Bump it as part of your change.
- The version should only be incremented once per PR
- Test-only, docs-only, or infra-only PRs do not require a version bump.
- Agents may only bump the **patch** segment (e.g. `0.63.10` → `0.63.11`). Never change Major or Minor without explicit human approval.
- Commit messages follow Conventional Commits] (`feat`/`fix`/`docs`/`refactor`/`chore`/`build`, imperative subject).

## Architecture

The codebase is a layered set of projects; dependencies flow **Avalonia → Presentation → Core** (Core has no UI dependencies).

### Layers (`src/`)
- **MakaMek.Core** — Engine and all domain logic: game loop, state machine, phases, commands, units/components, combat & piloting mechanics, dice. No UI. This is where game rules live.
- **MakaMek.Map** — Hex-grid map representation, coordinates, terrain, map generation.
- **MakaMek.Presentation** — ViewModels and **UiStates** (per-phase interaction logic). The testable bridge between Core and the UI. Uses the `Sanet.MVVM` framework (see the `sanet-mvvm` skill before touching ViewModels/navigation/DI).
- **MakaMek.Avalonia** — AvaloniaUI views + per-platform heads (`.Desktop`, `.Android`, `.iOS`, `.Browser`) and shared `.Controls`.
- **MakaMek.Bots** — AI bot framework: per-phase decision engines, plus an experimental LLM-powered bot (agents/tools). Ships as a Docker BotAgent.
- **MakaMek.Services** — Platform-abstraction interfaces (files, images, dispatcher, PDF export) with Avalonia implementations in `MakaMek.Services.Avalonia`.
- **MakaMek.Assets** — 2D asset management (unit/terrain images).
- **MakaMek.Localization** — Localized strings.
- **MakaMek.SourceGenerators** — Roslyn generators that build type registries at compile time (command types, component providers, movement-cost / roll-modifier / PSR-context resolvers). If you add a new command/component/modifier type, the registry is generated — don't hand-maintain a switch.

### Core game model (the "big picture")

Client-server architecture even for local play (RX-based transport locally, SignalR for LAN):
- **`BaseGame`** is the abstract root. **`ServerGame`** owns authoritative state, drives phase transitions, validates and applies commands, and broadcasts updates. **`ClientGame`** submits commands and mirrors state. `GameManager` handles lifecycle/lobby/network/DI wiring.
- **Commands** (`IGameCommand`, split into Client/Server) are the *only* way state changes — they are serialized and sent over the transport (`Services/Transport`) via `CommandPublisher`. State changes propagate through observables + commands.
- **Phases** implement `IGamePhase`, coordinated by `PhaseManager`, in order: Start → Deployment → Initiative → Movement → WeaponsAttack → WeaponAttackResolution → Heat → End. Each transition calls `ResetPhaseState()` on units. `PhysicalAttackPhase` exists but is not yet wired into the flow.
- **Mechanics** (`Models/Game/Mechanics`) are calculator services (`IToHitCalculator`, `IPilotingSkillCalculator`, `IFallProcessor`, critical hits, damage transfer, heat effects, etc.), performed server-side and broadcast for client display.

Two key patterns documented in `docs/architecture/` worth reading before touching those areas:
- **Movement-Phase-Interrupt-Pattern** — chain-of-handlers + game-action for movement hazards (skid, bridge collapse, water, jump landing).
- **Weapon-Attack-Resolution-Pattern** — phase-as-orchestrator + resolver pipeline + optional pre-attack gates (`BuildAttackQueue()`).

### UI interaction model
`MakaMek.Presentation/UiStates` mirrors the game phases: each `IUiState` (DeploymentState, MovementState, WeaponsAttackState, EndState, IdleState) encapsulates that phase's input handling, available actions, and multi-step sub-workflows (e.g. `MovementStep`, `WeaponsAttackStep` enums). This is where phase-specific UX logic belongs.

## Documentation

`docs/INDEX.md` is the single entry point to all docs (architecture, analysis, project, rules, design, archive), each category with its own `INDEX.md`. Game-rule implementations are documented under `docs/rules/`. Load only the specific document you need rather than scanning — see the `navigate-docs` skill. Docs are synced to the GitHub Wiki.

## Assets & Data

Unit/terrain art in `data/` comes from the MegaMek Data Repository (CC BY-NC-SA 4.0), used as-is and distributed separately as downloadable content — do not modify or commit derivatives into the source tree. Mechs are imported from MegaMek's **MTF** format (Level 1 equipment).

## Skills

Repo-local skills in `skills/` (installed to `.agents/skills` via `mise run install-skills`): `sanet-mvvm` (MVVM framework patterns — consult even for small ViewModel/navigation changes), `style-avalonia-app`, `coverage-check`, `generate-unit-tests`, `navigate-docs` (efficient documentation discovery — read before searching docs).

## MCP Tools

Prefer Serena (`serena_initial_instructions`) and Rider tools when available.
