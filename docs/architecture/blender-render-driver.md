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
        --out     artifacts/sprites/AS7 \
        --clips   Idle,Walk,Run,ProneEnter,ProneIdle,StandUp \
        --report  artifacts/sprites/AS7/render-report.json
```

Rules:

- `-b` — background, no UI, no display server needed.
- `--factory-startup` — ignore user preferences and add-ons; the render must not depend on whose machine it is.
- `--python-exit-code 1` — a Python exception fails the process. Additionally the driver verifies every expected file exists afterwards, because Blender can exit `0` with partial output.
- `--` — everything after it is parsed by the driver, not by Blender.
- The `.blend` is **not** an input and not checked in. The scene is constructed by the script from the preset, so the scene is diffable text, not an opaque binary.

Optional flags: `--engine cycles|eevee`, `--samples N`, `--resolution N`, `--save-blend path`, `--frame N` (single-frame smoke test), `--no-outline`, `--device cpu|gpu`.

## 3. Scene contract

Built by the driver, every run, from the preset:

| Element | Rule |
|---|---|
| Model | `bpy.ops.import_scene.gltf(filepath=…)`; apply the manifest's volume correction scale; position so the **footprint centre** is at the world origin, ground at `z = 0` |
| Camera | Orthographic, pitch 90°, yaw 0°, `ortho_scale` derived from the preset's `pixelsPerMetre` so all chassis share one scale |
| Lights | Key + fill + rim parented to the camera, fixed intensities — identical for every mech and every frame (facing is a runtime rotation, so lighting must not vary with it) |
| Materials | Imported glTF materials used as-is; a preset `materialOverride` may flatten them to a two-tone board look |
| Outline | **Freestyle** line set (silhouette + crease), width/colour from the preset — this is what replaces the current 2D line art |
| Film | `transparent = True`, RGBA PNG |
| Frame range | Per clip: `[start, start + frames - 1]`, one clip per render pass |
| Ground | Optional shadow catcher for a soft contact shadow |

Nothing in the scene is hand-edited between runs. If the scene needs a change, it is a preset change or a driver change.

## 4. Preset → scene mapping

The preset stays renderer-agnostic in shape; the driver translates the keys it understands and fails loudly on unknown ones (a typo must not silently render the default look).

| Preset key | Blender setting |
|---|---|
| `camera.pitchDegrees`, `camera.yawDegrees` | camera rotation |
| `scale.*` | `ortho_scale`, model `scale` (applied before framing) |
| `output.cell`, `output.supersample` | `render.resolution_x/y`, `resolution_percentage` |
| `output.background` | `film_transparent` |
| `lights[]` | lamp objects + intensities |
| `styling.outline` | Freestyle `lineset` (thickness, colour) |
| `styling.posterize` | compositor node if > 0 |
| `shading.groundShadow` | shadow-catcher plane + `opacity`/`blur` |
| `clips` | frame counts / row order (the atlas layout lives in the pipeline, not in Blender) |
| `engine`, `samples`, `denoise` | render settings; **CI always uses `cycles` + `cpu`** |

## 5. Determinism

Determinism here means *the same input produces a visually identical output on the same pinned Blender build*, not byte equality across machines. Concrete rules:

- Pinned Blender version, checked at startup (`blender --version`), refuse on a major mismatch.
- `--factory-startup`, fixed seed (`scene.cycles.seed = 0`), `use_adaptive_sampling = False`, fixed `samples`, fixed `use_denoising` and denoiser (`OPENIMAGEDENOISE`), fixed colour management (AgX or Standard, pinned in the preset).
- `cycles.device = 'CPU'` in CI; GPU rendering is opt-in locally for speed and is not part of the reproducible path.
- Thread count is fixed in the driver (`render.threads_mode = 'FIXED'`), so results do not vary with the CI machine's core count.
- Verification is **perceptual**, not byte-exact: compare against a committed reference frame with a small per-channel tolerance (or SSIM ≥ threshold), because Cycles' sample ordering is stable but not bit-identical across builds and CPU architectures.

## 6. Engine policy

| Context | Engine | Why |
|---|---|---|
| CI / reproducible batch | **Cycles, CPU** | works with no display server, no GPU, no Xvfb; deterministic enough to regression-test |
| Local iteration | EEVEE **or** Cycles GPU | faster; EEVEE needs a real display/EGL context and will not run in a bare Linux container |
| Final sprite atlas | Cycles CPU, low samples + denoise | 384² frames do not need many samples |

EEVEE-Next requires an OpenGL/Vulkan context and fails in headless containers without EGL; do not build a CI path on it. This is the single most common way Blender CI pipelines break.

## 7. Performance budget

| Quantity | Value |
|---|---|
| Frame | 384×384, RGBA, transparent |
| Samples | 32–64 + OIDN denoise (flat board look, no GI needs) |
| Per frame, CPU | ~1–3 s |
| Frames per chassis | ~60 (6 clips) |
| Per chassis | 1–3 min |
| 76 chassis | 1.5–4 h single machine, parallel across chassis |

If this lands above the acceptable time, the levers are, in order: fewer samples (denoise compensates), smaller cell (256² upscaled), fewer clips per pass, then a second machine. Writing a rasteriser is *not* on that list.

## 8. Version pinning and CI

- Blender is installed by an explicit setup task (mise or a workflow step) at a **pinned version**, from the official tarball — not `pip install bpy`, which is Python-version-locked and has a known interpreter-teardown crash that surfaces as a non-zero exit after a successful render.
- A CI job renders **one frame** of a committed fixture model at 64×64 and compares perceptually. That is the canary for "Blender still does what the driver expects".
- Full renders are not part of the required PR gate.

## 9. In-repo layout (Python isolation)

```
tools/blender/
  render.py            entry point: arg parsing → scene build → render → verify outputs
  scene.py             camera / lights / outline / film construction
  preset.py            preset → bpy settings, strict unknown-key rejection
  README.md            how to run it locally, how to pin the version
presets/
  board-topdown.json
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
