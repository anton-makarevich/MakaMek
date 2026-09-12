# AGENTS.md — src/MakaMek.Avalonia

Scoped guidance for AI coding agents working under `src/MakaMek.Avalonia` (the shared Avalonia UI project and all platform heads). The root `AGENTS.md` still applies.

## Avalonia documentation MCP

Before writing or editing any AXAML or Avalonia-specific C# in this folder, load and follow the `avalonia-ui` skill and use the `avalonia-docs` MCP tools it describes.

Why: Avalonia markup fails silently at runtime when written from stale memory (broken bindings, wrong selectors), and this MCP serves the current official docs. At minimum, call `avalonia-docs_get_avalonia_expert_rules` once per session before touching UI code, and search the docs whenever unsure about a control property, style selector, or binding syntax.
