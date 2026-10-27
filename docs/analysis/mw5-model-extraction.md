# MW5 Model Extraction & 2D Sprite Re-Render Pipeline

Research into extracting 3D mech models from *MechWarrior 5: Mercenaries* and turning them into the 2D sprites (and eventual spritesheet animations) that MakaMek's board renders today.

Date: 2026-10-25 · Scope: research only, no code changes.

> The pipeline these techniques feed into is designed in [../architecture/asset-pipeline.md](../architecture/asset-pipeline.md) (stage 1 = canonical model import, stage 2 = sprite rendering). This document covers the extraction fact base and the sprite-format details that design builds on.

---

## 1. Executive summary

1. **Extraction is a solved, well-trodden problem.** MW5 is Unreal Engine 4.26; every **base-game** asset lives in `MW5Mercs-WindowsNoEditor.pak`, and DLC/mods ship as additional `.pak` files that must be loaded too (see §2.1). Three working routes exist: **FModel** (browse/preview/export), **UE Viewer/umodel** (viewer + batch export), and the official **MW5 Mod Editor** (FBX export with real materials and skeletons).
2. **There are 199 unit variants in `data/units/mechs` but only 76 distinct chassis.** MW5 meshes are per-chassis, so the render workload is 76 model sets, not 199 (see §2.3).
3. **Current unit art is a single 84×72 top-down silhouette** (`unit.png` inside the `.mmux` zip) drawn at 0.84 × the 100×86.6 hex. A re-render pipeline only needs to replace one PNG per unit at first — no format change is required to prove the idea.
4. **Animation needs a format change** (`.mmux` v2: sprite atlas + metadata), but the runtime already has a sprite engine: `MonacoLabs.Presto.Avalonia` exposes `SpriteSheet` / `ImageSequence` / `PlayFrames` (see [presto-animation-capabilities.md](presto-animation-capabilities.md) §3.6).
5. **Walking/idle/run exist in MW5; prone and stand-up do not.** MW5 has no knockdown mechanic, so no fall/getup animation was ever shipped. Those two clips must be authored in Blender (cheap for a top-down camera) or approximated from the shutdown/collapse and jump-jet clips.
6. **Legal:** renders of MW5 models are derivative works of PGI/Microsoft IP. They cannot be committed to this repo or shipped as downloadable content. The *tooling* can be; the *output* must stay a user-generated local asset pack (which the existing `LocalFolderResourceStreamProvider` already supports).

---

## 2. Fact base

### 2.1 Game and archives

| Item | Value |
|---|---|
| Engine | Unreal Engine **4.26** (older builds 4.23) |
| Install root | `…\MechWarrior 5 Mercenaries\MW5Mercs\Content\Paks\` |
| Main archive | `MW5Mercs-WindowsNoEditor.pak` (plus DLC/mods as additional paks) |
| Asset types | Cooked `.uasset` (skeletal meshes, textures, material instances, data assets) |
| AES | Base-game paks are not encrypted as a rule; mod paks occasionally are. If a tool reports the archive as encrypted you need the game's AES key. |
| UE5 mappings (`.usmap`) | **Not needed** — 4.26 assets are versioned |

### 2.2 Asset layout inside the game

- `MW5Mercs/Content/Objects/Mechs/<Chassis>/…` — one **skeletal mesh** per chassis (rig + hardpoint sockets as bones), plus animation assets, material instances, textures and `AnimBlueprint`s.
- Variant data lives in data assets (`…Data`, `.hps` hardpoint sets, loadouts); **the mesh is shared across variants of a chassis**.
- Weapons/arms are **separate meshes attached to sockets** — an extracted mech body may be missing arms or weapons.
- Materials are custom UE4 material graphs (paint-scheme composition, camo masks), not plain PBR — expect flat/grey previews from `umodel`/FModel.

#### 2.2.1 Verified against the pak (2026-10-27, [MW5Extractor](../../src/MakaMek.Tools/MW5Extractor/README.md))

The statements above are directionally right but the real tree is messier; measured on a full base + DLC install (285,997 virtual files):

| Reality | Consequence |
|---|---|
| **104** mech folders, not 76: Clan chassis live one level deeper, `Objects/Mechs/CLANS/<Chassis>/`, and DLC content is nested inside the single base pak (`MW5Mercs/Content/DLC7/...`) | Chassis identity must be resolved from the chassis' own child folders, not from the segment after `Mechs` |
| 102 chassis are monolithic (`Model/Body/<Chassis>_SKM`); **Atlas II ("modular") has no body mesh** — it is a set of part meshes plus `Model/Body/AS7II` | "One skeletal mesh per chassis" is false for newer DLC chassis |
| Body meshes carry **no sockets**; the shared `Mech_SKL` carries 47 (`Torso_Weapon`, `Forearm_Left_Weapon`, `Weapon_Melee_*`, `WeldTarget_*`) | Mount transforms exist, but at torso/arm granularity — not per hardpoint |
| 101 cockpit meshes, 90 cockpit glass meshes; 14 chassis ship no glass | Glass is per chassis and sometimes absent |
| 104 `<Chassis>_HPS` hardpoint sets, 686 variant (`_MDA`/`_Loadout`/`_UnitCard`) assets, 12,541 weapon meshes, 10,978 animation assets | Everything the assembly step needs is present |
| `MWMechHardpointAsset`/`MWMechDataAsset`/`MWMechLoadoutAsset` are custom classes CUE4Parse leaves as `UObject`, **but `JsonPropertiesExporter` dumps them fully**: `Atlas_HPS.json` lists `HardpointSlotID` (`Torso_Left_MH1_lrm10`), `MechSkeletonSocketID` (`Torso_Weapon`), `MechPart`, and `HardpointVisuals` (weapon tag → weapon Blueprint); `AS7-D_Loadout.json` lists `InstalledWeapons[]` as `HardpointSlotID` → weapon data asset | Hardpoint topology, mount socket and the equipment→asset mapping are machine-readable headlessly — no hand-written deserialiser, no hand-curated weapon table |

The MW5 layout is therefore "body + cockpit + glass + weapon meshes + data assets", with **two body layouts** to support. Filenames remain the cheapest discovery index (`Weapon_Mech_AS7_Torso_Right_BH1_AC20_SKM` encodes chassis, location, hardpoint and weapon), and the `<Chassis>_HPS`/`_MDA`/`_Loadout` JSON is the authoritative assembly data.

### 2.3 Chassis count (measured from this repo)

```
data/units/mechs/*.mmux      → 199 variants
distinct chassis prefixes    →  76:  ARC AS7 ASN AWS BJ BL BLR BMB BNC CDA CGR CHP
CLNT CN9 COM CP CPLT CRB CRD CRK CTF DRG DV ENF FLC FLE FLS FS9 GHR GLT GRF HBK HER
HGN HNT HSR JM6 JR7 JVN KTO LCT LGB LNC25 MAD MCY MON ON1 OSR OTL OTT PNT PXH QKD RFL
RVN SDR SHD STG STK STN TBT TDR THE THG UM VL VLK VND VTR WHM WLF WSP WTH WVE WVR ZEU
```

A chassis map (`AS7 → Atlas`, `CPLT → Catapult`, …) must be maintained as a small JSON file in the tooling; MW5 folder names are chassis names, MegaMek/MakaMek codes are `CHASSIS-VARIANT`.

---

## 3. Extraction routes

### 3.1 Route A — FModel (recommended for exploration and bulk export)

Open-source UE datamining frontend. Reads `.pak` directly, renders static/skeletal meshes with animations in-app, exports `.psk/.pskx`, `.gltf`, PNG/TGA textures and raw `.uasset`/JSON.

Setup:

1. `Directory → Selector → Add Undetected Game` → point at `…\MW5Mercs\Content\Paks`, pick **UE 4.26**, press **+**.
2. Settings → **Output Directory**, **Texture Platform = Desktop**.
3. Loading mode **All** → Load (merges base + DLC + installed mods).
4. Browse `MW5Mercs/Objects/Mechs/…`, double-click a mesh to preview, right-click → **Save Model** (`.psk`) / **Save Texture**.

Strengths: fastest to a preview; per-asset graph navigation (jump from mesh → material → texture); exports both PSK and glTF.
Weaknesses: no batch/batch-CLI; custom materials render grey.

### 3.2 Route B — UE Viewer (umodel) (recommended for animation inspection and batch)

Gildor's UE Viewer is confirmed working for MW5 ("meshes, textures and animations", build 1590). It is a real-time viewer with animation playback and a scriptable CLI exporter.

```
umodel.exe -path="…\MechWarrior 5 Mercenaries" -export -out=out MW5Mercs-WindowsNoEditor.pak
```

Keys worth knowing: `PgUp/PgDn` cycle objects, `[`/`]` cycle animations, `Space` play, `Ctrl+A` animation sets, `Ctrl+X` export current object, `Ctrl+S` screenshot.

Caveat: custom materials render grey; spiritovod's custom "materials" build (linked from the MW5 forum topic) improves preview and fixes some static meshes in newer game versions.

### 3.3 Route C — MW5 Mod Editor (highest fidelity)

The official Mod Editor is a complete UE 4.26 editor, free in the Epic launcher for game owners. In it: select asset → **Asset Actions → Export** → **FBX**.

This is the only route that preserves:

- real materials and texture references,
- the real skeleton (hardpoint sockets are bones — useful later),
- LODs, collision, correct scale/pivot.

Skeletal-mesh export options that work: FBX 2013/2014 binary, `Vertex Color` on, `Level of Detail` off, `Export Morph Targets` on, `Map Skeletal Motion to Root` on.

Cost: a multi-GB editor install and a slow first load.

### 3.4 Automation

If the pipeline needs to run for 76 chassis repeatedly, write it as a scripted tool rather than clicking:

| Layer | Library |
|---|---|
| Pak read/decrypt | `repak` (CLI) or **CUE4Parse** (the library FModel is built on) |
| Asset parse | CUE4Parse (meshes, textures) / `UAssetAPI` (property-level) |
| Mesh → file | CUE4Parse exporters (PSK/glTF), or shell out to `umodel -export` |
| Render | `blender -b -P tools/blender/render.py` — this **is** the pipeline's renderer (see `../architecture/blender-render-driver.md`); the same install also covers PSK/PSA round-tripping |

A C# tool on CUE4Parse fits this repo's stack (net10.0, no Python build step needed for extraction) and can emit the `.mmux` packages directly.

**This now exists:** [`src/MakaMek.Tools/MW5Extractor`](../../src/MakaMek.Tools/MW5Extractor/README.md) mounts the paks, enumerates and classifies all 104 chassis into `discovery-report.json`, reads meshes/skeletons/sockets, exports glTF/PSK/JSON, and dumps the custom hardpoint/loadout data assets — headless, read-only, no GUI. It is the intended `unreal` adapter of `MakaMek.Pipeline.Import`.

### 3.5 Extraction gotchas

| Gotcha | Handling |
|---|---|
| LODs | `Save Model` writes `_LOD0.._LODn` — use LOD0 only |
| Duplicate material slots | merge duplicates in Blender, or shading looks wrong |
| PSK has no vertex normals | work from `.pskx`/glTF when normals matter |
| Custom materials | expect grey; rebuild base colour/normal/mask manually |
| Paint schemes | MW5 composes camo from mask textures — extracted albedo is a plain base colour |
| Scale | PSK carries no unit system; UE is cm/Z-up, models import ~100× too big |
| Missing limbs | weapons/arms are separate meshes on sockets; decide per-chassis what to include |
| Mod paks | load **All** in FModel or you may pick a modded override |

---

## 4. Re-rendering the 2D assets

### 4.1 What exists today

| Thing | Current state |
|---|---|
| Package | `.mmux` = zip with `unit.json`, `unit.png`, `manifest.json` |
| Image | single 84×72 ARGB PNG, transparent, **top-down silhouette**, ~1:1 to logical pixels |
| Drawing | `UnitControl` → `Image` at `0.84 × Width/Height` of a **100 × 86.6** hex, `Stretch = Fill`, rotated by torso facing |
| Loading | `IImageService.GetImage("units/mechs", model)` → `CachedImageService` → `UnitCachingService` (`ImageCache`) |
| Animation runtime | `MonacoLabs.Presto.Avalonia` `SpriteSheet` / `ImageSequence` / `PlayFrames` (pinned preview dependency) |
| Unit states available | `UnitStatus.Prone`, `MovementType` (Walk/Run/Jump/Prone), `IsOutOfCommission`, shutdown/heat, `Mech.IsProne` |

So: **P0 changes nothing but the PNG.** Animation later needs a v2 package + a state→clip resolver.

### 4.2 Animation inventory

| Clip | Source in MW5 | Notes |
|---|---|---|
| `Idle` | ✅ chassis idle / AnimBlueprint idle | subtle torso sway + weight shift |
| `Walk` | ✅ locomotion | loop; leg cycle reads well even top-down |
| `Run` | ✅ locomotion | longer stride, faster |
| `Jump` | ⚠️ only chassis with jump jets | useful for `MovementType.Jump` |
| `Shutdown` / `PowerUp` | ✅ shutdown/power-up | good for `UnitStatus.Shutdown` |
| `Destroyed` | ✅ destruction/collapse | good for `IsOutOfCommission` |
| `ProneEnter` | ❌ **does not exist** | MW5 has no knockdown mechanic |
| `ProneIdle` | ❌ **does not exist** | author |
| `StandUp` | ❌ **does not exist** | author |

MW5 never shipped fall/getup animations (knockdown was a MWO feature, removed early and never per-mech; the MercTech 1.0 mod had to *author* four new clips to add knockdown). Treat prone/standup as **new animation work**, not extraction.

### 4.3 Authoring prone/stand-up for a top-down camera

Because the board is top-down, lying down is a silhouette problem, not a rig problem — cheat aggressively:

1. **ProneEnter** (4–6 frames): rotate the root/hips 80–90° about the mech's forward axis while dropping the pelvis; splay arms. At 256 px and 12 fps this reads as "falling over".
2. **ProneIdle** (2–4 frames): settled pose, tiny breathing offset.
3. **StandUp** (5–7 frames): reverse with a push-up beat — arms plant, torso rises, legs gather under.
4. Reuse one authored set across chassis via retargeting (skeletons are per-chassis but humanoid-ish; MercTech reports cross-chassis animation reuse works after rig surgery). Fallback: 3–4 body-plan variants (biped, reverse-joint, quadruped) instead of 76 bespoke sets.

### 4.4 Camera and lighting spec

The current art is top-down and the `UnitControl` rotates sprites for facing, so the camera and lights must be **facing-independent**:

| Parameter | Recommendation |
|---|---|
| Projection | Orthographic, `ortho_scale` fixed to the hex footprint (100 u) — every mech shares one scale |
| Pitch | **90° (decided)** — matches the current art, so sprite replacement never changes how the board reads. The tilted alternative was rejected; see `../architecture/asset-pipeline.md` §4.2 for the consequences (facing is the only depth cue; prone needs a marker) |
| Yaw | 0° (mech faces "north"); rotation happens at runtime |
| Framing | mech **footprint centre** at frame centre, not bounding-box centre — keeps position stable in the hex across frames |
| Lights | key + fill + rim parented to the camera, fixed intensity, **neutral white** — identical lighting on every mech and every frame (colour would break the luminance contract) |
| Background | `film_transparent = True`, RGBA output |
| Renderer | Headless Blender (`blender -b -P tools/blender/render.py`), Cycles CPU — see `../architecture/blender-render-driver.md`. Blender is also the tool for the manual extraction route above (FBX export, PSK/PSA import), so one dependency covers both ends |

### 4.5 Frame cell, colour and atlas

- Current draw size is `0.84 × hex` = **84 × 72.746** logical px, so render at **84×72 per frame** (1×, the same as every existing asset) and accept the same ~1.01× vertical stretch that `Stretch = Fill` already applies to today's 84×72 art. There is no *extra* resampling introduced by the pipeline — it is identical to the status quo — but if pixel-exactness is ever wanted, render 84×73 and switch `UnitControl` to `Stretch = Uniform`. Because resolution is only a preset value, a sharper variant later is a `cellWidth`/`cellHeight` change plus render time, not a format change. (No padding inside the cell: the atlas packer adds the 2 px gutters between cells.)
- Sprite *size* is not chosen here: it comes from the tonnage-volume scale chain in `../architecture/asset-pipeline.md` §4.3, with per-chassis overrides in `scale-overrides.json`.
- Keep the **84:72 aspect** inside the cell, or switch `UnitControl` to `Stretch = Uniform`; `Stretch = Fill` on a square frame would squash the mech.
- **Luminance only, no hue.** The shipped sprites are tinted at runtime (`UnitControl` composites the owner colour over the sprite with the sprite's alpha as an `OpacityMask`), so they must be a white→dark-grey ramp: near-white base, darker creases and outline. Measured on the current art: ~98% of opaque pixels are neutral (R≈G≈B) across a full 4…255 range — the tint path already assumes exactly this. Details in `../architecture/blender-render-driver.md` §3.1.
- One atlas per chassis, **one row per clip**, fixed cell size → trivial slicing and a trivial Presto `SpriteSheet` mapping.

Proposed `.mmux` v2 additions (backwards compatible — `unit.png` stays as the static/reduced-motion fallback):

```jsonc
// unit-sprites.json
{
  "version": 1,
  "sheet": "unit-sprites.png",
  "cellWidth": 84,
  "cellHeight": 72,
  "colorSpace": "luminance",        // R = G = B; alpha = silhouette; tinted at runtime
  "pivot": [0.5, 0.5],
  "animations": {
    "Idle":       { "row": 0, "frames": 6,  "fps": 8,  "loop": true,  "hold": null },
    "Walk":       { "row": 1, "frames": 10, "fps": 12, "loop": true,  "hold": null },
    "Run":        { "row": 2, "frames": 10, "fps": 16, "loop": true,  "hold": null },
    "Jump":       { "row": 3, "frames": 8,  "fps": 12, "loop": true,  "hold": null },
    "ProneEnter": { "row": 4, "frames": 6,  "fps": 12, "loop": false, "hold": "last" },
    "ProneIdle":  { "row": 5, "frames": 4,  "fps": 4,  "loop": true,  "hold": null },
    "StandUp":    { "row": 6, "frames": 7,  "fps": 12, "loop": false, "hold": "last" },
    "Shutdown":   { "row": 7, "frames": 4,  "fps": 8,  "loop": false, "hold": "last" },
    "Destroyed":  { "row": 8, "frames": 8,  "fps": 10, "loop": false, "hold": "last" }
  }
}
```

Suggested clip costs at 12 fps: walk 10 frames ≈ 0.83 s loop; prone transition 6 frames = 0.5 s (matches a tabletop "falls this turn" beat); stand-up 7 frames ≈ 0.6 s.

### 4.6 Integration map (when we get past P0)

| Concern | File | Change |
|---|---|---|
| Package read | `src/MakaMek.Assets/Services/PackageReaders/MmuxUnitPackageReader.cs` | parse `unit-sprites.json` (+ optional sheet bytes) into `UnitPackage` |
| Package record | same file, `UnitPackage` | add `SpriteSheetData? Sprites` |
| Cache | `src/MakaMek.Assets/Services/UnitCachingService.cs` | second dictionary `SpriteCache`; keep atomic commit under `SyncRoot` |
| Image access | `src/MakaMek.Avalonia/MakaMek.Services.Avalonia/AvaloniaAssetImageService.cs`, `CachedImageService.cs`, `HybridImageService.cs` | new `GetSpriteSheet(assetType, assetName)` returning a decoded atlas + metadata |
| Render | `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/UnitControl.cs` | swap `Image` for a Presto `SpriteSheet` visual; play clip from state |
| Clip resolution (pure, testable) | new `src/MakaMek.Presentation/UiStates/UnitAnimationResolver.cs` | `(UnitStatus, MovementType, isMoving, isDestroyed) → clip name` — unit-testable, keeps UI thin |
| Reduced motion | `UnitControl` | fall back to static `unit.png` frame 0 (Presto `WhenReduced`) |

`UnitAnimationResolver` is where the value is: it belongs in Presentation (tested, coverage-tracked), not in the control.

### 4.7 Effort

| Phase | Scope | Estimate |
|---|---|---|
| P0 spike | 3 chassis (e.g. AS7 Atlas, COM Commando, LCT Locust), static top-down re-render, drop into `.mmux`, eyeball in game | 1–2 days |
| P1 | all 76 chassis static renders; camera/lighting settled; render script + atlas-less `unit.png` swap | 3–5 days (mostly unattended render time) |
| P2 | `.mmux` v2 + `UnitAnimationResolver` + `UnitControl` sprite playback; Idle/Walk/Run for the 3 pilot chassis | 3–4 days |
| P3 | prone/stand-up authored (4 body plans), full 76-chassis rollout | 1–2 weeks, dominated by animation authoring + review |

Per-chassis render time is minutes, not hours, if the scene is a template with linked meshes.

### 4.8 Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Licensing** | Renders are derivative works — cannot be committed or published | Ship the *generator*, not the output; user runs it against their own game copy into a local pack |
| Silhouette readability | MW5 mechs are busy; top-down may read worse than the clean MegaMek line art | Pilot side-by-side at 84×72 before committing; mitigate with outline/posterize post-process rather than changing the camera pitch (locked at 90° — see `../architecture/asset-pipeline.md` §4.2) |
| Missing arms/weapons | Extracted body may look "stripped" | Decide a canonical loadout per chassis; attach socket meshes |
| Skeleton variance | Retargeting prone clips across 76 rigs | Author per body plan, not per chassis |
| Asset size | 76 × 9 clips × 10 frames × 84×72 × 4 B ≈ **160 MiB** uncompressed (≈ 24 KiB/frame) | PNG on a grey ramp compresses this heavily, so it is not a packaging problem at 1×; if clips grow, cut frame counts or trim cells before raising resolution |
| Soft raster at zoom | 1× is as soft as the current art when zoomed or on high-DPI | Accepted for parity with existing assets; resolution is a preset value, so a high-DPI pass is a later `cellWidth`/`cellHeight` change, not a format change |
| Custom materials | Grey renders from FModel/umodel | Export via Mod Editor FBX, or author a simple 3-tone material (base/shadow/highlight) — arguably *better* for board readability |
| Format churn | `.mmux` v2 breaks readers | Keep `unit.png` authoritative; sprites optional; bump `manifest.json` version |

---

## 5. Legal position

> **Not legal advice.** The statements below are engineering constraints we choose to honour, not a legal interpretation of any agreement. Verify against the current EULA before relying on them.

MW5 assets and any render derived from them are PGI/Microsoft IP. Quoting the [MechWarrior 5: Mercenaries EULA](https://mw5mercs.com/eula) directly (26 July 2021 text):

- **License Grant** — "a non-exclusive, non-transferable, non-sublicensable worldwide license to install and use the Product … solely for your personal and non-commercial use".
- **Restrictions / Code of Conduct** (pursuant to Article 3) — the licensee shall not "(c) copy, reproduce, display, perform, or otherwise use the Product in a way that is not expressly authorized in this EULA" or "(d) sell, rent, lease, license, distribute, or otherwise transfer the Product".
- **Derivative works** — "You shall not reverse engineer, derive source code from, modify, adapt, translate, decompile, or disassemble the Product or create derivative works based on the Product, **except as set forth in Article 7 with respect to User Generated Content**". A rendered sprite of a game mesh is a derivative of the Product under that reading.
- **Ownership** — "Any reproduction or representation of these licensed materials in any way and for any reason is prohibited without Piranha Games' prior permission".

**The User-Generated-Content carve-out is real, and narrower than "modding is allowed".** Article 7 permits creating and distributing *your own* additions/mods; the separate [MW5 Mod Editor EULA](https://mw5mercs.com/static/docs/MW5_Mod_Editor_EULA.pdf) then scopes "Licensed Materials" (software + content) separately from the Editor (Epic-licensed) and permits Licensed Materials **inside a compliant, free Mod used with the game**. That does not make standalone redistribution of extracted assets — or of renders produced from them — a "mod": the carve-out is about shipping content that extends the game, under its conditions, not about publishing the game's art as an independent asset pack.

This is the same constraint that shaped the current asset story: `data/` art comes from the MegaMek Data Repository (CC BY-NC-SA 4.0) and is distributed separately as downloadable content.

Working rules we apply:

- ✅ **May ship:** the extraction/render tooling, the `.mmux` v2 format, the runtime sprite support, documentation.
- ❌ **May not ship:** rendered sprite atlases, or the extracted meshes, in the repo or in a downloadable pack.
- ⚠️ **A mod is a different act:** a free, game-integrated mod built with the Mod Editor may include Licensed Materials under that EULA's conditions. That does not license *this repository* to ship MW5-derived sprites, and it does not turn an asset pack into a mod.
- ✅ **Users may:** run the tool against their own licensed copy and load the result through `LocalFolderResourceStreamProvider` (which already exists precisely for "assets not published to GitHub").
- Alternative for a *shippable* art pass: run the same pipeline against commissioned or CC-licensed mech models, or trace silhouettes into original vector art.
- The pipeline enforces the first two mechanically with a closed `licenseStatus` (`redistributable` / `not-redistributable` / `unknown`, default-deny) — see `../architecture/asset-pipeline.md` §8.

## 6. Recommendation

1. **P0 first, nothing else.** Render the 3 pilot chassis top-down at **84×72** (the same size as the existing art), drop them into `.mmux`, and judge them against the MegaMek sprites in-game. If the silhouette doesn't read at board size, the whole pipeline is the wrong investment and no format work is justified.
2. If P0 passes, **P1 is a pure asset job** — no code — and can run in parallel with everything else.
3. Only then design `.mmux` v2 and `UnitAnimationResolver`, and start with Idle/Walk/Run. Prone/stand-up are authored content, so schedule them last and per body plan, not per chassis.
4. Keep the generator under `tools/` with a gitignored output folder; never point CI at it.

## 7. Sources

- Gildor's forum, *MechWarrior 5: Mercenaries* topic (UE 4.26, umodel 1590 works for meshes/textures/animations): <https://www.gildor.org/smf/index.php/topic,7536.0.html>
- Gildor's UE Viewer documentation: <https://www.gildor.org/projects/umodel>
- FModel: <https://fmodel.app/> · wiki: <https://github.com/4sval/FModel/wiki>
- MW5 Mod Editor guide (asset workflow, Save To Mod, packaging): <https://static.mw5mercs.com/docs/MW5Mercs_Mod_Editor_Guide_(v2.3).pdf>
- Mechwarrior Foundry wiki, *Modding Mechs* (chassis = skeletal mesh + animations + data assets): <https://mechwarrior5modding.fandom.com/wiki/Modding_Mechs>
- Blender PSK/PSA extension (imports umodel/FModel `.psk/.pskx/.psa`): <https://extensions.blender.org/add-ons/io-scene-psk-psa/>
- Knockdown/fall animations do not exist in MW5; MercTech 1.0 authored them: <https://www.reddit.com/r/Mechwarrior5/comments/1tns9sz/knockdowns_merctech_10_feature_preview/>
- In-repo: [presto-animation-capabilities.md](presto-animation-capabilities.md) §3.6 (sprite sheet runtime), `src/MakaMek.Assets/Services/PackageReaders/MmuxUnitPackageReader.cs`, `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/UnitControl.cs`
