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

## Build & run

```bash
dotnet build src/MakaMek.Tools/MW5Extractor/MW5Extractor.Cli/MW5Extractor.Cli.csproj

export MW5_PAKS_PATH="…/MechWarrior 5 Mercenaries/Content/MW5Mercs/Content/Paks"   # or pass --paks
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
| `discovery-report` | every chassis in one JSON file, plus assumption checks against the research notes | `discovery-report` |

Asset arguments accept a full virtual path, a path suffix (`Atlas/Model/Body/Atlas_SKM`) or a unique
asset name (`Atlas_SKM`). Ambiguous names fail with the candidate list.

## Output

Nothing is written outside gitignored `artifacts/mw5/`:

```
artifacts/mw5/
  mount-report.json
  discovery-report.json
  inspect/<asset>.json
  extract/<asset>/…           # mirrors the virtual path, e.g. …/Atlas/Model/Body/Atlas_SKM.glb
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
- **Not implemented here** (later slices): merging attachments into one skinned mesh, the MakaMek →
  MW5 equipment mapping table, animations, `.mmux` output and the 199-variant batch.
- Chassis identity is the MW5 folder; the `chassis-map.json` (MW5 folder → MegaMek code) belongs to the
  assembly step.

## Licensing

The tooling ships; MW5-derived output does not. Meshes, textures, exported JSON and rendered sprites are
PGI/Microsoft IP and stay local — never commit them, never publish a pack built from them. See
[../analysis/mw5-model-extraction.md](../../../docs/analysis/mw5-model-extraction.md) §5.

Third-party dependencies are pinned to `CUE4Parse` / `CUE4Parse-Conversion` `1.2.2.202610` (Apache-2.0).
Those packages pull `SixLabors.ImageSharp` and `Microsoft.Bcl.Memory` versions with published advisories;
`NU1902`/`NU1903` are suppressed in this project only (local tool, pinned versions, no untrusted input) and
should be re-checked on the next CUE4Parse bump.
