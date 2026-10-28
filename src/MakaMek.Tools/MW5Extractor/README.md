# MW5Extractor

Headless stage-1 extractor for the MakaMek asset pipeline: find, inspect and export MechWarrior 5 assets
(skeletal meshes, cockpits, weapons, hardpoint/loadout data, animations) without a GUI.

Built for spike [#1652](https://github.com/anton-makarevich/MakaMek/issues/1652) — "render fully assembled
MW5 models per variant" — as the replacement for the manual FModel step used by
[#1642](https://github.com/anton-makarevich/MakaMek/issues/1642). The library is meant to become the
`unreal` adapter of `MakaMek.Pipeline.Import` ([asset-pipeline.md](../../../docs/architecture/asset-pipeline.md) §3.1);
the CLI is a thin wrapper, so no behaviour lives only in the CLI.

## What the pak actually contains (verified 2026-10-27, MW5 Mercs + DLC, UE 4.26)

| Fact | Value |
|---|---|
| Paks | 1 (`MW5Mercs-WinGDK.pak`, 99 GiB), no encryption, mount in ~0.8 s |
| Virtual files | 285,997 (281,206 under `MW5Mercs/Content`) |
| Chassis | **104** (95 `Objects/Mechs/<Folder>` folders; Clan mechs live one level deeper, `Objects/Mechs/CLANS/<Chassis>`) |
| Body layout | 102 monolithic (`Model/Body/<Chassis>_SKM`), **1 modular** (Atlas II: part meshes + `Model/Body/AS7II`), 1 (Blackhawk KU) without a body mesh |
| Cockpits / glass | 101 `_Cockpit_SKM` / 90 `_CockpitGlass_STM` (14 chassis ship no glass) |
| Hardpoint sets | **104 / 104** chassis have `<Chassis>_HPS` |
| Variant assets | 686 `_MDA` / `_Loadout` / `_UnitCard` assets |
| Weapon meshes | 12,541 `Weapon_Mech_<Chassis>_<Location>_<Hardpoint>_<Weapon>_SKM` |
| Animations | 10,978 `_ANI` / `_AOF` / `_Montage` assets |
| Body mesh sockets | **0** — mount points are not on the body mesh |
| Shared skeleton sockets | `Mech_SKL`: 272 bones, **47 sockets** (`Torso_Weapon`, `Forearm_Left_Weapon`, `Weapon_Melee_Left`, `WeldTarget_*`, …) — coarse mount transforms, no per-hardpoint identity |
| Custom data classes | `MWMechHardpointAsset`, `MWMechDataAsset`, `MWMechLoadoutAsset`, `MWMechModelDataAsset` — not typed by CUE4Parse, **but readable via `extract --format json`** |

The last row is the decisive result: `Atlas_HPS.json` exposes `MechHardpoints.WeaponSlots[]` with
`HardpointSlotID` (`Torso_Left_MH1_lrm10`), `MechSkeletonSocketID` (`Torso_Weapon`), `MechPart`, the
slot-type asset and a `HardpointVisuals` map from weapon tags (`Weapon.Missile.LRM.LRM10`) to the weapon
Blueprint path; `AS7-D_Loadout.json` lists `InstalledWeapons[]` as `HardpointSlotID` → weapon data asset.
So hardpoint topology, mount socket and the weapon-asset mapping are all machine-readable — no
hand-written deserialiser and no hand-curated weapon table are required for the assembly step.

Animations decode too: `UAnimSequence.ConvertAnims()` yields one track per skeleton bone with rotated/
translated/scaled keys, and `cli clips --probe` turns that into a measured per-chassis inventory. MW5 keys at
30 fps; the Atlas walk is 61 keys over 2 s with 8.6 m of (stripped) root travel.

## Build & run

```bash
dotnet build src/MakaMek.Tools/MW5Extractor/MW5Extractor.Cli/MW5Extractor.Cli.csproj

# The paks directory — the folder that contains MW5Mercs-WindowsNoEditor.pak / MW5Mercs-WinGDK.pak:
#   Epic / Steam: …/MechWarrior 5 Mercenaries/MW5Mercs/Content/Paks
#   Xbox / GDK:   …/MechWarrior 5 Mercenaries/Content/MW5Mercs/Content/Paks
export MW5_PAKS_PATH="…/MechWarrior 5 Mercenaries/MW5Mercs/Content/Paks"   # or pass --paks
dotnet run --project src/MakaMek.Tools/MW5Extractor/MW5Extractor.Cli -- mount
```

Every verb accepts `--paks`, `--extra <dir>` (repeatable), `--aes <hex>`, `--oodle <dll>`, `--zlib <dll>`,
`--game <EGame>` and `--json`. Unset values fall back to `MW5_PAKS_PATH`, `MW5_AES_KEY`, `MW5_OODLE_DLL`,
`MW5_ZLIB_DLL`.

| Verb | Purpose | Example |
|---|---|---|
| `mount` | archives, engine version, file counts, content roots, native codec availability | `mount` |
| `ls <prefix>` | virtual path index, with `--pattern` glob and `--limit` | `ls MW5Mercs/Content/Objects/Mechs/Atlas --pattern *_SKM.uasset` |
| `find <chassis>` | classify one chassis: body, body parts, cockpit, glass, hardpoints, weapons, variants, animations | `find Atlas` / `find AS7-D` |
| `inspect <asset>` | exports, generic tags, bones, sockets with transforms, LODs, material slots, animation info | `inspect Atlas_SKM` |
| `extract <asset>` | `--format gltf\|psk\|json` → files on disk | `extract Atlas_SKM` / `extract Atlas_HPS -f json` |
| `clips <chassis>` | map the chassis' animation assets onto the canonical vocabulary; `--probe` decodes frames/fps/root motion/loop closure | `clips AS7-D --probe` |
| `assemble <chassis\|variant>` | merge body + cockpit + loadout weapons into **one** renderable glTF (`<VARIANT>.glb` + `<VARIANT>.assembly.json`); `--clips Walk` produces a skinned animated `<VARIANT>.animated.glb` | `assemble AS7-D --clips Walk` |
| `discovery-report` | every chassis in one JSON file, plus assumption checks against the research notes | `discovery-report` |

Asset arguments accept a full virtual path, a path suffix (`Atlas/Model/Body/Atlas_SKM`) or a unique
asset name (`Atlas_SKM`). Ambiguous names fail with the candidate list.

## Assembly: one model per variant

`assemble AS7-D` writes one static glTF in canonical space (metres, Y-up, mech facing **+Z**, pivot at the
footprint centre, lowest point on the ground) plus an `assembly.json` describing every part. The renderer
only needs that single file (`render.py --model AS7-D.glb --scale <chassis scale> --fit`).

```
body         Atlas_SKM                       identity
cockpit      AS7_TP_Cockpit_DM0_STM          bone "Cockpit"  (from <Chassis>_MDL → MechDestructibleState.ThirdPersonCockpitData)
weapon       Weapon_…_BH1_AC20_SKM           bone "Torso_Pitch"      slot Torso_Right_BH1_autocannon20
…
```

The mapping is data all the way down — no shipping weapon table:

1. `<Model>_Loadout` → `InstalledWeapons[]` (`HardpointSlotID` + weapon data asset).
2. Weapon data asset → `WeaponType.TagName` (`Weapon.Ballistic.AutoCannon.Standard.AC20`).
3. `<Chassis>_HPS` → the slot (exact id, else same location/hardpoint + slot type), its
   `MechSkeletonSocketID` and `HardpointVisuals[tag]` → the weapon Blueprint.
4. Blueprint → `_SKM` mesh; socket → bone (MW5's weapon sockets are identity offsets on `Mech_SKL`).
5. Bone world transform from the exported body glTF (rest pose) positions the part; the body itself needs none.

Notes that matter:

- **The first-person cockpit is not the visible head.** `Atlas_Cockpit_SKM` is the FPP interior (scaled 0.4 in
  its Blueprint); the exterior head is the static mesh named by `<Chassis>_MDL` and bone-parented to `Cockpit`.
  `assemble` uses the third-person mesh and falls back to the FPP cockpit only when a chassis declares none.
- **Weapon meshes are authored relative to their mount**, so the mount bone (not the slot id) places them; the
  torso hardpoints all share `Torso_Weapon` (`Torso_Pitch`) and differ by their baked local geometry.
- Weapons that have no visual (melee actuators, empty slots) are listed under `unmapped` with a reason.
- Output is deterministic: the same command twice produced a byte-identical `.glb` (verified 2026-10-27).

## Animation: canonical clips, one model

`clips <chassis> --probe` is the inventory: it resolves MW5 asset names to the canonical vocabulary
(`Idle`, `Walk`, `Run`, `Jump`, `Shutdown`, `Destroyed`) and reports frame counts, derived fps, duration,
animated bones, root travel and loop closure. Aliases are data in `clip-map.json` (next to the library):

```jsonc
{ "name": "Walk", "loop": true, "patterns": ["_WalkForward_Straight_ANI", "_WalkForward_ANI", "_Walk_ANI"] }
```

`assemble <variant> --clips Walk` then bakes the clip into a **skinned** glTF:

```
<variant>.animated.glb      body skin + skeleton + attachments + glTF animation named "Walk"
<variant>.assembly.json     animations[]: source asset, keys, fps, duration, root travel, loop closure
```

What is different from the static merge:

- **The body keeps its skin and joints.** A clip is bone-local transforms, so only geometry that still
  references the joints can be animated. Attachments (cockpit, weapons) are rigidly bound to their mount joint
  in the same skin, not merged into the body.
- **Tracks map by bone name.** `Mech_SKL` has 272 bones, `Atlas_SKM` 110, and a walk animates 39 — indices do
  not line up.
- **Keys are converted like the exporter converts bones:** y/z swap with negated quaternion W, translations
  cm → m. `CUE4Parse`'s `AnimationExporter` cannot write glTF (ActorX/UEFormat/USD only), so the glTF animation
  is written with SharpGLTF.
- **Root motion is stripped** (horizontal only) and reported; the sprite is board-anchored.
- **The closing key of a loop is dropped** (61 keys → 60 sampled poses for the 2 s walk).
- **The animated model stays in MW5 source space** (Y-up, facing +X). Blender's glTF importer cancels an
  armature-level transform on skinned meshes; the assembly report says `modelSpace = "mw5-source"` and
  `modelYawDegrees = 90`, and the driver applies `--model-yaw 90`.

Full walk pipeline (after `assemble AS7-D --clips Walk`):

```bash
F:/blender/blender.exe -b --factory-startup -noaudio --python-exit-code 1 \
  -P tools/blender/render.py -- \
  --model artifacts/mw5/assemble/AS7-D/AS7-D.animated.glb \
  --preset tools/blender/board-topdown.json \
  --scale 5.098 --model-yaw 90 --fit --supersample 4 \
  --clips Walk --out artifacts/spike/anim/AS7-D \
  --report artifacts/spike/anim/AS7-D/report.json

python tools/blender/sheet.py \
  --frames artifacts/spike/anim/AS7-D/frames --report artifacts/spike/anim/AS7-D/report.json \
  --assembly artifacts/mw5/assemble/AS7-D/AS7-D.animated.assembly.json \
  --preset tools/blender/board-topdown.json \
  --out artifacts/spike/anim/AS7-D --frames-out artifacts/spike/anim/AS7-D/frames-1x --name AS7-D
```

Verified on `AS7-D` (2026-10-27): Walk = 61 keys @ 30 fps / 2 s, 8.61 m root travel stripped, 39 animated
bones, loop closure 0°, 60 sampled poses → 10 shipped frames at 12 fps, fitted 1× cell 91 × 63.

## Output

Nothing is written outside gitignored `artifacts/mw5/`:

```
artifacts/mw5/
  mount-report.json
  discovery-report.json
  inspect/<asset>.json
  clips/<chassis>.json        # canonical clip inventory (+ decoded metrics with --probe)
  extract/<asset>/…           # mirrors the virtual path, e.g. …/Atlas/Model/Body/Atlas_SKM.glb
  assemble/<variant>/…        # <VARIANT>.glb + .assembly.json, or <VARIANT>.animated.glb with --clips
```

## Exit codes

`0` ok · `1` unexpected error · `2` bad arguments / ambiguous asset · `3` pak directory or asset missing ·
`4` export failed.

## Notes and limits

- **Native codecs are never downloaded.** CUE4Parse's helpers would fetch Oodle/zlib-ng from the network;
  this tool only loads a library you point it at, and reports availability in `mount`. The current MW5 pak
  mounts and exports without either.
- **glTF exports are faithful, not pretty:** materials resolve to material instances (not board-look),
  textures come out where referenced. Rendering stays stage 2's job (`tools/blender/render.py`).
- **Not implemented here** (later slices): `.mmux` v2 output, the 199-variant batch, published contact sheets
  and animation authoring for prone/stand-up (MW5 ships none).
- Chassis identity is the MW5 folder; the `chassis-map.json` (MW5 folder → MegaMek code) belongs to the
  assembly/packaging step. `assemble` already reports the variant id it used.

## Licensing

The tooling ships; MW5-derived output does not. Meshes, textures, exported JSON and rendered sprites are
PGI/Microsoft IP and stay local — never commit them, never publish a pack built from them. See
[../analysis/mw5-model-extraction.md](../../../docs/analysis/mw5-model-extraction.md) §5.

Third-party dependencies are pinned to `CUE4Parse` / `CUE4Parse-Conversion` `1.2.2.202610` (Apache-2.0).
Those packages pull `SixLabors.ImageSharp` and `Microsoft.Bcl.Memory` versions with published advisories;
`NU1902`/`NU1903` are suppressed in this project only (local tool, pinned versions, no untrusted input) and
should be re-checked on the next CUE4Parse bump.
