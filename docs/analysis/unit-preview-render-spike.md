# Unit Preview Image — Requirements Analysis & Spike Findings

Pre-implementation analysis for [#1831](https://github.com/anton-makarevich/MakaMek/issues/1831): a second,
non-board render (`unit-preview.png`) for unit cards, squad tiles and the record sheet.

Date: 2026-11-28 · Status: spike verdict — **tonnage-volume scale accepted (Atlas = 100 %), front 10°/0°, 128 px** · Branch:
`spike/1831-unit-preview-image` · Related:
[#1832](https://github.com/anton-makarevich/MakaMek/issues/1832) (rear view / `views` schema),
[asset-pipeline.md](../architecture/asset-pipeline.md) §2.5/§4/§5,
[blender-render-driver.md](../architecture/blender-render-driver.md) §3/§4,
[mw5-model-extraction.md](mw5-model-extraction.md) §4.5.

---

## 1. Clarified requirements (supersede the issue where they differ)

| Item | Issue #1831 | Clarified requirement |
|---|---|---|
| Canvas | 256 × 256 at 1× | **Accepted: 128 × 128 at 1×**, 4× supersampled then downsampled. 64×128 and 128×64 were evaluated and rejected; 96×128 is the only viable portrait alternative (§3.3). HiDPI can be regenerated later if needed |
| Colour | authored albedo / `rgba`; luminance only for board | **Luminance only.** The runtime owner tint must still apply, exactly as it does to `unit.png`. No albedo; keep `board-look` and neutral white lights |
| Camera | fixed 40° pitch / 35° yaw | **Accepted: front elevations only, 10° pitch / 0° yaw. No iso.** |
| Scale | one global `pxPerMetre`, derive from tallest chassis | **Accepted: tonnage-volume correction, with the Atlas as the fixed 100 t / 100 % reference.** Measured once at import and reused by every render product. `uniform` and `--fit` remain comparison/fallback options |
| Pilot set | AS7-D, COM-2D, LCT-1V | **Atlas + Locust** for the comparison (the local assembled Locust is `LCT-1E`; variants share the chassis mesh, so the chassis comparison is valid) |

The rest of the issue stands: one static preview per **chassis** (variants share it), one optional package
entry, two UI sites, no animation, no board-sprite change.

> **Accepted scale decision (2026-11-28).** Previews use the same **tonnage-volume correction** as the board
> sprite. The **Atlas is the fixed reference: 100 t = 100 % and it is never rescaled.** Every other chassis
> is adjusted relative to it using its measured volume; the ratio is computed **once at import** and stored
> in `model.json`, then reused by every render product (board sprite, preview, future outputs). Only the
> product's `pxPerMetre` calibration differs — plan-view extent for the board, front-silhouette extent for
> the preview. The issue's "the preview must not use the tonnage-volume chain" is **superseded**: the
> preview uses the same correction, not a second one.

> **Important correction to the issue's premise.** "A second preset plus a second output artefact, not new
> renderer work" is not accurate. The shipped driver is hard-coded to a top-down camera and a
> camera-parented light rig; a front camera needs driver work (see §4). The preset loader also has no
> `preview`/`views` keys yet, and it *requires* `output.channels = "luminance"` — which, after the
> clarification above, is exactly what we want.

> **Facing correction (spike finding).** The first pass rendered the **rear** because directional lighting
> made the back look like the front. An ambient two-sided probe settled it: canonical models face **−Y** in
> Blender space (glTF +Z forward maps to Blender −Y), as the architecture docs say. The preview camera is at
> **−Y looking +Y**; the front camera must be `yaw 0` in a convention where 0 means "from the front
> (−Y)", not the `+Y` convention the old spike driver used.

---

## 2. Spike method

Rendered on Blender **5.2.3 LTS**, Cycles CPU, 48 samples + OIDN, 4× supersampling (512²) downsampled to
128². Look reused from `tools/blender/scene.py` (board-look material, Freestyle silhouette, `Standard` view
transform, transparent film); only the camera and light placement were spike-local, because the shipped
driver cannot frame a non-top-down camera.

Models: assembled static `AS7-D.glb` (Atlas, 15.3 × 7.8 × 20.0 m) and
`A2M2_Zombie_LCT-1E.glb` (Locust, 5.1 × 4.7 × 9.6 m). Both are single-mesh, no armature. Output lives in
`artifacts/spike/preview/` and is **not committed** (MW5-derived).

Framing recipe used for every view:

```text
ortho camera, ortho_scale = 128 (1 model unit = 1 px after --scale)
front is -Y; d = (−sin(yaw)·cos(pitch), −cos(yaw)·cos(pitch), sin(pitch))
camera at target + d·distance, aimed at the model
then shifted along the camera up-axis so the lowest silhouette pixel sits `margin` px above the bottom
```

### Measured silhouettes at 128 × 128 (base 5.2 px/m, front views only)

| View (pitch/yaw) | Atlas px (w×h) | Atlas margins L/R/T/B | Locust px (w×h) | Locust margins L/R/T/B |
|---|---|---|---|---|
| 0 / 0 | 82 × 106 | 23/23/15/7 | 28 × 52 | 50/50/69/7 |
| **10 / 0** | **82 × 108** | **23/23/12/8** | **28 × 54** | **50/50/66/8** |
| 20 / 0 | 82 × 107 | 23/23/13/8 | 28 × 55 | 50/50/64/8 |

Cross-checks at other scales (front 10°):

| Render | px/m | Atlas | Locust | Note |
|---|---|---|---|---|
| Uniform | 5.2 | 82 × 108 | 28 × 54 | 42 % of the canvas for the Locust |
| Volume-corrected, hole-filled | 8.1 (×1.56) | — | 44 × 83 | 65 % of the canvas |
| Fitted | 11.65 | — | 62 × 118 | **touches the top edge** (0 px margin) |
| Fitted, corrected | 11.0 | — | 58 × 112 | 6 px top margin, acceptable |

**Edge check:** a bounding-box-corner projection is not a silhouette. It predicted a 130 px height for a
3/4 view that actually renders 98 px. More importantly, the 11.65 px/m fitted Locust genuinely touches the
top edge (top margin 0) because the front silhouette is taller than the rear one it was calibrated on.
The edge-touch assertion must measure the **rendered alpha / projected vertices**, and fitting must be
recomputed at the actual view.

---

## 3. Accepted decisions

### 3.1 Camera: straight front, 10° pitch

**Decision (accepted): front only, 10° pitch / 0° yaw, no iso.**

Front-only, 0° vs 10° vs 20°: all three are readable; 10° adds shoulder planes, foot tops and a little
torso depth while preserving bilateral symmetry, so a future mirrored tint pattern still maps cleanly. 0° is
slightly flatter; 20° starts to look down at the mech and hides the chest under the shoulder pods. 3/4 iso
is out of scope per the clarification.

"Camera ~10 m above ground" does not survive contact with an orthographic camera: position along the view
axis is irrelevant, only the angle matters. A camera 10 m above ground aimed at a 20 m Atlas's mid-body is
level (0°); aimed at a 9.6 m Locust's mid-body it is ~5–10° down at plausible distances. A fixed **10°** is
the closest comparable interpretation; a literal camera height must be converted into a pitch in the
preset, never into a camera position the driver reads.

### 3.2 Scale: tonnage-volume, computed once and shared

**Decision (accepted):** the preview uses the existing `scale.mode: "tonnage-volume"` chain from
[asset-pipeline.md](../architecture/asset-pipeline.md) §4.3. The **Atlas is the fixed reference**
(100 t = 100 %, correction `1.0`); every other chassis is normalized to the Atlas's measured volume. The
correction is **product-independent**: it is measured once at import, stored in `model.json`, and reused by
the board sprite, the preview and any future render output. The preview must not re-measure volume, shrink
the Atlas, or own a second correction.

**Can Blender measure it? Yes.** No extra library is needed; we measured five ways on the assembled GLBs:

| Method | Atlas (m³) | Locust (m³) | Locust / Atlas | Linear correction to reach 0.20 | Locust at 128 (from 54 px) |
|---|---|---|---|---|---|
| Bounding box | 2388.7 | 226.4 | 0.0948 | ×1.28 | 69 px |
| bmesh signed volume | 682.8 | 33.1 | 0.0485 | ×1.60 | 87 px |
| bmesh after hole fill | 528.7 | 27.9 | 0.0528 | ×1.56 | 84 px |
| Convex hull | 2112.0 | 158.4 | 0.0750 | ×1.39 | 75 px |
| Voxel solid (128² ray columns) | 318.9 | 22.5 | 0.0704 | ×1.42 | 77 px |

Interpretation:

- **The actual volume ratio is ~0.05–0.09, nowhere near 0.20.** With the Atlas pinned as the 100 % reference,
  the Locust is much smaller than its 20 t implies, so it must be scaled **up ~1.3–1.6× linearly** relative
  to the Atlas. The Atlas itself never moves.
- The method spread is ±7 px at 128 (≈ ±3 px at 44). That is acceptable, but the method must be **pinned
  and recorded**, because it changes the correction by ~20 %.
- Raw `bmesh` signed volume is unreliable here: the game meshes are open (feet/legs), so the sign/closure
  assumptions fail. Hole-filling and voxelisation agree within ~10 %; the bounding box is a coarse upper
  bound. The docs already choose voxel for board sprites; reuse that method at import and store
  `volume.modelCubicMeters` + `volume.correctionScale` in `model.json`.
- **Volume correction equalizes 3D volume, not front-elevation height.** The Locust is narrow, so after
  correction its height lands at ~77–84 px vs the Atlas's 108 px (71–78 %), not at the tonnage ratio
  `(20/100)^(1/3) = 58.5 %`. That is the intended behaviour of volume normalisation; it is worth stating
  explicitly because a front render makes height the perceived size.

#### One correction at import, one calibration per render product

```text
at import (once per catalog, reference = AS7):
  correctionScale_ref = 1.0
  correctionScale_i   = ((tonnage_i / tonnage_ref)
                         × (modelVolume_ref / modelVolume_i))^(1/3)

per render product (the same correction is reused):
  pxPerMetre_board    = (referenceCoverage × drawBoxHeightPx)
                        / planViewExtent_ref
  pxPerMetre_preview  = usablePx
                        / projectedFrontHeight_ref
  effectiveScale_i    = pxPerMetre_global × correctionScale_i × override_i × floorClamp_i
```

`pxPerMetre_global` is exactly what a hand-calibrated `--scale` is: **one multiplier for every chassis**.
It can be layered on top of the correction to tune the overall size, but it cannot change the Atlas:Locust
ratio — that ratio comes only from `correctionScale_i`. Do not encode hand calibration as a per-chassis
value; it is the same number for every model.

Density cancels out of this relative form and remains provenance for the 3D client only. The Atlas has
`correctionScale = 1.0`, so it never shrinks or grows; a 100 t chassis with a different mesh volume would
still be adjusted (its volume/nominal ratio differs), but the reference is pinned. Pinning the correction
is not enough on its own: the preset's `referenceCoverage`/`pxPerMetre` must also stay anchored to the
Atlas's current output, so introducing the correction never moves the Atlas.

The only difference between the board sprite and the preview is `pxPerMetre`: the board calibrates on the
plan-view extent (what fills a hex from above), the preview calibrates on the front silhouette height at
the preview pitch. Both multiply the **same** `correctionScale` from the manifest, so a re-import updates
every render product at once. This is the "adjust once, reuse for all renders" rule; a future record-sheet
or squad-tile render is another calibration, not another correction, and reuses the preview image unless it
actually needs a different camera.

Measured outcomes at the two UI sizes (128 art, 84 card, 44 tile):

| Policy | Atlas @128 | Locust @128 | Atlas @84 | Locust @84 | Atlas @44 | Locust @44 |
|---|---|---|---|---|---|---|
| Uniform (5.2 px/m) | 108 px | 54 px (42 %) | 71 px | 35 px | 37 px | **19 px** |
| **Tonnage-volume (×1.42–1.56)** | 108 px | **77–84 px (60–66 %)** | 71 px | 51–54 px | 37 px | **26–29 px** |
| Fitted per chassis | 108 px | 112 px | 71 px | 73 px | 37 px | 38 px |
| 65 % floor only (no volume) | 108 px | 83 px | 71 px | 54 px | 37 px | 28 px |

The accepted Locust render is 44 × 83 px at 128 (65 % of the canvas). With the pinned voxel method the
correction lands at ~77 px and the 65 % floor raises it to 83; the hole-filled measurement lands at 84 px
directly. Either way the accepted size is the same. It reads clearly at 84 and remains usable at 44. The
floor stays as a readability guard for any future light chassis whose corrected size still lands too
small. The report must show both `volumeCorrection` and `appliedFloor`. Use a distinct preset field,
`scale.minimumHeightCoverage: 0.65` (height-based), rather than reusing the board's area-based
`minimumCoverage`.

Derivation at the accepted 10°/0° view, 8 px margins. The measured table above is at the 5.2 px/m
base; calibrating the Atlas to the 112 px usable height gives 5.39 px/m and scales both models by 1.036:

```text
Atlas measured at 5.2 px/m       = 108 px
Atlas calibrated to 112 px       = 5.39 px/m
Locust uniform                   = 54 × 1.036 ≈ 56 px
volume ratio r                   = V_LCT / V_AS7 ≈ 0.053–0.070 (method-dependent)
linear correction                = (0.20 / r)^(1/3) ≈ 1.42–1.56
Locust corrected                 ≈ 80–87 px
65 % floor                       = 0.65 × 128 = 83 px
accepted render                  = 83 px (8.1 px/m on the 5.2 base, ×1.56)
```

The Atlas stays at 108 px on the 5.2 base and 112 px when calibrated; it is never shrunk.

If the import-time volume is not yet implemented, the preview falls back to `uniform` with the floor and the
report marks `volumeCorrection: null`; it must not measure volume itself. The import-time measurement is the
single source of truth.

### 3.3 Canvas aspect: 64×128 evaluated, 128×128 retained

The proposal was a **64 × 128 portrait** canvas (mechs are usually taller than wide), not a 128 × 64
landscape. Both UI slots are square — 84 × 84 on the card and 44 × 44 on the squad tile — and draw with
`Uniform`, so a portrait source is letterboxed to the slot height.

The Atlas is the problem: at the accepted preview scale it is **82 × 108 px** (aspect ≈ 0.76), not 2:1. The
Locust is 44 × 83 (aspect ≈ 0.53). A 64-wide canvas therefore cannot hold the Atlas at full height: with
6 px margins the global scale has to drop to 52/82 = **0.634**, making the Atlas 52 × 68 and leaving 54 px of
empty height. Fitting each mech to the 64 × 128 frame instead would let the Locust fill 98 px of height while
the Atlas stays at 68 — the Locust would appear **taller than the Atlas**, breaking the reference rule.

| Canvas / mode | Display in 84 × 84 | Mech height on card (A / L) | Mech height on tile (A / L) | PNG A / L |
|---|---|---|---|---|
| **128 × 128** | 84 × 84 | 71 / 54 px | 37 / 28 px | 12.7 / 6.0 KB |
| 96 × 128 (crop) | 63 × 84 | 71 / 54 px | 37 / 28 px | 12.4 / 5.9 KB |
| 64 × 128, global scale | 42 × 84 | 45 / 34 px | 23 / 18 px | 6.9 / 3.3 KB |
| 64 × 128, per-mech fit | 42 × 84 | 45 / 64 px | 23 / 34 px | 6.9 / 9.0 KB |
| 128 × 64, landscape | 84 × 42 | 34 / 34 px | 18 / 18 px | 4.5 / 3.2 KB |

- **64 × 128 with the correct global scale** preserves the Atlas:Locust ratio but shrinks the card mech from
  71 → 45 px and the tile from 37 → 23 px; the Locust tile falls to 18 px, undoing the volume correction.
  It saves ~45 % of the preview PNG (3–7 KB vs 6–13 KB), but the preview is not the file-size driver.
- **64 × 128 with a per-mech fit** is worse: the Locust becomes 64 px on the card vs the Atlas's 45 px.
- **96 × 128** is the portrait canvas that matches the reference Atlas aspect (≈ 0.75). It is visually
  identical to 128 × 128 on screen (same mech heights) and saves only ~2 %, because empty transparent
  columns compress almost for free. It is the only portrait worth considering.
- **128 × 64 landscape** halves the on-screen mech and is the worst option.
- A 64 × 128 image shown **1:1 in a portrait card** would put the Atlas at 68 px tall — close to today's
  84 × 84 display (71 px) — but the 44 × 44 tile would need a separate square artifact. That is two images
  per chassis, out of scope.

The preview PNG is only 3–13 KB; a `.mmux` already carries `unit.json` (~10 KB), `unit.png` (~4 KB) and, for
v2, a sprite atlas (~120 KB). If size becomes critical, optimize PNG encoding (palette/quantization, strip
metadata) or reduce sprite frames — not the aspect.

**Decision: retain 128 × 128** for the shared card/tile preview. If a portrait canvas is ever required for
layout, use **96 × 128**, not 64 × 128. A 64 × 128 portrait is only viable as a separate card-only artifact
with a square tile fallback.

---

## 4. What the requirements imply for the renderer

The driver has to do more than accept a second preset:

1. **Camera target.** `scene.add_camera` places the camera at `(0, 0, 3·cellWidth)` and only rotates it.
   Any pitch < 90° makes the view axis miss the model by `distance·tan(pitch)` (≈ 68 px at 10°, 1×),
   putting the mech off-frame. A preview camera must be **placed from a target and an orientation**, and
   the target must be shifted so the silhouette's lowest pixel sits on the baseline.
2. **Front is −Y.** The canonical model faces −Y after the Blender glTF import; the old spike convention
   (camera at +Y) is the rear. Animated MW5 models additionally need the assembly's `modelYawDegrees`
   (currently `-90` for AS7-D, while docs/README say `90` — fix that inconsistency).
3. **Light rig.** Preset lights are camera-parented with positive `z` (behind the camera). That is "above
   the scene" only for a top-down camera; for a front camera the rig ends up behind the model. The preview
   needs a world-space rig or a view-space rig whose lamps sit in front of the camera. This spike used a
   world-space 3-light rig (key front-left-above, fill front-right, rim behind).
4. **Framing/edge check.** `--fit` measures plan-view `x`/`y` extents and is wrong for a front view. The
   preview uses the fixed 128 canvas and must verify the **rendered silhouette** (or projected mesh
   vertices) does not touch the edge; fail the run with the chassis id and the offending margin. The 11.65
   px/m fit above is a real failure case (0 px top margin).
5. **Output mode.** `--preview` (or `output.preview`) writes one `unit-preview.png` instead of clip frames.
   No `--fit`, no atlas, no animation. Idle can be rendered as clip frame 1 when the model is animated.
6. **Preset schema.** The strict loader currently knows `camera`/`output`/`clips` only. Add **one** `views`
   mechanism shared with #1832:

   ```jsonc
   "views": {
     "board":   { "pitchDegrees": 90, "yawDegrees": 0 },
     "preview": { "pitchDegrees": 10, "yawDegrees": 0 },
     "prone":   { "pitchDegrees": 65, "yawDegrees": 180 }
   },
   "scale": {
     "mode": "tonnage-volume",
     "referenceChassis": "AS7",
     "referenceCoverage": 0.875,          // of the preview canvas height, after correction
     "minimumHeightCoverage": 0.65        // height floor; distinct from the board's area-based minimumCoverage
   },
   "clips": { "ProneIdle": { "view": "prone", ... } },
   "preview": { "view": "preview", "file": "unit-preview.png", "marginPx": 8 }
   ```

   The loader's schema needs `views`, `preview` and `scale.minimumHeightCoverage` added. #1832 needs
   per-clip views; #1831 needs one named view for a separate artefact. One schema, two users — do not grow
   `preview` and `views` as competing mechanisms.
7. **Luminance conversion.** `sheet.py` only *declares* `colorSpace: "luminance"`; it does not convert or
   assert. The preview path needs the same conversion the future C# pipeline owns:
   `L = 0.2126R + 0.7152G + 0.0722B; R = G = B = L`. The current board outline `#101418` is slightly blue
   (max channel delta 8–10 in the spike), so either make the preview outline neutral (`#141414`) or keep
   the pre-conversion assertion out of the preview path. Do not ship a bluish preview.
8. **Report.** `render-report.json` gains
   `preview: { view, pitchDegrees, yawDegrees, pxPerMetre, canvas, marginPx, volumeCorrection, appliedFloor }`.

---

## 5. Packaging, runtime and UI requirements

| Layer | Requirement |
|---|---|
| `MmuxPackager` | Copy an optional `unit-preview.png` **and its `unit-preview.json`** next to `unit.png`. Absent = valid package. **Do not bump the manifest version**: the entries are optional in both `1.0` and `2.0`, so packages without a preview stay byte-compatible and older readers ignore the extra zip entries |
| `MmuxUnitPackageReader` | Expose `UnitPackage.PreviewImage` (nullable) and `UnitPackage.PreviewMetadata` (nullable: canvas, `unitRect`, pivot). Missing or corrupt preview/metadata returns `null` plus a diagnostic (same pattern as `SpriteError`); it must never throw and never drop the unit. Read it for any supported manifest major version |
| `UnitCachingService` | Cache the preview bytes and metadata alongside `unit.png`; an absent preview is not an error and is not retried on every request |
| `IImageService` | Add an optional `GetUnitPreview(model)` returning the image **and** its metadata (a small `UnitPreview` DTO), with a default implementation returning `null`; override in `CachedImageService`/`HybridImageService`. The UI owns the `preview ?? unit.png` fallback so the fallback exists in one place |
| `UnitItemControl` | Use the preview at 84 × 84 when present (default `Stretch` is `Uniform`). Use `unitRect` to place the tint/pattern overlay over the unit instead of the full canvas. **Skip the `RotateTransform Angle="180"`** for previews — it exists to orient the top-down token, not a portrait |
| `SquadUnitTile` | Use the preview at 44 × 44 when present. Use `unitRect` for the tint overlay. **Skip the `facing × 60°` rotation** for previews; `ShowFacing` must not rotate a portrait. Keep the 0.7-opacity owner tint on both controls — verified visually on the 128/84/44 sheets |
| Record sheet | Use the preview wherever the existing `unit.png` is shown as identification; the board token stays `unit.png` |
| Licensing | A preview is *more* identifiable than a grey top-down token. It obeys the same rule: tooling ships, output never does; `not-redistributable` refuses it; never promote to `data/` |

### 5.1 Preview metadata: `unit-preview.json`

The PNG alone is not enough for the tint overlay. A 128 × 128 preview contains the unit only inside its
silhouette bbox (e.g. the Atlas occupies 82 × 108 px with 23 px side margins), so the overlay needs the
**actual unit rect**, not the canvas.

```jsonc
{
  "version": 1,
  "image": "unit-preview.png",
  "canvas": { "width": 128, "height": 128 },
  "unitRect": { "x": 23, "y": 12, "width": 82, "height": 108 },  // alpha bbox, canvas px
  "pivot": { "x": 64, "y": 120 },                                 // footprint centre on the ground line
  "colorSpace": "luminance",
  "view": { "name": "preview", "pitchDegrees": 10, "yawDegrees": 0 },
  "scale": { "pxPerMetre": 5.39, "correctionScale": 1.0 }
}
```

Measured examples:

| Chassis | unitRect (x, y, w, h) | pivot | canvas |
|---|---|---|---|
| AS7-D (Atlas) | 23, 12, 82, 108 | 64, 120 | 128 × 128 |
| LCT-1E (Locust, volume-corrected) | 42, 36, 44, 83 | 64, 119 | 128 × 128 |

Rules:

- `unitRect` is the **alpha bounding box after downsampling** (including the 1 px outline and antenna), not
  a hand-authored crop. The pipeline measures it and writes it; it must be inside the canvas and must not
  touch the edge (already the render assertion).
- `pivot` is the footprint centre on the ground line: `x = canvas.width / 2`, `y = unitRect.y + unitRect.height`.
  It is the anchor for a future tint pattern/decal and for any rotation.
- The UI maps the image into its slot with `Uniform`, then scales the rect by the same factor:
  `s = displayWidth / canvas.width`; `unitDisplayRect = imageOffset + unitRect × s`. The tint/pattern
  overlay is clipped to `unitDisplayRect` (or its alpha mask), not the whole canvas. This also keeps a
  future non-square canvas aligned.
- The existing **solid owner tint** already works from the image alpha mask; the rect makes the anchor
  explicit and is required for a tiled/patterned tint, which cannot infer the unit bounds from transparency
  alone once the pattern ignores alpha.
- Missing or corrupt `unit-preview.json`: use the PNG with `unitRect = full canvas` and `pivot = canvas
  centre/bottom`, log a diagnostic, never drop the unit. Missing PNG: no preview, fall back to `unit.png`.
- `render-report.json` records the same `preview.unitRect` and `preview.pivot` so a sprite directory is
  self-describing.

---

## 6. Required work (revised from the issue)

1. **Preset** — `tools/blender/unit-preview.json`: front 10°/0°, 128², `channels: "luminance"`,
   `board-look`, neutral outline, transparent film, `Standard`, preview block, `scale.mode`.
2. **Driver** — target-based camera framing (front = −Y), world-space/view-space preview light rig,
   `--preview` output, silhouette edge assertion, preview block in the report. Keep `--fit` off the preview
   path.
3. **Pipeline** — compute the product `pxPerMetre` from the tallest chassis at the chosen view, then
   multiply the **shared** `volume.correctionScale` from `model.json`; apply the 65 % floor; luminance
   conversion; downsample; measure the alpha bbox and write `unit-preview.json` (`unitRect`, `pivot`,
   canvas, view, scale); write `preview` metadata. The driver still receives only `--scale`.
4. **Volume correction (accepted, shared)** — the import-time voxel measurement
   (`volume.modelCubicMeters` + `volume.correctionScale`) is the single source of truth for every render
   product. Implement it once in stage 1 (`assemble`/normalizer, where the canonical mesh and canonical
   loadout are final) and store it in `model.json`; the board renderer and the preview only supply their own
   `pxPerMetre`. Until it lands, the preview falls back to `uniform` and marks `volumeCorrection: null`; it
   must not measure volume itself.
5. **Board-sprite impact** — the Atlas stays at its current size (it is the reference); other chassis are
   adjusted relative to it, which changes their existing board sprites. Re-render the pilot board sprites
   and review the lineup together with the previews; this is intended, not a preview-only change.
6. **Packaging** — optional `unit-preview.png` + `unit-preview.json`, no manifest bump.
7. **Runtime** — reader/cache/image-service expose `UnitPackage.PreviewImage` and `PreviewMetadata` (canvas,
   `unitRect`, pivot) as in §5.1.
8. **UI** — both controls bind the preview, skip rotation, keep tint, use `unitRect` to position the
   tint/pattern overlay, fall back to `unit.png`.
9. **Docs** — `asset-pipeline.md` §2.5/§4.2/§4.3/§4.4/§5, `blender-render-driver.md` §3/§4,
   `tools/blender/README.md`, `mw5-model-extraction.md` §4.5, `animated-mech-integration.md` §2, and the
   `modelYawDegrees` sign inconsistency. Coordinate the `views` schema with #1832.

Cost: one frame per chassis at 128² (512² render), ~1–2 s each; 76 chassis is a few minutes. Volume
measurement is an import-time cost (one voxel pass per chassis), not a per-preview cost.

---

## 7. Updated acceptance criteria

- [ ] `tools/blender/unit-preview.json` passes the strict validator and is the only place the preview look
      lives.
- [ ] `render --preview` writes a 128 × 128 `unit-preview.png` at front 10° pitch / 0° yaw for every
      chassis; front is −Y; feet on a shared baseline, footprint centre horizontally centred.
- [ ] `scale.mode` supports `tonnage-volume` and `uniform`, with `--fit` as the per-chassis escape hatch;
      the tonnage-volume path consumes the **import-time** `volume.correctionScale` and only computes the
      product `pxPerMetre`; the 65 % floor is a guard.
- [ ] `volume.correctionScale` is computed once at import relative to the Atlas (Atlas = `1.0`, never
      rescaled), stored in `model.json`, and consumed unchanged by the board sprite and the preview;
      changing it affects both at the next render without changing either product's calibration.
- [ ] Any hand-calibrated scale is a single global multiplier applied equally to every chassis; it changes
      the overall size but never the Atlas:others ratio.
- [ ] A silhouette touching the canvas edge fails the run with the chassis id and margin; the check uses
      the rendered silhouette/projected vertices, not bounding-box corners.
- [ ] `render-report.json` records the preview view, camera, effective `pxPerMetre`, volume correction,
      applied floor, canvas, `unitRect` and `pivot`.
- [ ] `unit-preview.json` is written next to the PNG with `canvas`, `unitRect`, `pivot`, `colorSpace`, view
      and scale; `unitRect` is the measured alpha bbox, lies inside the canvas, and does not touch the edge.
- [ ] The preview is luminance-only (`R = G = B`, alpha = silhouette) and the UI still applies the owner
      tint at 0.7 opacity; no albedo/colour path exists.
- [ ] The UI maps `unitRect` through the same `Uniform` scale as the image at 84 × 84 and 44 × 44, and the
      tint/pattern overlay is anchored to that rect rather than the full canvas.
- [ ] `pack` copies `unit-preview.png` and `unit-preview.json` when present; packages without them remain
      byte-compatible and their manifest version is unchanged.
- [ ] Missing/corrupt preview returns `null`, logs a diagnostic, and the UI falls back to `unit.png`; a
      missing/corrupt `unit-preview.json` keeps the PNG with a full-canvas rect and logs a diagnostic; a unit
      is never dropped.
- [ ] Preview images are not rotated by the card's 180° transform or the squad tile's facing rotation.
- [ ] AS7-D and a Locust are rendered and eyeballed at 84 × 84 and 44 × 44 (local asset is `LCT-1E`; the
      final pilot set still includes COM-2D and LCT-1V): each is recognisably a mech, Atlas > Locust still
      reads, and the 65 % floor keeps the Locust readable.
- [ ] Volume measurement is deterministic, method-pinned (voxel grid + fill policy), and recorded; the
      measured volume ratio (~0.05–0.07) is documented, not mistaken for the tonnage ratio (0.20). The
      correction is stored in the manifest, not recomputed by a render product.
- [ ] No MW5-derived file is committed; the licensing gate refuses previews exactly as it refuses sprites.

---

## 8. Risks and open questions

| Risk | Mitigation |
|---|---|
| 44 px is too small for a light mech under strict uniform scale | volume correction lands at ~28 px; the 65 % floor guarantees it. Review at 44 px |
| Volume method changes the correction by ~20 % | pin the method (voxel + hole-fill policy), store it in `model.json`, record the value in the report |
| Preview metadata drifts from the PNG (hand-edited, re-cropped) | generate `unitRect` from the alpha channel in the pipeline, validate it is inside the canvas and non-zero, record it in `render-report.json` |
| A render product re-measures volume and drifts from the board sprite | one `correctionScale` in the manifest; render products only apply it. A unit test asserts both products read the same manifest field |
| Shared correction changes non-reference board sprites | expected; the Atlas stays put. Re-render the pilot board sprites and review the lineup with the previews before rolling out |
| Volume correction compresses height differences (Atlas/Locust height ≈ 71–78 %) | expected: it normalises 3D volume, not silhouette height; the board sprite remains the size cue |
| Camera work expands beyond "a second preset" | treat target-based framing + light rig as explicit driver work; estimate it before the UI work |
| Front/back confusion recurs | front is −Y; add a rendered front/back probe to the driver's review artifacts |
| Blue outline trips the luminance assertion | neutral outline in the preview preset, or convert before asserting |
| `views` and `preview` schemas diverge from #1832 | settle one `views` block first; both issues consume it |
| Animated source yaw sign confusion | use `modelYawDegrees` from the assembly report; fix the docs to match |
| 128 px is soft on 2× HiDPI (84 logical = 168 device px) | accepted for the first path; file size is a constraint. Regenerate a higher-res set later if HiDPI needs it |
| 64×128 or 128×64 proposed to save bytes | 64×128 global saves ~45 % but shrinks the card mech 71→45 px and the tile 37→23 px; per-mech fit makes the Locust taller than the Atlas. Keep 128×128 (96×128 if a portrait canvas is needed) |
| Uniform or fitted fallback weakens "relative size reads" | tonnage-volume is the default; `uniform`/`--fit` are comparison or unavailable-volume fallbacks, not shipping policies |

Open question for the product owner:

1. Does the record sheet use the preview (recommended)? The board token keeps `unit.png` (yes).
