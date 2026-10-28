# Animated Mech Integration (mmux v2 + distance-driven movement playback)

Prototype integration for [#1659](https://github.com/anton-makarevich/MakaMek/issues/1659): the AS7-D ships
Idle (static), Walk and Run art, the package format grows an **optional** sprite atlas, and the board
replays a received `MoveUnitCommand` over a fixed five seconds with frames driven by travelled distance.

This note records what was actually run, the calibration values, and the known limitations. Rendered
MW5 assets are **not committed**: the pipeline output stays in gitignored `artifacts/`, and the game
loads the v2 package from a user folder through the existing local-folder resource provider.

## 1. Asset pipeline

### 1.1 Extractor diagnostics

`AnimationClipReader` now measures the root trajectory **before** `StripRootDrift` and keeps the old
behaviour intact (poses, sampling, `rootMotionMeters`, stripping and dropping the loop's closing key).
New per-clip fields (also in `clips --probe` and the assembly JSON):

| Field | Meaning |
|---|---|
| `rootEndpointDisplacementMeters` | Horizontal displacement from the first to the last root key — the authored stride |
| `rootDisplacementXMeters` / `rootDisplacementZMeters` | The displacement vector in glTF space |
| `rootProgressMonotonic` | True when no key moves backwards against the forward direction |
| `rootMaxLateralDeviationMeters` | Largest perpendicular offset from the first-to-last line |
| `rootPathLengthMeters` | Accumulated horizontal root path length |

Warnings are emitted for non-monotonic progress and for an endpoint displacement that differs from the
maximum excursion by more than 10% (a hint that the clip includes lateral drift).

Measured for AS7-D:

| Clip | Root motion (max excursion) | Endpoint displacement | Monotonic | Lateral | Path length |
|---|---|---|---|---|---|
| Walk | 8.607 m | 8.607 m | yes | 0 m | 8.607 m |
| Run | 18.450 m | 18.450 m | yes | 0 m | 18.450 m |

Because endpoint displacement equals the old maximum excursion for both clips, the root measurement has no
sideways drift; the sampled foot correction (§1.4) is applied on top of it.

### 1.2 Sheet metadata

`tools/blender/sheet.py` writes `distancePerCycle` per clip. The default is the sampled foot-contact
stride from the assembly's `footGait.correctedStrideMeters` × the report's `appliedScale`; clips
without foot contacts (and older assemblies) fall back to the root endpoint, then `rootMotionMeters`.
A repeated `--distance-per-cycle CLIP=PIXELS` override is available for visual calibration. The sheet
also records provenance in `render`: preset, `appliedScale`, `supersample` and `modelYawDegrees`, and
the per-clip `footGait` block. Packing, row order, padding and pivot are unchanged; the metadata schema
moved to version 2 only for the added optional fields.

The foot probe uses the shipped frame count (`clips --probe --shipped-frames 10`). The extractor reads
the `LeftFoot_Down` / `RightFoot_Down` notify windows, samples `Foot0_Left` / `Foot0_Right` only at
those shipped frames, and reports the root travel and foot slip per stance plus the corrected stride.

### 1.3 Commands used

```bash
# 1. Probe the clips and their new stride diagnostics
MW5Extractor.Cli clips AS7-D --probe --shipped-frames 10 --paks "<paks>" \
    --oodle "<oo2core>" --zlib "<zlib-ng>" -o artifacts/mw5/clips/AS7-D.json

# 2. Assemble the animated model with the three clips
MW5Extractor.Cli assemble AS7-D --clips Idle Walk Run --paks "<paks>" \
    --oodle "<oo2core>" --zlib "<zlib-ng>" -o artifacts/mw5/assemble/AS7-D-v2

# 3. Render Walk and Run together at one scale/yaw and pack them into one atlas
blender -b --factory-startup -noaudio --python-exit-code 1 -P tools/blender/render.py -- \
    --model artifacts/mw5/assemble/AS7-D-v2/AS7-D.animated.glb \
    --preset tools/blender/board-topdown.json \
    --scale 5.098 --model-yaw 90 --fit --supersample 4 --clips Walk,Run \
    --out artifacts/mw5/anim/AS7-D-v2 --report artifacts/mw5/anim/AS7-D-v2/report.json

python tools/blender/sheet.py \
    --frames artifacts/mw5/anim/AS7-D-v2/frames \
    --report artifacts/mw5/anim/AS7-D-v2/report.json \
    --assembly artifacts/mw5/assemble/AS7-D-v2/AS7-D.animated.assembly.json \
    --preset tools/blender/board-topdown.json \
    --out artifacts/mw5/anim/AS7-D-v2/sheet \
    --frames-out artifacts/mw5/anim/AS7-D-v2/frames-1x \
    --name AS7-D --downsample auto

# 4. Static Idle from the same animated GLB and settings
blender -b --factory-startup -noaudio --python-exit-code 1 -P tools/blender/render.py -- \
    --model artifacts/mw5/assemble/AS7-D-v2/AS7-D.animated.glb \
    --preset tools/blender/board-topdown.json \
    --scale 5.098 --model-yaw 90 --fit --supersample 4 --clips Idle --frame 1 \
    --out artifacts/mw5/anim/AS7-D-v2-idle --report artifacts/mw5/anim/AS7-D-v2-idle/report.json
# Downsample the 4x Idle frame by 4 (BOX) -> unit.png
```

### 1.4 Produced values

| Value | Result |
|---|---|
| Applied scale / yaw / supersample | `5.098`, `90°`, `4` |
| Walk source frames | `0, 6, …, 54` (10 frames at 12 fps), unchanged from #1653 |
| Run source frames | `0, 3, …, 27` (10 frames at 16 fps) |
| Atlas cell | `91 × 94` px (1×); Walk row 0, Run row 1 |
| `distancePerCycle` | Walk `43.102` px, Run `97.288` px |
| Static Idle canvas | `84 × 44` px (1×) |

**Foot-contact calibration.** The default is now measured from the shipped frames, not the root endpoint
alone. `clips --probe` reads the clip's `LeftFoot_Down` / `RightFoot_Down` notify windows and samples the
`Foot0_Left` / `Foot0_Right` world positions only at the 10 frames the atlas ships. The corrected stride
is `rootEndpoint × (rootTravel − footSlip) / rootTravel` over the sampled stance windows:

| Clip | Sampled stance contacts | Corrected stride | Pixels at `5.098 px/m` |
|---|---|---|---|
| Walk | Right `3.443 m` root / `0.107 m` slip (5 frames); Left `2.582 m` / `0.006 m` (4 frames) | `8.4546 m` | `43.102` |
| Run | Right `1.845 m` / `0.076 m` (2 frames); Left `1.845 m` / `0.051 m` (2 frames) | `19.0835 m` | `97.288` |

The root-only values (`43.878 / 94.058`) remain the fallback for clips without foot contacts and older
assemblies. The 5 s movement slot is unchanged: distance still represents speed, so longer moves cycle
the legs faster. The sampled probe only changes which stride the frame phase uses.

**Orientation correction.** The first render used `--model-yaw -90` (the value the assembly report
reported at the time) and produced sprites with the mech's front at the bottom, opposite to the
existing board art where facing 0 points up. The frames were re-rendered with `--model-yaw 90`; the
assembler now reports `modelYawDegrees = 90` and the sheet records that value. `--fit`, the cell size,
the stride and the frame schedule are unchanged by the 180° flip.

## 2. mmux v2

v2 is **additive**: `manifest.json`, `unit.json` and the static `unit.png` are still mandatory, and the
manifest `version` selects the behaviour.

| Manifest | Reader behaviour |
|---|---|
| missing or `1.x` | v1 exactly as before; no sprite payload |
| `2.x` | v1 content plus optional `unit-sprites.png` / `unit-sprites.json` |
| other major | throws a clear unsupported-version error; the cache logs it and skips the unit |

A broken or incomplete sprite pair never drops the unit: the reader reports the problem through
`UnitPackage.SpriteError`, the cache logs it, and the static image is used.

```bash
dotnet run --project src/MakaMek.Tools/MmuxPackager -- \
    -d <folder with unit.json> \
    -i <folder with unit.png> \
    -s <folder containing <Model>/unit-sprites.png + unit-sprites.json> \
    -o <local package folder outside version control>
```

The package output is deliberately kept out of git because MW5-derived renders are not redistributable
(see [mw5-model-extraction.md](mw5-model-extraction.md) §5). A filesystem provider resolves units under
`<root>/units/mechs`, so stage the package as:

```
<local root>/units/mechs/AS7-D.mmux
```

Then add a **Filesystem** provider for `Units` pointing at `<local root>`; the v2 package loads like any
other content. The prototype build used `artifacts/mw5/packages/v2-root` (gitignored).

## 3. Runtime playback

- `BattleMapViewModel.UnitMoveCommands` publishes received `MoveUnitCommand`s on the UI dispatcher,
  but only while `FeatureFlags.IsUnitAnimationEnabled` is on (off by default until v2 ships, so units
  keep moving instantly); `UnitPlaybackInterruptions` cancels playback on displacement, skid, fall,
  stand-up, game change, detach and disposal.
- `MovementPlaybackTimeline` (Presentation, pure and unit-tested) classifies path segments into
  translations, turns and redundant segments, then allocates the fixed 5 s: `180 ms` per 60° turn
  (capped at 40% of the duration), the rest proportional to distance. Turn-only paths spread the whole
  duration over their turns. `Sample(progress)` returns position, facing, signed travelled distance and
  the clip.
- `UnitControl` keeps presentation-only position/facing while playing, restores the authoritative model
  state on completion/cancellation, and never writes display state back to the unit. Sprite units are
  drawn 1:1 at their atlas cell; v1 units keep the historical 84% box. The 32 ms state poll never starts
  or restarts playback.
- Frame phase is `floor(±distance / distancePerCycle × frameCount) mod frameCount`. Reversed segments
  use negative distance, which plays the cycle backwards; phase is continuous across segments of one
  command. Frames are pre-cropped `CroppedBitmap`s; the tint mask reads the same sub-rectangle from the
  atlas (a `CroppedBitmap` is not an `IImageBrushSource`).
- Presto `0.1.0-preview.1` drives one linear `5 s` timer per command (`Presto.CreateTimer` inside a
  control-owned `ControlScope`). Under `Presto.Defaults.ReducedMotion` the playback snaps to the end
  instead of animating. `RenderTransform` stays owned by the control and is never shared with Presto.
  The pinned package was verified to expose `TimerParams.OnUpdate`, `Timer.Progress`,
  `Scope.Execute<T>` and `Presto.Defaults.ReducedMotion`; the plan's "custom property or OnUpdate" choice
  resolved to `OnUpdate`, which needs no custom Avalonia property.
- `IsCompleted = false` chunks and later continuations are queued as separate operations, each starting
  from its own `segments[0].From`.

## 4. Known limitations

- **Run loop seam.** The source Run clip closes with a `7.9°` pose mismatch, so its loop visibly jumps;
  this is recorded in the clip warnings and not corrected by the render.
- **No Jump/Prone clips.** `Jump` and `Prone` translate with the static image; missing Walk/Run clips on
  a package behave the same way.
- **No idle animation.** Idle is a single static `unit.png`; the atlas only carries Walk and Run.
- **Canvas differs from the idle art.** `--fit` sizes Walk/Run (`91 × 94`) and Idle (`84 × 44`)
  separately. Both are drawn at the same scale and centred, so the mech does not change size, but the
  idle and moving canvases are not pixel-identical.
- **One fixed duration.** Every command takes 5 s by design, so very short moves crawl and long moves
  sprint; this mirrors the "every turn is the same time" board-game abstraction rather than unit speed.
- **No regressions tested for other packages.** Only AS7-D was built as v2; every committed `.mmux`
  remains v1 and is covered by the existing v1 reader tests.
