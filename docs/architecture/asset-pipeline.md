# Asset Pipeline — Model Import & Sprite Rendering

Design for a two-stage, fully automated, CLI/MCP-driven asset pipeline:

1. **Import** — any 3D source (MechWarrior 5, other games, a model from the internet) → one **canonical model** containing normalised geometry, skeleton, materials and the animation clips the board needs.
2. **Render** — canonical model → sprites and spritesheets consumed by MakaMek.

The canonical model from stage 1 is also the asset format for the planned **native 3D client**, so stage 1 is not disposable tooling — it is the long-term asset contract. The renderer engine question (MonoGame vs Avalonia-hosted) stays open by design; see [../analysis/avalonia-3d-renderer-evaluation.md](../analysis/avalonia-3d-renderer-evaluation.md).

No UI-only tools are part of the pipeline. Every step is a headless process that can be scripted, batched, and exposed to an agent.

Date: 2026-10-25 · Status: design proposal · Related: [../analysis/mw5-model-extraction.md](../analysis/mw5-model-extraction.md)

---

## 1. Why two stages

The temptation is a single "MW5 → PNG" script. It fails for four reasons:

| Problem with a fused pipeline | Consequence of splitting |
|---|---|
| Every new source (MW5, Unity game, Sketchfab FBX) would need its own render code | Sources only implement one interface: "produce a canonical model" |
| Onboarding a source is coupled to the render look | The look is one preset file, applied to every source |
| The native 3D version would need a *third* asset format | Stage 1 output *is* the runtime 3D format |
| A bad source silently produces bad sprites | Stage 1 validates and records warnings; stage 2 renders whatever it is given, deterministically |

The contract between the stages is the whole design. Everything else is an adapter or a preset.

---

## 2. The canonical model (stage 1 output)

### 2.1 Files

```
<asset-id>/
  model.glb            # geometry + skin + skeleton + animations + materials + embedded textures
  model.json           # manifest: provenance, conventions, clip table, metrics, warnings
  textures/            # only when --external-textures is used
```

`.glb` + JSON, nothing bespoke. Reasons:

- glTF 2.0 carries skinning, skeletal animation, PBR materials and embedded textures in one file — no format invention.
- It is the best-supported C# path into both plausible runtime engines: `SharpGLTF` (read/write) plus `SharpGLTF.Runtime.MonoGame` / `MonoGame.AnimAndPBR` for MonoGame, and `SharpGLTF` again for an Avalonia-hosted renderer; the MonoGame content pipeline can also consume `.glb` through `OpenAssetImporter`.
- It is readable by Blender/three.js/anything, so a human escape hatch exists without adding a dependency to the pipeline.

### 2.2 Canonical conventions

These are non-negotiable and enforced by the normalizer (`validate` fails otherwise):

| Convention | Value |
|---|---|
| Units | metres, `1 glTF unit = 1 m` |
| Axes | glTF standard: right-handed, **Y-up** |
| Facing | mech faces **+Z**, "up" is +Y (no per-source exceptions; adapter rotates) |
| Origin / pivot | on the ground plane, at the **footprint centre** (not the bounding-box centre — that drifts between poses) |
| Mesh | triangulated, welded, normals + tangents + UV0, ≤ 4 bone influences per vertex |
| Rest pose | bind pose = the source's rest pose; T-pose is *not* required (mechs have no arms-out pose) |
| Materials | PBR kept as authored, but exactly one material property, `extras.boardLook`, names the sprite material profile |
| Animation fps | resampled to **30 fps**, linear interpolation |
| Scene | exactly one scene, no lights/cameras/empties exported (stage 2 owns those) |

### 2.3 Canonical clips

The game only needs a fixed vocabulary — so the pipeline defines it and every source is mapped onto it:

| Clip | Loop | Required | Used by |
|---|---|---|---|
| `Idle` | ✔ | yes | default board state |
| `Walk` | ✔ | yes | `MovementType.Walk` |
| `Run` | ✔ | yes | `MovementType.Run` |
| `Jump` | ✔ | no | `MovementType.Jump` |
| `ProneEnter` | ✘ | yes | falling / knocked down |
| `ProneIdle` | ✔ | yes | `UnitStatus.Prone` |
| `StandUp` | ✘ | yes | standing up |
| `Shutdown` | ✘ | no | `UnitStatus.Shutdown` |
| `Destroyed` | ✘ | no | `IsOutOfCommission` |

`model.json` records, per clip, **how it was satisfied** — this is the key to "regardless of source":

```jsonc
{
  "name": "ProneEnter",
  "source": "synthesized",          // extracted | aliased | synthesized | absent
  "origin": "Idle",                  // clip the synthesizer started from
  "rule": "root-pitch-85@6f",        // named rule from clips.rules.json
  "frames": 6, "fps": 30, "loop": false, "hold": "last"
}
```

### 2.4 Clip resolution order

For every canonical clip, in order:

1. **Extracted** — the adapter found an animation matching the canonical name (or a declared alias).
2. **Aliased** — adapter-specific pattern matched (e.g. UE `Anim_Mech_Walk_Fwd` → `Walk`). Aliases live in the adapter, not in shared code.
3. **Synthesized** — a declarative rule in `clips.rules.json` derives the clip from another clip by transform: e.g. `ProneEnter = Idle + root pitch 85° over 6 frames + sink 0.4 m`, `StandUp = ProneEnter reversed`. This is how MW5 gets prone/stand-up at all — it has no knockdown animations.
4. **Absent** — nothing could satisfy it; the manifest records `absent` and stage 2 renders the static frame as the fallback. Never fails the build.

Because step 3 is data, adding a new "cheat" animation for a whole family of sources is a JSON edit, not a code change.

### 2.5 Manifest (`model.json`)

```jsonc
{
  "schema": 1,
  "id": "AS7",                       // chassis code, not the variant
  "source": {
    "adapter": "unreal", "kind": "pak",
    "uri": "MW5Mercs-WindowsNoEditor.pak#/Game/Objects/Mechs/Atlas/...",
    "gameVersion": "4.26", "sha256": "…",
    "license": "PGI/Microsoft — not redistributable"
  },
  "metrics": {
    "heightMeters": 15.2, "footprintDiameterMeters": 11.4,
    "boundsMin": [-5.7, 0, -4.1], "boundsMax": [5.7, 15.2, 4.1],
    "triangles": 41200, "bones": 63, "materials": 3
  },
  "tonnage": 100,
  "volume": {
    "method": "voxel", "grid": 64,
    "modelCubicMeters": 1520.4,
    "targetCubicMeters": 666.7,
    "densityTonsPerCubicMeter": 0.15,
    "correctionScale": 0.7597,          // (target/model)^(1/3), applied at render time
    "includesAttachments": true,
    "canonicalLoadout": ["arm_ra_std", "ac20"]
  },
  "planView": {
    "silhouetteAreaSquareMeters": 96.3,
    "coverageAtReferenceScale": 0.94      // silhouette area / hex area, before overrides
  },
  "conventions": { "units": "m", "up": "+Y", "forward": "+Z", "pivot": "footprint-centre" },
  "animations": [ /* clip table above */ ],
  "attachments": [ { "socket": "hardpoint_ra", "mesh": "…" } ],  // optional arms/weapons
  "warnings": [ "no Jump clip", "materials flattened to boardLook=simple" ],
  "pipeline": { "version": "0.1.0", "generatedAt": "…", "deterministic": true }
}
```

`id` is the **chassis** code; variants (`AS7-D`, `AS7-K`) share one canonical model. A `chassis-map.json` in the pipeline config maps source identifiers (`Atlas`) to game codes (`AS7`).

---

## 3. Stage 1 — `import`

### 3.1 Source adapters

One interface, many implementations. All headless, all C#:

```csharp
public interface IModelSource
{
    string Name { get; }                                  // "unreal", "gltf", "assimp"
    Task<SourceProbeResult> ProbeAsync(string uri, CancellationToken ct);
    Task<RawModel> ReadAsync(SourceRequest request, CancellationToken ct);
}
```

| Adapter | Library | Covers | Notes |
|---|---|---|---|
| `gltf` | SharpGLTF | `.gltf`, `.glb` | Baseline; also the canonical writer. Any internet source that ships glTF goes here directly |
| `assimp` | AssimpNet (Assimp 5) | FBX, OBJ, DAE, STL, 3DS, LWO, MD5… | The catch-all for asset stores and hobbyist exports. FBX is what most shops ship |
| `unreal` | CUE4Parse | MW5, other UE4/UE5 games | `.pak`/`.utoc`, skeletal meshes, skeleton, AnimSequences, materials. AES key from config, never hard-coded |
| `unity` | AssetsTools.NET | Unity games / asset bundles | Same C# ecosystem, large coverage gain for a small adapter |
| `cryengine` | — | MWO (`MWO.pak`, `.cgf/.chr/.caf`) | **Deferred**: no mature C# reader. Accept an FBX produced elsewhere via the `assimp` adapter instead |

Explicitly **not** in the pipeline: FModel, umodel/UE Viewer, the MW5 Mod Editor, Blender's GUI. They may be used once, manually, to produce an `.fbx`/`.glb` that the `assimp`/`gltf` adapter then reads — but nothing automated depends on them. (If a source ever needs a scripted pre-conversion, `blender -b --python` is the only acceptable form, and it must be optional.)

### 3.2 Normalizer

A pure, unit-testable chain applied to `RawModel`:

1. **Unit scale** — source units → metres (explicit per adapter, plus a `--scale` override).
2. **Orientation** — re-axis to Y-up/+Z-forward, bake the transform into vertices (not a node rotation).
3. **Pivot** — translate so the footprint centre is at the origin on the ground plane.
4. **Geometry** — triangulate, weld by position+UV+normal, drop degenerate faces, recompute tangents, clamp influences to 4.
5. **Skeleton** — map bones onto the canonical name table (case/prefix-insensitive, fuzzy with a review list); preserve unmapped bones as `extras`.
6. **Materials** — resolve textures into roles (`baseColor`, `normal`, `emissive`, `mask`), convert to PNG, cap at 2k, pick one `boardLook`.
7. **Animations** — match/alias/synthesize/absent per §2.4, resample to 30 fps, strip root motion into a metadata track (sprites are board-anchored).
8. **Metrics & validation** — bounds, footprint, tri/bone counts, convention assertions.
9. **Volume measurement** — voxelise the bounds at a fixed grid and count occupied cells (§4.3); record `volume.modelCubicMeters` and `planView.silhouetteAreaSquareMeters`. Tonnage comes from the catalog (built from `data/units/mechs/*.mmux`), never guessed from the model.

Every step is deterministic and idempotent: importing an already-canonical `.glb` must be a no-op apart from the manifest timestamp.

### 3.3 CLI

```bash
makamek-pipeline import \
  --source unreal \
  --uri "D:/Games/MW5/MW5Mercs/Content/Paks/MW5Mercs-WindowsNoEditor.pak" \
  --asset "/Game/Objects/Mechs/Atlas/SK_Atlas" \
  --id AS7 \
  --out artifacts/models/AS7 \
  --json
```

Exit codes follow the existing tool convention (`MtfConverter`/`MmuxPackager`): `0` ok, `2` bad arguments, `3` input missing, `4` validation failed.

---

## 4. Stage 2 — `render`

### 4.1 Renderer

**Blender, driven headlessly — nothing to build.** Full contract in [blender-render-driver.md](blender-render-driver.md); the short version:

- `blender -b --factory-startup -P tools/blender/render.py -- --model … --preset … --out …` builds the entire scene from the preset, imports the canonical `.glb`, renders the clip frames, and verifies every expected file exists before exiting.
- Blender already provides skinning, path-traced or rasterised shading, denoising, AO, soft shadows, **Freestyle outlines**, a compositor and colour management. That is the whole renderer, maintained by someone else.
- The `.blend` is not an input and not checked in: the scene is text (preset + driver), so it is diffable and reproducible. `--save-blend` exists only to inspect or art-direct a scene by hand.
- **Cycles CPU is the CI engine.** EEVEE needs a display/EGL context and fails in bare containers; it is a local-iteration option only.
- Determinism means "visually identical on the pinned Blender build": `--factory-startup`, fixed seed/samples/denoiser/threads/colour management, and perceptual (not byte-exact) regression comparison.
- Blender is a **build-time tool**: GPL, but nothing links against it and no Blender code ships. Its only output is our PNGs.

Rejected alternatives, for the record: a framework-free C# rasteriser (≈500 LOC plus shading, AA and outline work — and a quality ceiling), Avalonia-hosted OpenGL (batch capture needs a live surface; no WASM), HelixToolkit Avalonia (Direct3D 11, Windows-only), MonoGame (a second graphics stack and a Windows/Xvfb-shaped CI). Evidence in [../analysis/avalonia-3d-renderer-evaluation.md](../analysis/avalonia-3d-renderer-evaluation.md).

Blender **MCP** does not replace this driver: MCP drives a *running* Blender interactively (official Blender Lab server, Blender 5.1+, plus community servers) and is for authoring the preset or diagnosing a model. Shipped frames come from the CLI path only.

### 4.2 Presets

The look lives in `presets/*.json`, not in code:

```jsonc
{
  "name": "board-topdown",
  "camera": { "projection": "ortho", "pitchDegrees": 90, "yawDegrees": 0 },
  "scale": {
    "mode": "tonnage-volume",       // tonnage-volume | uniform | none
    "referenceTonnage": 100,
    "referenceCoverage": 0.90,      // of hex height, at reference tonnage
    "densityTonsPerCubicMeter": 0.15,
    "minimumCoverage": 0.55,        // readability floor, applied last
    "hexHeightLogicalPx": 86.6
  },
  "output": { "cell": 384, "supersample": 2, "background": "transparent", "colorSpace": "sRGB" },
  "engine": { "name": "cycles", "device": "cpu", "samples": 48, "denoise": "openimagedenoise" },
  "lights": [
    { "type": "area", "position": [-6, -8, 12], "size": 8, "color": "#FFF4E0", "intensity": 420 },
    { "type": "area", "position": [7, -3, 9], "size": 10, "color": "#AFC4E0", "intensity": 140 }
  ],
  "styling": { "outline": { "enabled": true, "width": 2.0, "color": "#101418" }, "posterize": 0 },
  "shading": {
    "groundShadow": { "enabled": true, "opacity": 0.25, "blurPx": 6 }
  },
  "clips": {
    "Idle":       { "frames": 6,  "fps": 8,  "row": 0 },
    "Walk":       { "frames": 10, "fps": 12, "row": 1 },
    "Run":        { "frames": 10, "fps": 16, "row": 2 },
    "ProneEnter": { "frames": 6,  "fps": 12, "row": 4 },
    "ProneIdle":  { "frames": 4,  "fps": 4,  "row": 5 },
    "StandUp":    { "frames": 7,  "fps": 12, "row": 6 }
  },
  "atlas": { "layout": "rows", "padding": 2, "powerOfTwo": true }
}
```

**One shipped preset: `board-topdown`, pitch locked at 90°** — the same projection as the current MegaMek line art, so replacing a sprite never changes the reading of the board. The preset mechanism stays (it is how the look is versioned), but there is no tilted variant to maintain.

Two consequences of 90° worth designing around:

- **Facing is the only depth cue.** Frames differ only in plan-view silhouette, so walk/run legs must splay rather than step "forward", and the light rig should be constant in world space (a per-mech rotating key light would make identical poses look different at different facings).
- **Prone needs a deliberate silhouette cue.** A fallen mech seen from directly above is still "a mech from above"; limb splay alone is weak at 84×72. Plan on splayed limbs *plus* a static marker (desaturated fill or a downed chevron) from the preset's `styling` block — and verify it in the P2 gate.

### 4.3 Scale: tonnage-volume normalisation

Sprite size must express **tonnage**, not the artist's opinion of bulk. MW5 is the motivating case: its assault mechs are visually inflated relative to lighter chassis, so a raw bounding-box fit makes an Atlas swallow the hex while a Locust rattles around in it.

The model is three independent factors:

```text
V_model     = voxelised displacement volume of the canonical mesh (m³)
V_target    = density × tonnage                       // 100 t @ 0.15 t/m³ → 667 m³
s_volume    = (V_target / V_model)^(1/3)              // per-model correction
s_override  = scale-overrides.json[chassis] ?? 1.0     // human review, persisted
pxPerMetre  = (referenceCoverage × hexHeightLogicalPx) / (V_target(refTonnage)^(1/3))
spriteScale = pxPerMetre × s_volume × s_override
```

Why this shape:

- **Volume correction is a measurement, not a geometry edit.** `s_volume` is stored in the manifest and applied at render time, so the `.glb` stays a faithful copy of the source, and changing the density constant re-scales everything without re-importing 76 models.
- **The pixel factor is global**, fixed by the preset so that a 100 t mech fills `referenceCoverage` of the hex height. Because volume scales with the cube of length, sprite size then falls out as `∝ tonnage^(1/3)` automatically — 20 t renders at ~58% the linear size of 100 t, which is both physically honest and the thing the current flat 84×72 art cannot express.
- **Volume is measured on a declared loadout**, not on whatever the variant happens to carry (`volume.includesAttachments` + `canonicalLoadout` per chassis), so `AS7-D` and `AS7-K` produce identical sprites.
- **Voxel, not tetrahedron.** Game meshes are open (missing arms, unwelded shells) so signed-tetrahedron volume is garbage. Voxelising the bounds at a fixed grid (default 64³) and counting occupied cells × cell volume is robust, deterministic, and cheap. `--volume-method hull` is available when a watertight mesh is expected.

One property worth knowing before tuning anything: **`density` cancels out of the sprite size.** It raises `s_volume` by `density^(1/3)` and lowers `pxPerMetre` by the same factor, so the rendered height depends only on `tonnage / V_model` (a shape constant) and `referenceCoverage`. Density exists for the native 3D client's physical units, not for sprites — `referenceCoverage` and the overrides are the only real knobs. Size ends up `∝ tonnage^(1/3)`: a 20 t Locust is ~58% the linear size of a 100 t Atlas, not 20%.

At 90° pitch the *visible* quantity is plan-view area, not 3D volume, so the render report also records `planView.silhouetteArea`, and the readability floor (`minimumCoverage`) is applied against projected coverage, not height:

```text
coverage = projectedSilhouetteArea(mech) / hexArea
if coverage < minimumCoverage → warn, and clamp sprite scale up to the floor
```

The clamp is a warning, not a silent fix — a light mech hitting the floor is a deliberate design decision to be recorded in the override file.

#### Per-chassis overrides (persisted, reviewed, re-runnable)

`scale-overrides.json` lives in the repo next to the preset, one entry per chassis, and is **applied to every run** until changed:

```jsonc
{
  "AS7": { "scaleMultiplier": 1.05, "note": "reads small next to KGC", "reviewed": "2026-10-25" },
  "LCT": { "scaleMultiplier": 1.18, "note": "below the readability floor", "reviewed": "2026-10-25" },
  "COM": { "scaleMultiplier": 1.00, "note": "", "reviewed": "2026-10-25" }
}
```

Rules that make the loop safe:

- **Precedence:** preset → chassis override → optional variant override (`AS7-D`) → floor clamp. Every applied entry is listed in `render-report.json` with its `note`, so a re-run is reproducible and auditable.
- **Drift warnings:** an override for a chassis missing from the catalog, an entry unreviewed for > 180 days, or an effective coverage outside `[minimumCoverage, 0.98]` produces a warning rather than silently shipping.
- **Iteration loop:** render the **lineup sheet** (all chassis at effective scale on a hex grid, one image), fix the JSON, then `--only AS7,LCT` to re-render just the offenders. No re-import, no re-extraction.
- **Escape hatches:** `--ignore-overrides` (raw output for comparison) and `--mode uniform` (equal sprites, for A/B judging whether tonnage scaling is actually readable).
- **Suggestion pass:** `render --suggest-overrides` writes a `scale-proposals.json` diff (chassis whose coverage lands outside a target band) for a human to accept — the tool proposes, the checked-in file decides.

### 4.4 Output

```
artifacts/sprites/AS7/
  frames/<clip>/<frame:03>.png   # raw Blender frames (the only thing Blender produces)
  unit-sprites.png               # atlas, one row per clip (packed by the pipeline, not by Blender)
  unit-sprites.json              # frame rects, fps, loop, hold, pivot (schema from the analysis doc)
  unit.png                       # static Idle frame, downscaled to the current 84×72 slot
  contact-sheet.png              # every clip/frame in one image, for human review
  lineup.png                     # (batch only) all chassis at effective scale on a hex grid
  scene.blend                    # optional, --save-blend only: the generated scene, for inspection
  render-report.json             # per-clip frames, Blender version, samples, effective scale, warnings
```

Division of labour: Blender writes PNG frames and nothing else. All assembly — downscale, atlas packing, `unit.png`, contact sheet, lineup, reports — is the pipeline's job, which is why the atlas layout can change without touching Python.

The atlas exists so the MakaMek runtime change is mechanical: `.mmux` v2 adds `unit-sprites.png` + `unit-sprites.json` next to the unchanged `unit.png`, and `UnitControl` swaps its `Image` for a Presto `SpriteSheet`.

### 4.5 CLI

```bash
makamek-pipeline render --model artifacts/models/AS7 --preset presets/board-topdown.json --out artifacts/sprites/AS7 --json
makamek-pipeline render --catalog chassis-map.json --preset presets/board-topdown.json --out artifacts/sprites --json
makamek-pipeline render --catalog chassis-map.json --preset presets/board-topdown.json --only AS7,LCT --json
makamek-pipeline render --catalog chassis-map.json --preset presets/board-topdown.json --suggest-overrides --json

# Blender-facing controls (thin pass-through to the driver, validated against the preset)
makamek-pipeline render … --blender-path "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
makamek-pipeline render … --smoke-test          # 1 frame, 64×64, perceptual check — CI canary
```

`render` shells out to the driver and fails on a non-zero exit **or** a missing expected frame. `--catalog` renders every mapped chassis with per-item failure isolation, partial success allowed, one `summary.json` plus one `lineup.png` at the end. This is the unattended 76-chassis path — and the lineup sheet is the artifact a human reviews for scale (§4.3).

---

## 5. Packaging: `pack`

`.mmux` v2 assembly reuses the zip writer already in `src/MakaMek.Tools/MmuxPackager` — extract it into `MakaMek.Pipeline.Pack` and have the existing tool call the shared code (no duplicated writer).

```bash
makamek-pipeline pack --unit data/units/mechs/AS7-D.mmux --sprites artifacts/sprites/AS7 --out artifacts/pack/AS7-D.mmux --json
```

Rule: the pipeline **writes to `artifacts/`**, never to `data/`. Promoting a pack into `data/` is a deliberate, reviewable copy — and for MW5-derived assets, never (see §8).

---

## 6. Agentic surface

### 6.1 One service, three faces

```
IPipelineService  ←  CLI verbs  ←  MCP tools
```

All logic lives in `IPipelineService`; the CLI and the MCP server are thin wrappers. No behaviour may exist only in the CLI.

### 6.2 CLI

Single binary `makamek-pipeline`:

| Command | Purpose |
|---|---|
| `import` | source → canonical model |
| `render` | canonical model → sprites/atlas |
| `pack` | sprites + unit data → `.mmux` |
| `run` | `import → render → pack` in one call |
| `inspect` | print metrics/clips/materials of a canonical model (no GPU) |
| `validate` | assert conventions + manifest schema (no GPU, CI-friendly) |
| `sources` | list adapters and their capabilities |
| `catalog` | build/refresh `chassis-map.json` (tonnage, weight class, canonical loadout) from `data/units/mechs` |

Global flags: `--json` (machine-readable envelope: `{ ok, data, warnings, errors }`), `--quiet`, `--log-level`. Human output is a rendering of the same envelope — never the only output.

### 6.3 MCP server

`ModelContextProtocol` (already referenced by `BotAgent`/`BotContainer` in this repo), stdio transport.

| Tool | Input | Output |
|---|---|---|
| `pipeline_sources` | — | adapters + capabilities |
| `pipeline_inspect` | `modelPath` | manifest summary, clip table, warnings |
| `pipeline_validate` | `modelPath` | violations with JSON-pointer paths |
| `pipeline_import` | `source`, `uri`, `asset`, `id`, `out`, `options` | canonical model path + manifest + warnings |
| `pipeline_render` | `modelPath`, `preset`, `out`, `clips[]`, `ignoreOverrides`, `only[]` | sprite paths + effective scale + coverage warnings |
| `pipeline_scale` | `catalog`, `preset` | current overrides + suggested changes + lineup path |
| `pipeline_pack` | `unitPath`, `spritesPath`, `out` | `.mmux` path + entry list |
| `pipeline_run` | full chain spec | all of the above |
| `pipeline_batch` | `catalog`, `preset`, `out` | per-item status + summary |

Resources: `pipeline://presets` and `pipeline://schema/model` (so an agent can read the preset list and manifest schema instead of guessing). Every tool returns structured content, not prose — agents should not have to parse log text.

Blender's own MCP server is a **separate, complementary** surface, not part of this contract: the official Blender Lab server (Blender 5.1+, add-on + external server, executes LLM-generated Python with no guards) and community servers drive a *running* Blender for interactive authoring and diagnosis. The pipeline's MCP wraps the deterministic CLI; Blender MCP is how a human or agent tunes the scene that the CLI will later render. Never make CI depend on it.

Non-goals for the MCP surface: no GUI automation, no "click here" instructions, no tools that require a human in the loop.

---

## 7. Project layout & testing

Mirrors existing `src/MakaMek.Tools` conventions (`net10.0`, `System.CommandLine 2.0.13`, one console app per concern, `README.md` per tool). Blender is the only Python, isolated under `tools/blender/` as a build tool:

```
src/MakaMek.Tools/
  MakaMek.Pipeline.Abstractions/   IModelSource, IRenderBackend, DTOs, CanonicalClip, validator
  MakaMek.Pipeline.Import/         adapters (gltf, assimp, unreal, unity) + Normalizer + glb writer
  MakaMek.Pipeline.Render/         driver invocation, frame/atlas packing, contact sheet, lineup, report
  MakaMek.Pipeline.Pack/           .mmux v2 writer (shared with MmuxPackager)
  MakaMek.Pipeline.Cli/            makamek-pipeline
  MakaMek.Pipeline.Mcp/            MCP server
tools/blender/
  render.py                        scene build → render → verify outputs (see blender-render-driver.md)
  scene.py, preset.py, README.md
presets/
  board-topdown.json
  scale-overrides.json
tests/MakaMek.Pipeline.Tests/      normalizer, clip resolution, synthesis rules, atlas math, manifest, scale chain
```

Testing split matches the repo's coverage policy (UI and external tools excluded, logic covered):

- **Covered by unit tests:** scale/axis/pivot maths, bone mapping, clip alias + synthesis resolution, fps resampling, preset parsing (including strict unknown-key rejection), atlas layout, `unit.png` downscale, manifest serialisation and validation, **volume voxelisation and the tonnage→sprite scale chain** (override precedence, coverage floor), and the Blender **command-line construction** (pure string/args mapping — the thing that silently breaks).
- **Not covered:** Blender itself and the Python driver. They get a CI canary instead: `render --smoke-test` renders one frame of a committed fixture at 64×64 and compares it perceptually against a reference. Skips with a clear message when Blender is not installed; never blocks a PR on the renderer.

Determinism requirements (so a diff report means something): pinned Blender version, `--factory-startup`, fixed seed/samples/denoiser/threads/colour management from the preset, fixed culture, pinned package versions, sorted iteration everywhere, and content hashes of model + preset + Blender build recorded in `render-report.json`.

---

## 8. Licensing

Stage 1 records `source.license` in every manifest and the CLI refuses to `pack` an asset whose license is marked `not-redistributable` unless `--allow-nonredistributable` is passed, in which case the output is written outside `artifacts/` too. That keeps MW5-derived sprites a local, user-generated pack (loaded through the existing `LocalFolderResourceStreamProvider`) and prevents an accidental commit of derivative art.

Everything else — adapters, normalizer, renderer, presets, format — ships. The output of MW5 does not.

---

## 9. Phasing

| Phase | Deliverable | Proof |
|---|---|---|
| **P1** | `Abstractions` + `gltf` adapter + normalizer + `inspect`/`validate` + tests | any `.glb` in → canonical `.glb` + manifest out, no GPU needed |
| **P2a** | `tools/blender` driver + preset + `render --smoke-test`; one chassis, one clip | a canonical model renders top-down at 84×72, and the same command reproduces it |
| **P2b** | atlas + `render` CLI + `contact-sheet` + `lineup` + tonnage-volume scale chain | 3 chassis rendered; side-by-side against current 84×72 art *and* the first `lineup.png` — is a Locust visibly smaller than an Atlas, and is the Locust still readable? |
| **P3** | `unreal` adapter (CUE4Parse) + `--catalog` batch + override review pass | 76 chassis static sprites, unattended, scale reviewed once via the lineup sheet |
| **P4** | animation extraction + clip resolution/synthesis + `.mmux` v2 + `pack` | `Idle`/`Walk`/`Run`/`ProneEnter`/`ProneIdle`/`StandUp` shipped for one chassis |
| **P5** | MCP server, diff reports, `run`/`batch` orchestration, optional asset previewer | an agent can go from pak → `.mmux` with no human steps |

P2a/P2b is the go/no-go gate: it must answer two questions at board size, not one. (1) Does a 90° render read better than the current line art? (2) Is the tonnage-driven size range — roughly 0.58× linear from 20 t to 100 t — readable for lights without an override? If either fails, stop before P3. P2a is deliberately small: the driver, one preset and one model, because that is the whole renderer.

---

## 10. Risks

| Risk | Mitigation |
|---|---|
| **Python in the repo** | Confined to `tools/blender/`, treated as a build tool with no runtime counterpart; everything else stays C# |
| **Blender version churn breaks the scene** | Pin the version, check it at startup and refuse on a major mismatch, ship the CI smoke canary |
| **EEVEE won't run in CI** | Cycles CPU is the CI engine; EEVEE is local-only (it needs a display/EGL context) |
| **Render time at scale** | 1–3 s/frame CPU; levers in order: samples down (denoise), 256² cells, fewer clips per pass, parallel chassis — not "write a renderer" |
| **Determinism expectations** | Cycles is stable but not bit-identical across builds; regressions are caught with perceptual comparison and tolerance, and the report records the Blender build |
| Blender exits 0 with partial output | The driver verifies every expected frame exists; the CLI fails on a missing frame, not just on the exit code |
| Blender MCP treated as a build dependency | It is interaction-only; CI never calls it, and the pipeline's own MCP wraps the CLI |
| Assimp FBX quality varies by exporter | Accept `.glb` as the preferred hand-off (Blender's glTF importer is the best-supported path anyway); treat FBX as best-effort with `validate` warnings |
| UE adapter churn (CUE4Parse follows engine releases) | Pin the package; the canonical format insulates stage 2 from it |
| Animation synthesis looks bad for prone | Keep `ProneEnter`/`ProneIdle`/`StandUp` overridable by authored clips dropped into the model folder |
| **Prone unreadable at 90°** | Plan-view silhouettes barely change when a mech lies down; add a static downed marker (desaturate / chevron) in the preset's `styling` block and validate at P4 |
| **Volume measurement noise** | Open/multi-shell meshes make volume unstable; voxel method at a fixed grid + a per-chassis `volumeCubicMeters` override for outliers, and report the value every run so drift is visible |
| **Scale-override drift** | Overrides are checked-in data that silently outlive their reason; require `note` + `reviewed`, warn after 180 days, list every applied override in the render report, and keep `lineup.png` as the standing review artifact |
| Preset sprawl | One preset per look, reviewed like code; per-chassis deviation belongs in `scale-overrides.json`, not in new presets |
| Repository size | `artifacts/` gitignored; only the `.mmux` promoted deliberately |
| Two-state truth (glb vs `.mmux`) | `.mmux` is generated; canonical model + preset are the source of truth |
