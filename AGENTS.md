# MakaMek — Agent Entry Point

This is the **MakaMek** project: a BattleTech tabletop game implementation in C#.

## Documentation

Navigate to [`docs/INDEX.md`](docs/INDEX.md) for the full documentation hierarchy. The index provides one-line summaries and links to every document, organised into these categories: architecture, analysis, project, rules, design, and archive.

## Skills

The `.agents/skills/navigate-docs/SKILL.md` skill teaches efficient documentation discovery using the index hierarchy — read it before searching for documentation to avoid unnecessary file loading.

Available skills in `.agents/skills/`:
- `generate-unit-tests` — generate xUnit tests for a C# class
- `navigate-docs` — locate relevant documentation efficiently via index files

## MCP Tools

Prefer Serena and Jetbrains/Rider tools when available

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, invoke the `skill` tool with `skill: "graphify"` before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
