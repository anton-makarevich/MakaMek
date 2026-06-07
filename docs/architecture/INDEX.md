# Architecture Documentation Index

Architectural designs for core game modules, network protocol, AI bot subsystems, and game lifecycle management.

## Core Modules

| Document | Summary |
|----------|---------|
| [Game.md](Game.md) | Core game loop, state machine, and GameManager responsibilities |
| [Map.md](Map.md) | Hex-grid map representation, coordinate systems, and terrain integration |
| [Units.md](Units.md) | Unit model, component structure, equipment, and damage tracking |
| [Components.md](Components.md) | Unit part/component system, slot management, and critical hit allocation |
| [UiStates.md](UiStates.md) | UI state machine, phase-driven screen transitions, and input handling |
| [Movement-Phase-Interrupt-Pattern.md](Movement-Phase-Interrupt-Pattern.md) | Chain-of-handlers + game-action pattern for movement hazards (bridge collapse, skid, water, jump landing) |

## Systems

| Document | Summary |
|----------|---------|
| [Content-Download.md](Content-Download.md) | Content download pipeline, asset management, and update distribution |
| [Game-(Protocol)-High-Level-Architecture.md](Game-(Protocol)-High-Level-Architecture.md) | High-level network protocol design for client-server game communication |
| [MMTX-Terrain-Format.md](MMTX-Terrain-Format.md) | MMTX binary/text terrain file format specification and parser design |

## Bot Architecture

| Document | Summary |
|----------|---------|
| [llm-bot-system-design.md](llm-bot-system-design.md) | Full system design for the LLM-powered AI bot player |
| [llm-deployment-agent-design.md](llm-deployment-agent-design.md) | Design of the LLM deployment agent responsible for unit placement decisions |
| [bot-player-system-implementation-roadmap.md](bot-player-system-implementation-roadmap.md) | Phased implementation roadmap for delivering the bot player system |
| [bot-decision-engines-design-megamek-analysis.md](bot-decision-engines-design-megamek-analysis.md) | Analysis of MegaMek's bot decision engine architectures as design reference |

## Lifecycle

| Document | Summary |
|----------|---------|
| [game-shutdown-lifecycle.md](game-shutdown-lifecycle.md) | Designed shutdown sequence for graceful game session teardown |
| [game-shutdown-edge-cases.md](game-shutdown-edge-cases.md) | Edge cases and failure modes in the game shutdown process |
