# Blender sprite driver

Headless Blender renders top-down board sprites from a 3D mech model. The driver builds the whole
scene from a preset on every run; there is no `.blend` input and nothing is hand-edited between runs.

Design and contract: [../../docs/architecture/blender-render-driver.md](../../docs/architecture/blender-render-driver.md).
The look lives in [board-topdown.json](board-topdown.json).

> **Spike layout.** Everything for this experiment lives in `tools/blender/` - driver, preset and this
> README - so it can be removed in one go if the verdict is negative. The design docs describe the
> eventual layout (`presets/board-topdown.json` at the repository root); the preset simply sits next to
> the driver for now. `artifacts/` stays at the root and is gitignored.

```
tools/blender/
  render.py    entry point: arguments -> scene build -> render -> verify outputs -> JSON report
  scene.py     camera / lights / materials / Freestyle / output settings
  preset.py    preset loading and strict validation (unknown keys are errors)
```

## Requirements

Blender **5.2 LTS** (pinned; the driver refuses another major version). Cycles CPU — no display
server, no GPU, no EGL.

```bash
# Windows (this repo's dev machines)
F:/blender/blender.exe -b --factory-startup --version

# Linux CI
/srv/blender/blender -b --factory-startup --version
```

## Usage

```bash
blender -b --factory-startup -noaudio \
        --python-exit-code 1 \
        -P tools/blender/render.py -- \
        --model  artifacts/models/AS7/model.glb \
        --preset tools/blender/board-topdown.json \
        --scale  4.5359 \
        --out    artifacts/sprites/AS7 \
        --clips  Idle \
        --report artifacts/sprites/AS7/render-report.json
```

Output layout:

```
<out>/frames/<clip>/<frame:03>.png     # RGBA frames, already at output.cellWidth x cellHeight
<out>/render-report.json               # only with --report
```

The last line printed to stdout is a JSON report including `appliedScale`, the Blender version,
engine/samples, the frame list and the model's measured metrics. Blender can exit `0` with partial
output, so the driver verifies every expected frame exists before exiting.

| Flag | Meaning |
|---|---|
| `--model` | glTF/GLB to render (required) |
| `--preset` | preset JSON (required) |
| `--scale` | **required**, pixel space: after it, 1 model unit = 1 px, so `ortho_scale = cellWidth` frames the sprite directly |
| `--out` | output directory (required) |
| `--clips` | comma-separated clip names from the preset (default `Idle`) |
| `--report` | write the JSON report to this path as well |
| `--frame N` | render a single frame per clip (static sprite / smoke test) |
| `--samples N` | override the preset sample count |
| `--resolution N` | render an N×N cell instead of the preset's — local inspection only |
| `--supersample N` | render N× the cell size (84×72 → 336×288 at 4×). `--scale` stays in 1× pixel space; the light rig and the Freestyle thickness scale with N so the look is preserved. The pipeline downsamples back to the cell size |
| `--engine cycles\|eevee` | `cycles` is the reproducible path; `eevee` needs a display and is local-only |
| `--lighting lights\|ambient\|flat` | override `lighting.mode`: `lights` = camera-parented area rig, `ambient` = uniform world light (no cast shadows, soft occlusion only), `flat` = emissive material, no lighting at all (silhouette + outline only) |
| `--device cpu\|gpu` | Cycles device; `cpu` for anything reproducible |
| `--no-outline` | disable the Freestyle outline |
| `--save-blend PATH` | keep the generated scene for inspection |
| `--model-yaw DEG` | **spike-only**: extra yaw for source exports that are not yet canonical. Canonical models face `+Z` and need `0` |

Exit codes: `0` ok, `2` bad arguments, `3` missing input, `4` invalid preset or failed verification.

## Scale is not the driver's business

The driver never reads manifests, tonnage, `scale-overrides.json` or coverage thresholds. The C#
pipeline computes `pxPerMetre x correctionScale x override x floorClamp` and passes one number.
`docs/architecture/asset-pipeline.md` §4.3 owns that formula.

For a hand-calibrated run, the reference-chassis algebra collapses to:

```
effectiveScale = (referenceCoverage x headroom x drawBoxHeightPx) / planView.maxExtentMeters[reference]
               = (0.90 x 1.2 x 72) / 14.286          # AS7 Atlas, measured from Atlas_SKM.glb
               = 5.443
```

`correctionScale` cancels for the reference chassis itself, so its sprite needs no volume measurement.

The **1.2 headroom** is empirical, not arbitrary: at the documented 0.90 the reference chassis covers
0.217 of the hex area against 0.297 for the shipped art, i.e. every sprite under-fills the board.
`x1.2` puts the Atlas at 0.309 and the Annihilator at 0.299, and keeps the widest plan view (80 px)
inside the 84 px cell. The equivalent change in pipeline terms is `referenceCoverage: 0.90` +
`scale.headroom: 1.2`; it is applied here by passing `--scale 5.443`.

## Preset keys the driver reads

| Key | Effect |
|---|---|
| `camera.pitchDegrees` / `yawDegrees` | camera rotation (`90` = straight down, the board look) |
| `output.cellWidth` / `cellHeight` | render resolution **and** `ortho_scale` (= `cellWidth`) |
| `output.background` | `transparent` -> `film_transparent` |
| `output.viewTransform` | colour management (`Standard`; AgX/Filmic would compress the grey ramp) |
| `output.channels` | must be `luminance` — enforced, converted by the pipeline, not by Blender |
| `engine.*` | Cycles CPU, samples, denoiser |
| `lighting.mode` | `lights` (camera-parented area rig), `ambient` (uniform world, no cast shadows), `flat` (emissive — no shading at all) |
| `lighting.color` / `strength` | world colour and strength, used by `ambient` only |
| `lights[]` | area/point/sun lamps, **neutral white**, positions and sizes in pixel space, parented to the camera and aimed at the footprint origin (`lights` mode only) |
| `styling.materialOverride` | replaces every imported material with the flat `board-look` shader |
| `styling.outline` | Freestyle silhouette + border (+ optional `crease`), thickness in pixels |
| `styling.posterize` | reserved (not implemented yet) |
| `shading.groundShadow` | reserved (not implemented yet) |
| `clips` | frame counts per clip; the atlas layout stays in the pipeline |
| `scale.*`, `atlas.*` | **not read** — they belong to the pipeline |

`light` positions/sizes are in pixel space because after `--scale` 1 unit = 1 px. That keeps one rig
valid for every chassis regardless of how large the model is in metres.

The preset is validated strictly: an unknown key, a missing required section, a non-ortho camera or a
non-luminance output channel is an error, never a silent default.

## Determinism

`--factory-startup`, fixed seed (`0`), adaptive sampling off, fixed samples and denoiser, fixed thread
count (`4`), `Standard` view transform, no motion blur, no persistent data.

- Same machine, same build: the frame is **byte identical** across runs (verified on 5.2.3, Cycles CPU,
  64 samples, Freestyle on).
- Across machines/builds: expect small differences. Compare perceptually (per-channel delta), not by hash.
- Measured (5.2.3, CPU, AS7, Freestyle on), marginal cost per frame:

  | cell | samples | s/frame | 76 chassis × 60 frames |
  |---|---|---|---|
  | 84×72 | 16 | 1.11 | ~85 min |
  | 84×72 | 64 | 1.15 | ~87 min |
  | 336×288 (4×) | 64 | 3.37 | ~4.3 h |

  The sample count barely matters — Freestyle and per-frame scene sync dominate, not pixel fill. Use 64:
  it is effectively free and notably less noisy than 16. Supersampling costs ~3× (4× the pixels), and 4×
  is where the detail stops being mush: 16× is visually indistinguishable from 4× after downscaling.

## Gotchas

- **Preview orientation.** The board hex is **flat-top**, 100 × 86.6 (`HexCoordinatesPixelExtensions`),
  so a mech facing "up" faces a flat *edge*, not a vertex. The biome base texture
  (`data/hexes/biomes/*.mmtx` -> `base.png`, authored at 84×72) is stretched over the whole hex and
  `UnitControl` draws the unit at `0.84 × hex` = 84 × 72 centred. A pointy-top hex, or compositing the
  sprite onto the 84×72 base texture without stretching the hex, makes the mech look larger than it is
  in game.
- Blender's glTF importer parks helper objects (an `Icosphere` bone shape, gizmos) in a
  `glTF_not_exported` collection. They are not part of the model; the driver deletes them before
  measuring, shading and rendering. Anything that measures a glTF by hand must exclude them too, or
  the reported bounds are wrong by the helper's size.
- FModel exports UE cm as glTF metres, keeps the mech **facing +X** (not the canonical +Z) and gives
  weapons their own `Weapon_SKL` skeleton. Until stage 1 normalises the model, pass `--model-yaw 90`
  and do not expect weapons to land on their hardpoints.
- Freestyle line thickness above ~1.2 px at 84×72 covers a large share of the silhouette; the sprite
  turns into a black blob rather than gaining detail.

## Licensing

The driver and preset are ours. **Anything it renders from MW5 is not** — PGI/Microsoft IP, must stay
out of the repository and out of published asset packs. Render into `artifacts/` (gitignored) or outside
the repo. See `docs/analysis/mw5-model-extraction.md` §5.
