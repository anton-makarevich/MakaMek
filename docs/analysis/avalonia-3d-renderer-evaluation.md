# Avalonia 3D Renderer — Alternative to MonoGame for the Sprite Pipeline

Evaluation of Avalonia-hosted 3D rendering as the stage-2 renderer, replacing the MonoGame decision in [../architecture/asset-pipeline.md](../architecture/asset-pipeline.md) §4.1.

Date: 2026-10-25 · Status: **superseded for the renderer decision** — the pipeline renders with Blender automation instead ([../architecture/blender-render-driver.md](../architecture/blender-render-driver.md) §11 records why). The survey below is kept because it is still the reference for any *interactive* 3D we build later (asset previewer, possible Avalonia-hosted 3D client), and because it documents which Avalonia 12.1.4 APIs actually exist.

---

## 0. Outcome (read this first)

The question this document asked — "can Avalonia host the sprite renderer?" — was the wrong question. The renderer does not need to be built at all: **Blender already is a renderer**, headless, scriptable, with skinning, denoising, AO and Freestyle outlines, and its glTF importer consumes exactly the canonical model stage 1 produces. Driving it is a ~1-day script and a preset; a rasterizer is ~2 weeks plus a quality ceiling.

Blender also has MCP support, in two flavours, neither of which is a batch path:

| | Batch driver (chosen) | Blender MCP |
|---|---|---|
| Entry point | `blender -b --factory-startup -P render.py -- …` | MCP client → server → add-on in a **running** Blender |
| Deterministic / reproducible | yes | no (interactive) |
| Used for | CI, shipped frames | authoring the preset, diagnosing a model, one-off previews |
| Availability | official CLI | official Blender Lab server (Blender 5.1+, add-on + external server) and community servers (`mcp-for-blender`, `blender-mcp-server`) |

So the correct split is: **C# for the pipeline (import, normalise, pack, CLI/MCP), Blender+Python for rendering, MCP for authoring.** Sections 2–3 below remain accurate as an API survey. Sections 4–5 and §8 are superseded by §9. Section 6 and the rasterizer risks in §7 describe the rejected option and are not current guidance.

---

## 1. Question

The pipeline design picked MonoGame (DesktopGL) as the sprite renderer, justified by "it is the engine of the planned native 3D client". Two problems with that justification:

1. It optimises for a **hypothetical** future while the repository runs Avalonia 12.1.4 today, on five heads (desktop, browser, Android, iOS, headless tests).
2. It gives up the option of an **asset previewer** — a UI that shows a canonical model, plays its clips and compares against the shipped sprite. With MonoGame, that UI would be a second application with a second graphics stack.

So: can the renderer live in the Avalonia stack, headless enough to be a batch tool?

---

## 2. What "3D in Avalonia" actually means

Avalonia has no `Viewport3D` (that is WPF/XPF parity work, explicitly listed as unshipped). "3D in Avalonia" is always one of four things:

| Option | Mechanism | Verified in this solution |
|---|---|---|
| **A. OpenGL control** | `Avalonia.OpenGL.Controls.OpenGlControlBase` + any GL binding (Silk.NET is the community standard; Avalonia's own `GlInterface` is deliberately minimal) | ✅ `Avalonia.OpenGL.dll` 12.1.4 contains `OpenGlControlBase`, `OnOpenGlRender`, `IGlContext` |
| **B. Skia interop** | `DrawingContext.Custom(ICustomDrawOperation)` → `ISkiaSharpApiLeaseFeature` → raw `SKCanvas` inside Avalonia's own render pass | ✅ `ICustomDrawOperation` in `Avalonia.Base.dll`; `ISkiaSharpApiLeaseFeature`/`ISkiaSharpApiLease` in `Avalonia.Skia.dll` |
| **C. Third-party 3D control** | e.g. `HelixToolkit.Avalonia.SharpDX` | ⚠️ exists (3.1.2) but renders through **SharpDX/Direct3D 11 → Windows only**, pinned to Avalonia ≥ 11.2 |
| **D. Native window embedding** | host a Silk.NET/Veldrid `Swapchain` in an embedded OS window (e.g. `KillzXGaming/AvaloniaRender`) | ❌ not a composited control; breaks layout, input, clipping, and has no headless story |

Dependency facts that matter (read from the pinned packages, not from memory):

- `Avalonia.Skia` 12.1.4 depends on **SkiaSharp 3.119.4** — already in the graph, so a direct SkiaSharp reference costs nothing new.
- `Avalonia.Headless` 12.1.4 exposes `CaptureRenderedFrame`, `HeadlessWindowExtensions`, `UseHeadlessDrawing`, `HeadlessPlatformRenderInterface`, `ForceRenderTimerTick`.
- The repository **already renders real pixels headlessly**: `tests/MakaMek.Avalonia.AppTests/AppHarness.cs` configures `.UseSkia()` + `.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })` precisely so `CaptureRenderedFrame` returns pixels rather than an empty surface.

That last point is the decisive piece of evidence: "render Avalonia content without a window or GPU" is not a hope, it is a test harness that exists in this repo.

---

## 3. Option assessment

Scoring is for *this* job — offline sprite generation from a canonical glTF, 76 chassis × ~60 frames, on CI.

| | A. OpenGlControlBase | B. Helix SharpDX | C. Native window | D. Skia / CPU | E. MonoGame |
|---|---|---|---|---|---|
| Runs headless (no window) | ❌ needs context + drawing surface | ❌ D3D11 device + swapchain | ❌ | ✅ | ⚠️ hidden window / Xvfb |
| Runs on Linux CI container | ⚠️ Xvfb + GL | ❌ | ❌ | ✅ | ⚠️ |
| Deterministic output | ⚠️ driver-dependent AA/rounding | ⚠️ | ⚠️ | ✅ pure C#, fixed-point-able | ⚠️ driver-dependent |
| Platform coverage (app) | desktop + mobile, **not WASM** | Windows only | desktop | all, incl. WASM | desktop + mobile |
| Previewer for free | ✅ real-time | ✅ (Windows only) | ❌ | ✅ (frame-rate dependent) | ❌ separate app |
| New dependency weight | Silk.NET + native GL | SharpDX (unmaintained) + D3D11 | Veldrid/Silk | **SkiaSharp (already transitive)** | MonoGame + SDL/GL |
| Style control (flat/cel look) | shaders | material system | shaders | ✅ full control of the pixel | basic |
| Effort | medium (GL state, capture path) | low | high | medium (rasterizer) | medium |
| Reused by a future 3D client | preview only | preview only | no | yes, as fallback renderer | yes |

**A (OpenGL)** is the obvious "real 3D" answer and is fine for a live preview, but it is the worst fit for the actual deliverable: the batch renderer. Capturing GL content requires a live surface and a readback path threaded through `OnOpenGlRender`, which means a window — the exact thing the batch job must not need. It also excludes WASM.

**B (HelixToolkit)** is worth naming only to dismiss: Direct3D 11 means Windows-only rendering, SharpDX is unmaintained, and the package targets Avalonia 11.x. Fine for a Windows-only viewer, useless for a cross-platform pipeline.

**C (native window embedding)** breaks the composition model and cannot be captured headlessly at all.

**D (Skia/CPU)** inverts the problem: instead of asking "how do I get pixels out of a GPU surface", it produces pixels in managed memory and then uploads them. Capture is a memory copy. Determinism is a property of our code, not of a driver. And the same routine can be blitted into an Avalonia control for the previewer.

**E (MonoGame)** remains the right answer for *interactive runtime* 3D — 60 fps, full mesh, full scene. It is the wrong answer for an offline frame factory, and its stated advantage (matching the future client) is worth less than it looked now that the client could plausibly be Avalonia-hosted itself.

---

## 4. Recommended architecture

> **Superseded** — this was the Avalonia recommendation; the adopted design is Blender automation (§9). Kept because the layering argument still applies if an interactive viewport is built.

Three layers, only the middle one knows about Avalonia:

```
MakaMek.Pipeline.Render                 pure C#, zero graphics dependencies
  Scene / Camera / Skeleton          ← glTF-canonical model (from stage 1)
  Skinning (CPU, per frame)
  Rasterizer (z-buffer, 2–4× supersample per axis)
  Shading (lambert + ambient + rim, flat/cel option)
  Outline (depth+normal discontinuity edge detect)
  FrameBuffer → int[] RGBA                 ← testable by golden images
```

> **Not applicable in the adopted design.** There is no FrameBuffer, no `WriteableBitmap` consumer and no `--via-avalonia` parity check: Blender writes PNG frames and the pipeline assembles them (§9). If an interactive viewport is ever built, this section is where to start — and then the pixel contract would need pinning (channel order, premultiplied vs straight alpha, row stride, top-left origin) before three consumers touch it.

```text
MakaMek.Pipeline.Render.Avalonia        Avalonia host (thin)
  MechViewport : Control                  WriteableBitmap blit, orbit/zoom, clip scrubber
  SpriteSheetPreview : Control            atlas playback, hex overlay, scale guides

MakaMek.Pipeline.Cli / .Mcp             batch
  render → FrameBuffer → PNG (SkiaSharp 3.119.4) → atlas
```

Why this shape:

- **The renderer core has no framework dependency at all.** It is `System.Numerics` + arrays, so it is unit-testable with golden images at 64×64, sits inside the repo's coverage policy like any other logic assembly, and cannot drift when Avalonia ships a new renderer.
- **The previewer is a host, not a reimplementation.** `MechViewport` calls the same `Renderer.RenderFrame(scene, camera, clip, time)` the CLI calls. No "looks different in the tool" class of bug.
- **Capture is trivial.** Batch mode needs no window, no GL context, no Xvfb: `RenderFrame` returns a buffer, SkiaSharp encodes it. CI-friendly on any runner.
- **Parity is checkable** by re-rendering the same frame through the real Avalonia control under `Avalonia.Headless` and comparing hashes; if those diverge, the previewer is lying. (No such flag exists in the adopted CLI.)

### 4.1 What the rasterizer needs (and does not)

Needed: per-vertex skinning, backface culling, perspective-correct-enough interpolation (orthographic top-down, so affine is exact), z-buffer, per-pixel lambert + ambient + optional rim, 2×2 or 4×4 supersample, alpha output, optional outline pass, optional ground shadow (silhouette projected and blurred — cheap and enough at board size).

Not needed: PBR, IBL, normal mapping at sprite scale, shadows with penumbra, post-processing chains, GLSL. The target is an 84–384 px silhouette on a hex, not a turntable render.

### 4.2 Cost estimate

| Quantity | Value |
|---|---|
| Frame cell | 384² at 4 samples/px (2×2) → ~590k samples; a 4×4 grid (16 samples/px) → ~2.36M |
| Sprite LOD mesh (stage 1 emits it) | ≤ 15k triangles |
| Frames per chassis | ~60 (6 clips) |
| Per chassis | a few seconds of CPU |
| 76 chassis, single machine | minutes, parallelisable across cores by chassis |

Triangle-setup bound rather than fill bound (small triangles), which is exactly the case managed code handles well.

---

## 5. What this changes, and what it does not

> **Superseded** — see §9 for the changes that were actually made.

**Changes** in [../architecture/asset-pipeline.md](../architecture/asset-pipeline.md):

- §4.1 renderer: MonoGame → framework-free rasterizer + Avalonia host; `OpenGlControlBase` (Silk.NET) kept as an optional high-fidelity preview backend, not the batch path.
- Project layout: `MakaMek.Pipeline.Render` (pure) + `MakaMek.Pipeline.Render.Avalonia` (host) + `MakaMek.Pipeline.Previewer` (optional Avalonia app).
- Testing: the renderer core joins the covered assemblies (golden images); only the Avalonia host is excluded.
- Phasing: the previewer can arrive *before* the batch path, because it shares the core — which makes P2's go/no-go judgement much cheaper (drag a model in, look at it, no script).
- Risk register: "headless GL in CI" and "MonoGame skinned-animation limits" disappear; replaced by "rasterizer correctness" and "preview frame rate".

**Does not change:** the canonical glTF model, the clip vocabulary and synthesis rules, source adapters, the tonnage-volume scale chain, presets, `.mmux` v2, or the CLI/MCP surface. (The `--via-avalonia` flag discussed above belonged to *this* proposal; the adopted Blender driver adds no such flag.) The renderer is a pluggable backend behind `IRenderBackend` — which is why this can be revisited again later.

---

## 6. Implications for the future 3D client

> **Superseded** — this section reasons about the rejected CPU rasterizer. The offline sprite renderer is Blender automation (§9).

This investigation does not decide the client, but it de-risks the options:

- If the 3D client is **MonoGame**, it consumes the same canonical `.glb` — stage 1 is unchanged. The offline sprite renderer stays Blender automation (§9), not the CPU rasterizer.
- If the 3D client is **Avalonia-hosted**, the preview viewport is already the first version of it, and `OpenGlControlBase` becomes the runtime path (desktop + mobile; WASM would need a different story).
- Either way, keeping the canonical model engine-neutral means the sprite pipeline survives whichever way that decision lands.

An explicit non-goal: this rasterizer is **not** a runtime renderer. Interactive 3D at 60 fps with full-detail meshes and a real scene stays on the GPU.

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| Rasterizer correctness (clipping, z-fighting, seams) | Golden-image tests on fixtures; the fixture is an orthographic top-down view, so near-plane clipping is unnecessary by construction (no geometry approaches the camera plane) |
| Visual quality ceiling (no PBR, no soft shadows) | Validate at 84×72 in P2; outline + rim + posterize is the intended look, not a compromise |
| Preview frame rate on the CPU | Preview at 512² with the sprite LOD mesh; if it is not smooth enough, add the `OpenGlControlBase` preview backend behind the same interface |
| Avalonia host churn (12.x → 13) | Runtime renderer core has no Avalonia dependency; the host is ~200 lines |
| Two render paths drifting | (Superseded — no second path in the adopted design) if an interactive viewport is built, add a headless capture parity check in CI on one fixture |
| SkiaSharp version skew | Pin 3.119.4 to match `Avalonia.Skia` |
| Scope creep into a mini-engine | The rasterizer is a sprite tool: no scene graph, no asset hot-reload, no editor — the previewer reads files and draws them |

---

## 8. Recommendation (superseded — see §0 and §9)

Kept for the record; the decision that was actually taken is in §9.

1. ~~Switch stage 2 to the Avalonia/Skia CPU path.~~ Superseded: stage 2 is Blender automation.
2. ~~Build the previewer first.~~ Deferred: Blender's own viewport plus its MCP server *is* the interactive previewer for now; an in-app asset browser can come later and only needs 2D sprite/atlas display.
3. **Keep `IRenderBackend`.** Still true: if an interactive 3D viewport is added later (previewer or runtime), `OpenGlControlBase` + Silk.NET is the credible path on desktop/mobile, and this document is the survey for it.
4. **Keep the canonical `.glb`.** Still true, and now load-bearing: it is Blender's input format.

---

## 9. Blender automation compared (the option that won)

Scored for the same job: offline sprites, 76 chassis × ~60 frames, reproducible from CI.

| | Blender automation | C# rasterizer | Avalonia OpenGL | MonoGame |
|---|---|---|---|---|
| Code to own | ~300 lines Python driver + preset | ~500–800 lines C# + shading/AA/outline + tests | medium (GL state, capture) | medium |
| Quality ceiling | path tracing, AO, soft shadows, denoise, **Freestyle outlines** | flat/Gouraud + edge detect | high (shaders) | basic/medium |
| Skinning / glTF / animation | free, best-in-class importer | hand-written | hand-written | via pipeline |
| Headless in a bare CI container | ✅ `-b`, Cycles CPU | ✅ | ❌ needs surface | ⚠️ Xvfb |
| Determinism | visually stable, not bit-exact | bit-exact | driver-dependent | driver-dependent |
| Cost per frame | ~0.2–1 s CPU at 84×72 | ms | ms (GPU) | ms (GPU) |
| New dependency | Blender binary (pinned), Python | none | Silk.NET + GL | MonoGame + SDL |
| Language consistency | breaks it (one Python tool) | keeps it | keeps it | keeps it |
| MCP story | ✅ official Blender Lab MCP + community servers | none | none | none |

The trade is **~300 lines of Python and a pinned 300 MB tool** against **~800 lines of C# renderer plus an outline/AA quality ceiling**. Given that outline quality is the single largest risk to the whole art direction ("will it read at 84×72 instead of clean line art?"), Freestyle is worth more than language purity — and the Python is a build tool, not product code.

Blender it is, unless P2a shows Cycles CPU is too slow at 76 × 60 frames; the mitigation ladder there is samples → resolution → clips, not a rewrite.

### What survives from this investigation

- **`OpenGlControlBase`, `ICustomDrawOperation`, `ISkiaSharpApiLeaseFeature`, `Avalonia.Headless.CaptureRenderedFrame` are real in 12.1.4** — verified in the packages — so an interactive 3D viewport inside the Avalonia app is a known-quantity option, not a research project.
- **HelixToolkit.Avalonia.SharpDX is a trap** (Direct3D 11 → Windows-only rendering, unmaintained SharpDX, Avalonia 11.x).
- **`Avalonia.Headless` + `UseSkia()` already renders pixels in this repo** (`tests/MakaMek.Avalonia.AppTests/AppHarness.cs`), which is the tool to reach for if we ever want UI-level sprite/atlas regression screenshots.
- **Blender MCP is an authoring tool, not a build step.** The official server executes LLM-generated Python with no guards; CI must never depend on it.

### Revisit triggers

Reopen the renderer question only if: per-frame cost makes the 76-chassis batch impractical, Blender's determinism proves unusable for regression testing, or an in-app interactive 3D viewport becomes a product requirement (then §2–§4 apply directly).
