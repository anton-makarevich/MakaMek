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
  scene.py     camera / lights / materials / Freestyle / output settings / animation helpers
  preset.py    preset loading and strict validation (unknown keys are errors)
  sheet.py     atlas, contact sheet, clips.csv/json, GIF previews (Pillow; not used by the render path)
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
        --scale  5.460 \
        --fit \
        --supersample 4 \
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
| `--scale` | **required**, pixel space: after it, 1 model unit = 1 px, so `ortho_scale` is simply the larger cell dimension and frames the sprite directly |
| `--out` | output directory (required) |
| `--clips` | comma-separated clip names from the preset (default `Idle`). On an animated model each name resolves to the glTF animation of the same name; a missing clip fails the run instead of rendering a static pose |
| `--report` | write the JSON report to this path as well |
| `--frame N` | render a single output frame per clip (static sprite / smoke test) |
| `--samples N` | override the preset sample count |
| `--resolution N` | render an N×N cell instead of the preset's or the fitted one — local inspection only |
| `--fit` | size the cell to this model's plan-view projection (+2 px margin at 1×, rounded up to a multiple of the supersample factor) instead of a fixed cell. Measured over **every rendered frame**, so an animated pose cannot be clipped. Each unit ships its own canvas and is drawn 1:1, centred in the hex |
| `--supersample N` | render N× the cell size (with `--fit`, 83×42 → 332×168 at 4×). `--scale` stays in 1× pixel space; the light rig and the Freestyle thickness scale with N so the look is preserved. The pipeline downsamples back to the cell size |
| `--engine cycles\|eevee` | `cycles` is the reproducible path; `eevee` needs a display and is local-only |
| `--lighting lights\|ambient\|flat` | override `lighting.mode`: `lights` = camera-parented area rig, `ambient` = uniform world light (no cast shadows, soft occlusion only), `flat` = emissive material, no lighting at all (silhouette + outline only). Validated with the override applied, so `lights` still requires a non-empty rig |
| `--device cpu\|gpu` | Cycles device; `cpu` for anything reproducible |
| `--no-outline` | disable the Freestyle outline |
| `--save-blend PATH` | keep the generated scene for inspection |
| `--model-yaw DEG` | yaw for models exported in a different facing. Canonical static models need `0`; animated MW5 models are exported in source space and need **-90** (the assembly report prints `modelYawDegrees`) |

Exit codes: `0` ok, `2` bad arguments, `3` missing input, `4` invalid preset or failed verification.

## Animated clips and sheets

An assembled animated model carries one glTF animation per clip, named with the canonical vocabulary
(`Walk`, `Run`, …). `--clips Walk` resolves the action of that name; the preset's `frames`/`fps` decide the
shipped sampling, not the source rate.

```bash
blender -b --factory-startup -noaudio --python-exit-code 1 \
        -P tools/blender/render.py -- \
        --model  artifacts/mw5/assemble/AS7-D/AS7-D.animated.glb \
        --preset tools/blender/board-topdown.json \
        --scale  5.098 --model-yaw -90 --fit --supersample 4 \
        --clips  Walk \
        --out    artifacts/spike/anim/AS7-D \
        --report artifacts/spike/anim/AS7-D/report.json

python tools/blender/sheet.py \
        --frames artifacts/spike/anim/AS7-D/frames \
        --report artifacts/spike/anim/AS7-D/report.json \
        --assembly artifacts/mw5/assemble/AS7-D/AS7-D.animated.assembly.json \
        --preset tools/blender/board-topdown.json \
        --out artifacts/spike/anim/AS7-D \
        --frames-out artifacts/spike/anim/AS7-D/frames-1x \
        --name AS7-D
```

What the driver does with a clip:

- Pins the scene to 30 fps **before** the glTF import, so a glTF time of `t` seconds becomes source frame
  `t × 30` on every machine.
- Samples the action's key range evenly for the preset's output frame count, using subframes: `Walk` is 60
  source poses but ships 10 frames at 12 fps.
- Resets every pose bone and detaches NLA tracks before each clip, so a second clip cannot inherit bones the
  first one did not animate.
- Measures `--fit` from the evaluated (deformed) mesh per clip, with that clip's action active.
- Fails with exit `4` when the model has animations but the requested clip is not among them.

`sheet.py` (Pillow, `pip install pillow`) box-downsamples the 4× frames to 1× and writes:

```
unit-sprites.png / .json      # one row per clip, 2 px gutters, power-of-two, cell size + clip metadata
contact-sheet.png             # every frame labelled ("f04 src 19.7"), neutral and hex backgrounds
clips.csv / clips.json        # source asset, source fps/frames, shipped fps/frames, root motion, loop closure
preview-<clip>.gif            # 4× motion sanity check
frames-1x/<clip>/<frame:03>.png  # the downsampled 1× frames, when --frames-out is given
```

## Scale is not the driver's business

The driver never reads manifests, tonnage, `scale-overrides.json` or coverage thresholds. The C#
pipeline computes `pxPerMetre x correctionScale x override x floorClamp` and passes one number.
`docs/architecture/asset-pipeline.md` §4.3 owns that formula.

For a hand-calibrated run, the reference-chassis algebra collapses to:

```
effectiveScale = (referenceCoverage x hexWidthPx) / planView.maxExtentMeters[reference]
               = (0.78 x 100) / 14.286               # AS7 Atlas, measured from Atlas_SKM.glb
               = 5.460
```

`correctionScale` cancels for the reference chassis itself, so its sprite needs no volume measurement.

**There is no fixed sprite frame.** At a 90° camera the plan-view max extent maps to the sprite's
horizontal axis, and the sprite is drawn 1:1, centred in the hex (`Stretch=None`), so every unit can
ship exactly the canvas its own projection needs:

- `--fit` sizes the cell to the model's projection plus 2 px of margin at 1x (rounded up to a multiple
  of the supersample factor), covering every frame the run renders on both sides of the unit's origin
  so an animated pose is not clipped. Measured: Atlas **83 x 42** (mech 78 x 38), Annihilator
  **70 x 55** (mech 65 x 51). At `--supersample 4` that is 332 x 168 and 280 x 220.
- Portrait canvases (height > width) work too: `ortho_scale` is the larger cell dimension, so 1 unit
  stays 1 px in both axes and nothing is clipped.
- `referenceCoverage = 0.78` is the fraction of the 100 px hex width the reference chassis fills; it
  reproduces the hand-tuned "x1.2 over the old 84 px draw box" look (78 px against 80 px) while
  removing the 84x72 frame entirely.
- The old fixed-cell behaviour is still available by passing `--resolution N` (square) or by leaving
  `--fit` off, in which case `output.cellWidth/cellHeight` from the preset is used.
- Wide chassis can still poke a few pixels past the hexagon's slanted edges. A per-chassis
  `scale-overrides` entry is the escape hatch; that mechanism already exists in the design.

## Preset keys the driver reads

| Key | Effect |
|---|---|
| `camera.pitchDegrees` / `yawDegrees` | camera rotation (`90` = straight down, the board look) |
| `output.cellWidth` / `cellHeight` | render resolution **and** `ortho_scale` (the larger of the two, so 1 unit = 1 px for portrait cells too); ignored under `--fit`, which derives both from the model |
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
non-luminance output channel is an error, never a silent default. A `--lighting` override is applied
before that validation, so `--lighting lights` against an empty `lights` list fails with exit `4`
instead of rendering an unlit sprite.

## Determinism

`--factory-startup`, fixed seed (`0`), adaptive sampling off, fixed samples and denoiser, fixed thread
count (`4`), `Standard` view transform, no motion blur, no persistent data.

- Same machine, same build: static frames are byte-identical across runs; animated frames are
  **pixel-identical** (max channel delta **0** on all 10 `Walk` frames) while the PNG container bytes differ.
  Compare pixels, not hashes.
- Across machines/builds: expect small differences. Compare perceptually (per-channel delta), not by hash.
- Measured (5.2.3, CPU, AS7, Freestyle on), marginal cost per frame:

  | cell | samples | s/frame | 76 chassis × 60 frames |
  |---|---|---|---|
  | fitted (83×42 for the Atlas) | 64 | 1.10 | ~1.4 h |
  | fitted at 4× (332×168) | 64 | 2.32 | ~2.9 h |

  The sample count barely matters — Freestyle and per-frame scene sync dominate, not pixel fill; 16 vs
  64 samples differed by ~4 % at 84×72. Use 64: it is effectively free and notably less noisy than 16.
  Supersampling costs ~2×, and 4× is where the detail stops being mush: 16× is visually
  indistinguishable from 4× after downscaling.

## Gotchas

- **Preview orientation.** The board hex is **flat-top**, 100 × 86.6 (`HexCoordinatesPixelExtensions`),
  so a mech facing "up" faces a flat *edge*, not a vertex. The biome base texture
  (`data/hexes/biomes/*.mmtx` -> `base.png`, authored at 84×72) is stretched over the whole hex and
  `UnitControl` draws the unit at `0.84 × hex` = 84 × 72 centred. A pointy-top hex, or compositing the
  sprite onto the 84×72 base texture without stretching the hex, makes the mech look larger than it is
  in game.
- **Sprites are per-unit sized, but the app still draws at 0.84.** Until `UnitControl` changes from
  `Stretch.Fill` into `Width * 0.84` to `Stretch.None` centred (1:1), a fitted sprite is squeezed into
  84 × 72.7 in game. On-terrain previews drawn 1:1 show the target state, not today's build.
- **Previews must upscale with NEAREST, not a resampling filter.** The first 100×87 comparison sheet
  looked blurred at 4×/8× because the already-downsampled sprite was resized with LANCZOS; nearest
  shows the real pixels.
- Blender's glTF importer parks helper objects (an `Icosphere` bone shape, gizmos) in a
  `glTF_not_exported` collection. They are not part of the model; the driver deletes them before
  measuring, shading and rendering. Anything that measures a glTF by hand must exclude them too, or
  the reported bounds are wrong by the helper's size.
- **The importer drops an armature-level transform on skinned meshes.** A canonical basis baked into the
  armature rotates the attachments but leaves the body in source space (and vice versa). Animated models
  therefore ship in source space and the driver applies `--model-yaw -90`; `--fit` then recentres the
  footprint and grounds the feet. Do not try to bake the basis into the skeleton root: the importer
  normalises that too.
- **Bone-parented mesh nodes are unreliable across the importer.** Attachments must be skinned vertices
  bound 100 % to their mount joint (what `MW5Extractor assemble --clips` does), not child nodes of a bone.
- **`--fit` must measure evaluated vertices.** `Object.bound_box` is the undeformed mesh, so an animated
  pose is invisible to it; the driver's `world_bounds` walks the evaluated mesh instead.
- **Pixel-identical ≠ byte-identical.** Repeated animated renders on the same build produced frames with a
  max channel delta of 0, but different PNG container bytes (metadata), so compare pixels, not file hashes.
- FModel exports UE cm as glTF metres, keeps the mech **facing +X** (not the canonical +Z) and gives
  weapons their own `Weapon_SKL` skeleton. Until stage 1 normalises the model, pass `--model-yaw 90`
  and do not expect weapons to land on their hardpoints.
- Freestyle line thickness above ~1.2 px at 100×87 covers a large share of the silhouette; the sprite
  turns into a black blob rather than gaining detail.

## Licensing

The driver and preset are ours. **Anything it renders from MW5 is not** — PGI/Microsoft IP, must stay
out of the repository and out of published asset packs. Render into `artifacts/` (gitignored) or outside
the repo. See `docs/analysis/mw5-model-extraction.md` §5.
