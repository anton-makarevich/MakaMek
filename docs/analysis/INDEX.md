# Analysis Documentation Index

Technical analyses, integration studies, design evaluations, and race condition investigations for the MakaMek codebase.

## Bot Analyses

| Document | Summary |
|----------|---------|
| [bot-torso-rotation-evaluation.md](bot-torso-rotation-evaluation.md) | Evaluation of torso rotation decision logic for the AI bot combat system |
| [bot-turnstate-cache.md](bot-turnstate-cache.md) | Analysis of turn-state caching strategy to reduce redundant computation in the bot |
| [movement-decision-guidelines.md](movement-decision-guidelines.md) | Guidelines and heuristics for bot movement decision-making |

## Rules Provider Analyses

| Document | Summary |
|----------|---------|
| [Rules-Provider-Analysis.md](Rules-Provider-Analysis.md) | Software architecture analysis of rules provider design options and trade-offs |
| [Rules-Provider-Analysis-Updated.md](Rules-Provider-Analysis-Updated.md) | Updated rules provider analysis incorporating revised requirements and findings |
| [Rule-Related-PRs.md](Rule-Related-PRs.md) | Consolidated report on the four open rules-provider PRs (#1443, #1450, #1445, #1451) — overlaps, conflicts, and disposition |

## Asset Pipeline Analyses

| Document | Summary |
|----------|---------|
| [mw5-model-extraction.md](mw5-model-extraction.md) | Extracting 3D mech models from MechWarrior 5 (FModel / umodel / Mod Editor), the 2D sprite re-render pipeline, spritesheet animation plan, and the licensing constraints |
| [animated-mech-integration.md](animated-mech-integration.md) | mmux v2 sprite atlas, AS7-D Walk/Run calibration (`distancePerCycle`), and the fixed-5 s distance-driven movement playback prototype |
| [avalonia-3d-renderer-evaluation.md](avalonia-3d-renderer-evaluation.md) | Avalonia-hosted 3D vs MonoGame vs Blender automation for the sprite renderer: API survey with package evidence, comparison, and why Blender automation won (superseded verdict, kept for interactive-3D planning) |

## Reachability Analyses

| Document | Summary |
|----------|---------|
| [reachability-api.md](reachability-api.md) | Analysis of the three-layer reachability API with implementation status of each identified issue |

## UI/UX Analyses

| Document | Summary |
|----------|---------|
| [inputs-patterns.md](inputs-patterns.md) | Analysis of user input patterns in the UI state machine — overlays, action menus, and identified inconsistencies |
| [canvas-optimisations-analysis.md](canvas-optimisations-analysis.md) | Analysis of Avalonia Canvas viewport culling, virtualization, and rendering optimization strategies |
| [presto-animation-capabilities.md](presto-animation-capabilities.md) | Evaluation of the MonacoLabs.Presto animation framework, a capability inventory mapped to MakaMek UI use cases, and the animated unit selection ring pilot design |
