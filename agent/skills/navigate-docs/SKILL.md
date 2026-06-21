---
description: "Locate relevant project documentation efficiently using the index hierarchy. Use this skill whenever asked to \"find documentation\", \"look up docs\", \"what docs cover X\", or \"navigate to documentation about\" a topic. This skill avoids unnecessary token consumption by loading indexes before full documents and incrementally drilling down to only the relevant files."
---
# Navigate Docs

Locate relevant MakaMek documentation efficiently by traversing the index hierarchy — root index → category index → specific document — rather than bulk-loading files or scanning directories.

## Context Validation Checkpoints

- Is the topic or query clear enough to identify a relevant documentation category?
- Does `docs/INDEX.md` exist and is it accessible?
- Can the query be mapped to one of the known categories: `architecture`, `analysis`, `project`, `rules`, `design`, `archive`?
- If any checkpoint cannot be resolved with confidence, stop and ask the user for clarification before proceeding.

## Implementation Steps

### Step 1: Read Root Index
Load `docs/INDEX.md` to understand the available categories and their one-line descriptions. This is always the first step — never skip directly to a category or document.

### Step 2: Identify Relevant Category
Based on the query, determine which category is most likely to contain relevant information:
- **architecture** — how systems and modules are designed
- **analysis** — technical investigations, trade-off studies, race conditions, integration analyses
- **project** — requirements (PRDs), gap analyses, project planning
- **rules** — BattleTech game rules implementation
- **design** — UI/UX, colour schemes, visual design
- **archive** — historical or superseded documents

### Step 3: Load Category Index
Navigate to the identified category's `INDEX.md` (e.g. `docs/architecture/INDEX.md`) and read the document list and summaries. Do not load full documents yet.

### Step 4: Select Specific Documents
Identify which specific document(s) match the query based on the summaries in the category index. Select only the most relevant files — prefer precision over coverage.

### Step 5: Load Target Documents
Load only the identified relevant documents. Avoid loading entire categories or multiple large documents simultaneously to conserve context.

### Step 6: Cross-Reference If Needed
If the initial category does not contain the answer, return to `docs/INDEX.md` and try an adjacent category. For example, a topic that seems architectural may have a deeper analysis in `analysis/`, or a planning decision may live in `project/`.

> **Prefer**: loading indexes before full documents, and loading documents incrementally rather than in bulk.
> **Avoid**: scanning the filesystem directly or loading all documents in a category hoping to find the right one.
