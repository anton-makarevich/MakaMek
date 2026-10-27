# Blender Render Driver (stage 2)

How the pipeline turns a canonical model into sprites **using Blender as the renderer** — automation only, no renderer of our own.

Related: [asset-pipeline.md](asset-pipeline.md) §4 · rationale and rejected alternatives in [../analysis/avalonia-3d-renderer-evaluation.md](../analysis/avalonia-3d-renderer-evaluation.md)

---

## 1. Position

Blender is the sprite renderer. It already has: glTF import with skinning and animation, a path tracer (Cycles) and a rasteriser (EEVEE), denoising, ambient occlusion, Freestyle line rendering, a compositor, and a Python API that can drive all of it headlessly.

Writing a renderer would mean re-implementing skinning, a z-buffer, shading, antialiasing and outlines — and then living with the result. Instead the pipeline owns a **scene contract** and a **driver script**; Blender owns everything about how pixels are made.

Blender is a build-time tool. It is GPL, but nothing links against it and no Blender code ships in the app — the only artefacts are PNGs we generate.

## 2. Invocation contract

The only supported entry point:

```bash
blender -b --factory-startup -noaudio \
        --python-exit-code 1 \
        -P tools/blender/render.py -- \
        --model   artifacts/models/AS7/model.glb \
        --preset  presets/board-topdown.json \
        --scale   5.06 \
        --out     artifacts/sprites/AS7 \
        --clips   Idle,Walk,Run \
        --fit --supersample 4 \
        --report  artifacts/sprites/AS7/render-report.json
```

Rules:

- `-b` — background, no UI, no display server needed.
- `--factory-startup` — ignore user preferences and add-ons; the render must not depend on whose machine it is.
- `--python-exit-code 1` — a Python exception fails the process. Additionally the driver verifies every expected file exists afterwards, because Blender can exit `0` with partial output.
- `--` — everything after it is parsed by the driver, not by Blender.
- `--scale` is **required** and is the pipeline's `effectiveScale` (§3.2). The driver never reads manifests, overrides or tonnage: it is a renderer, not a policy engine.
- The `.blend` is **not** an input and not checked in. The scene is constructed by the script from the preset, so the scene is diffable text, not an opaque binary.

Optional flags: `--engine cycles|eevee`, `--samples N`, `--resolution N`, `--save-blend path`, `--frame N`
(single output frame, a render smoke test), `--no-outline`, `--device cpu|gpu`, `--lighting
lights|ambient|flat`, `--fit` (per-model canvas), `--supersample N` (render N× and let the pipeline
downsample), `--model-yaw degrees` (source-space models; canonical models need `0`).

## 3. Scene contract

Built by the driver, every run, from the preset:

| Element | Rule |
|---|---|
| Model | `bpy.ops.import_scene.gltf(filepath=…)`; apply the **`--scale` value the pipeline passes** (see §3.2 — the driver never computes scale itself); position so the **footprint centre** is at the world origin, ground at `z = 0` |
| Camera | Orthographic, pitch 90°, yaw 0°, `ortho_scale` derived from the preset's `referenceChassis` calibration so all chassis share one scale |
| Lights | Key + fill + rim parented to the camera, fixed intensities, **neutral white** (a coloured light would break the luminance contract, §3.1) — identical for every mech and every frame (facing is a runtime rotation, so lighting must not vary with it) |
| Materials | `board-look` override: imported textures and albedo are **discarded**, replaced by a near-white base with a tight roughness range, so all detail comes from shading rather than colour |
| Outline | **Freestyle** line set (silhouette + crease), dark and thin — this is what replaces the current 2D line art |
| Film | `transparent = True`; output is converted to 8-bit **luminance + alpha** by the pipeline (see §4) |
| Colour management | `Standard` view transform, not AgX/Filmic: a filmic curve is designed to desaturate and compress highlights, which fights a controlled grey ramp |
| Frame range | Per clip: the preset's `frames`/`fps`, sampled across the imported action's key range (§3.3); static models keep the historical `1..N` scene frames |
| Ground | Optional shadow catcher for a soft contact shadow (luminance only, like everything else) |

Nothing in the scene is hand-edited between runs. If the scene needs a change, it is a preset change or a driver change.

### 3.1 Colour contract

Sprites are **luminance-only** — no hue — because `UnitControl` tints them at runtime: it composites a `Border` with `Background = owner colour` and `OpacityMask = ImageBrush(sprite)` at 0.7 opacity over the unit image. Alpha carries the silhouette; luminance carries the detail that survives the wash.

Therefore:

- Render as greyscale by construction (`board-look` override, `Standard` view transform).
- The pipeline converts each frame to `L = 0.2126R + 0.7152G + 0.0722B` and writes `R = G = B = L`, then **fails the frame** if the pre-conversion channel delta exceeds 1/255 — a coloured render means the material override or the view transform was not applied.
- The current art already satisfies this (~98% neutral pixels, full 4…255 range), so the tint path needs no change.

### 3.2 Scale handoff

Scale policy has exactly one owner — the C# pipeline — and the driver receives a single number in **pixel space**.

```text
pxPerMetre     = (referenceCoverage × drawBoxHeightPx)          // from the preset
                 / (planView.maxExtentMeters[referenceChassis]
                    × correctionScale[referenceChassis])        // from that chassis's manifest

effectiveScale = pxPerMetre
               × model.volume.correctionScale                   // measured at import
               × (variant overrides[chassis][variant]            // optional, most specific first
                  ?? overrides[chassis])
               × floorClamp                                     // minimumCoverage, if triggered
```

- The pipeline computes it and passes `--scale`; the driver applies that uniform scale to the imported armature root and sets `ortho_scale = output.cellWidth`, so **after scaling, 1 model unit = 1 pixel** and framing is direct. The camera never depends on which mech it is looking at.
- The driver does **not** read `model.json`, `scale-overrides.json`, `planView.*`, tonnage or coverage thresholds. That keeps the calibration and override logic in C# (where it is unit-covered) and makes the Python side purely mechanical.
- The driver echoes `{"appliedScale": <value>}` in its stdout report; the pipeline asserts it matches what it passed and writes the breakdown (`pxPerMetre`, `correctionScale`, `override`, `floorClamp`, `effectiveScale`) into `render-report.json`.
- A pre-scaled model (`--scale 1.0` with the scale baked into the `.glb`) is deliberately **not** supported: it would make the canonical model depend on a presentation preset and break reuse by a future 3D client.

### 3.3 Animated clips

A model may carry glTF animations. Stage 1 names each animation with the canonical clip name (`Walk`, `Run`,
…), so `--clips Walk` selects the action named `Walk`: no clip mapping lives in the driver.

Rules, measured in spike [#1653](https://github.com/anton-makarevich/MakaMek/issues/1653):

- **Import rate.** The scene fps is pinned to 30 *before* `import_scene.gltf`. glTF animation times are in
  seconds; Blender lays the keys out at scene fps, so a source frame is `seconds × 30` on every machine.
- **Sampling.** The preset's `frames`/`fps` are the shipped rate, not the source rate. Output frame *k*
  samples the action's key range at `start + (k−1)/(N−1) × (end − start)`, using subframes. Ten output
  frames over a 60-key walk therefore read at 12 fps while the source stays at 30.
- **Pose reset.** Before each clip the driver detaches the action, removes NLA tracks and resets every pose
  bone. Without that, a second clip in one invocation inherits bones the first clip did not animate.
- **Root motion** is stripped in stage 1 — the sprite is board-anchored. The driver never compensates for it.
- **Loop closing key.** Stage 1 drops the duplicated last key of a looping clip; the last rendered frame is
  the last *distinct* pose and the runtime wraps to frame 1.
- **Fitting.** `--fit` measures the evaluated (deformed) mesh over every source frame the run renders, per
  clip with its action active. An object's `bound_box` is the undeformed mesh, so animated frames cannot be
  fitted from it.
- **Space.** Animated MW5 models stay in the skeleton's exported space (metres, Y-up, facing **+X**):
  Blender's glTF importer drops an armature-level transform on skinned meshes, so a baked canonical basis
  would leave the body and its attachments in different spaces. `assemble` reports
  `modelSpace = "mw5-source"` and `modelYawDegrees = -90`; pass that value to `--model-yaw`. Static models
  stay canonical and need `0`.
- **Report.** `render-report.json` records, per clip, the action name, its frame range, and the source frame
  behind every output frame — that is what lets the contact sheet label `f04 src 19.7`.

## 4. Preset → scene mapping

The preset stays renderer-agnostic in shape; the driver translates the keys it understands and fails loudly on unknown ones (a typo must not silently render the default look).

| Preset key | Blender setting |
|---|---|
| `camera.pitchDegrees`, `camera.yawDegrees` | camera rotation |
| `scale.referenceChassis`, `scale.referenceCoverage`, `scale.drawBoxHeightPx` | **not read by the driver** — the pipeline uses them to compute `--scale` (§3.2). The driver only takes `output.cellWidth` as `ortho_scale` |
| `output.cellWidth`, `output.cellHeight` | `render.resolution_x/y` — 84×72, the same size every existing asset uses; AA comes from Cycles samples, not a supersampling multiplier |
| `output.background` | `film_transparent` |
| `output.channels` | luminance conversion + `R = G = B` assertion in the pipeline (Blender always writes RGB) |
| `output.viewTransform` | `scene.view_settings.view_transform` (`Standard`) |
| `lights[]` | lamp objects + intensities |
| `styling.materialOverride` | replace imported materials with the `board-look` node group |
| `styling.outline` | Freestyle `lineset` (thickness, colour) |
| `styling.posterize` | compositor node if > 0 |
| `shading.groundShadow` | shadow-catcher plane + `opacity`/`blur` |
| `clips` | frame counts / row order (the atlas layout lives in the pipeline, not in Blender) |
| `engine`, `samples`, `denoise` | render settings; **CI always uses `cycles` + `cpu`** |

## 5. Determinism

Determinism here means *the same input produces a visually identical output on the same pinned Blender build*, not byte equality across machines. Concrete rules:

- Pinned Blender version, checked at startup (`blender --version`), refuse on a major mismatch.
- `--factory-startup`, fixed seed (`scene.cycles.seed = 0`), `use_adaptive_sampling = False`, fixed `samples`, fixed `use_denoising` and denoiser (`OPENIMAGEDENOISE`), fixed colour management (`Standard`, pinned in the preset).
- `cycles.device = 'CPU'` in CI; GPU rendering is opt-in locally for speed and is not part of the reproducible path.
- Thread count is fixed in the driver (`render.threads_mode = 'FIXED'`), so results do not vary with the CI machine's core count.
- Verification is **perceptual**, not byte-exact: compare against a committed reference frame with a small per-channel tolerance (or SSIM ≥ threshold), because Cycles' sample ordering is stable but not bit-identical across builds and CPU architectures.

## 6. Engine policy

| Context | Engine | Why |
|---|---|---|
| CI / reproducible batch | **Cycles, CPU** | works with no display server, no GPU, no Xvfb; deterministic enough to regression-test |
| Local iteration | EEVEE **or** Cycles GPU | faster; EEVEE needs a real display/EGL context and will not run in a bare Linux container |
| Final sprite atlas | Cycles CPU, low samples + denoise | 84×72 frames do not need many samples |

EEVEE-Next requires an OpenGL/Vulkan context and fails in headless containers without EGL; do not build a CI path on it. This is the single most common way Blender CI pipelines break.

## 7. Performance budget

| Quantity | Value |
|---|---|
| Frame | the fitted per-model canvas (`--fit`); spike [#1642](https://github.com/anton-makarevich/MakaMek/issues/1642) measured 83×42 for an assembled Atlas at 1× |
| Samples | 32–64 + OIDN denoise (flat board look, no GI needs) |
| Per frame, CPU | ~1.5 s at 1× and ~3.1 s at 4× supersampling for an assembled, animated Atlas (fitted canvas, 64 samples, Freestyle) |
| Frames per model | ~60 (one row per clip; e.g. Walk = 10 frames at 12 fps) |
| Per model | a few minutes at 4×; linear in frames |
| 199 models | ~7–8 h single machine at 4×, parallelisable across models |

If this lands above the acceptable time, the levers are, in order: fewer samples (denoise compensates), fewer clips per pass, then a second machine. Writing a rasteriser is *not* on that list.

Note the resolution is a **preset value, not an architectural one**: raising `cellWidth`/`cellHeight` later (for a high-DPI or zoomed variant) costs render time linearly in pixels, and the atlas JSON carries the cell size, so nothing else changes.

## 8. Version pinning and CI

- Blender is installed by an explicit setup task (mise or a workflow step) at a **pinned version**, from the official tarball — not `pip install bpy`, which is Python-version-locked and has a known interpreter-teardown crash that surfaces as a non-zero exit after a successful render.
- A CI job renders **one 84×72 frame** of a committed fixture model and compares perceptually. That is the canary for "Blender still does what the driver expects".
- Full renders are not part of the required PR gate.

## 9. In-repo layout (Python isolation)

```
tools/blender/
  render.py            entry point: arg parsing → scene build → render → verify outputs
  scene.py             camera / lights / outline / film / animation (actions, pose reset, bounds)
  preset.py            preset → bpy settings, strict unknown-key rejection
  sheet.py             sprite atlas, contact sheet, clips.csv/json and GIF previews (Pillow, stage-2 post)
  board-topdown.json   the preset (the driver's only look/scale input besides --scale)
  README.md            how to run it locally, how to pin the version
```

This is the only Python in the repository, it is a build tool, and it has no runtime counterpart. Everything else in the pipeline stays C#: import/normalise, preset ownership, atlas packing, `.mmux` writing, CLI and MCP.

## 10. MCP: two different jobs

Both exist and neither replaces the other.

| | Batch driver (this document) | Blender MCP |
|---|---|---|
| Path | `blender -b -P render.py` | MCP client → MCP server → add-on in a **running** Blender |
| Determinism | pinned inputs, reproducible | interactive, ad-hoc |
| Used by | CI, `makamek-pipeline render` | a human or agent tuning the scene, inspecting a model, one-off previews |
| Blender support | official CLI | community servers (`mcp-for-blender`, `blender-mcp-server`) and, since 2026, an **official Blender Lab MCP server** (`blender.org/lab/mcp-server`, Blender 5.1+, add-on + external server) |

Use MCP for *authoring* the preset and diagnosing a model (wrong scale, flipped normals, missing material); never for producing the shipped frames. The official server executes LLM-generated Python with no guards — treat it as a local convenience, not a build dependency.

Agents reach the batch path through the pipeline's own MCP (`pipeline_render`), which wraps the CLI. That keeps one reproducible implementation behind two interfaces.
