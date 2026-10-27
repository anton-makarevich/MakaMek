---
name: cue4parse
description: Reference manual for CUE4Parse and the MW5Extractor tool (src/MakaMek.Tools/MW5Extractor) — pak mounting, virtual-path enumeration, skeletal mesh/skeleton/socket reading, glTF/PSK/JSON export, and MechWarrior 5 asset gotchas. Manual invocation only: load it when the user asks for it or when MW5Extractor/AGENTS.md says to, not for generic C#/Avalonia/asset tasks.
---

# CUE4Parse / MW5Extractor reference

**Load this only when working on `src/MakaMek.Tools/MW5Extractor` or CUE4Parse code.** It is a manual,
not a task trigger: it does not describe a reusable task class, so unrelated requests should not match it.

Facts below were verified against `CUE4Parse` / `CUE4Parse-Conversion` **1.2.2.202610** and a real MW5
Mercenaries pak on 2026-10-27. The CUE4Parse wiki is mostly stubs — when in doubt, read the library source
and the `CUE4Parse.Example` project (paths in §9), then confirm with a tiny run against a real pak.

## 1. Mounting a pak set

```csharp
var provider = new DefaultFileProvider(
    new DirectoryInfo(paksDirectory),
    extraDirectories,                          // DirectoryInfo[] for DLC / Saved\Paks mods
    SearchOption.TopDirectoryOnly,
    new VersionContainer(EGame.GAME_UE4_26),   // MW5 is UE 4.26
    StringComparer.OrdinalIgnoreCase);

provider.Initialize();          // scans local files, registers paks (no asset parsing)
provider.SubmitKey(new FGuid(), new FAesKey(hexKey));   // only for encrypted paks
provider.Mount();               // reads pak indexes; returns mounted archive count
```

- `provider.Files` (285,997 paths for MW5) is a **path index**; reading it costs milliseconds. Do discovery
  here, never by loading assets.
- `provider.MountedVfs` → `IAesVfsReader` (`Name`, `Path`, `Length`, `FileCount`, `MountPoint`,
  `IsEncrypted`, `EncryptionKeyGuid`, `CompressionMethods`); `provider.RequiredKeys` lists keys still missing.
- `provider.TryGetGameFile(path, out var file)` resolves with or without the `.uasset` extension.
- Extensions are a family: `.uasset` is the package, `.uexp`/`.ubulk`/`.uptnl` are sidecars. Filter with
  `GameFile.IsUePackage` when matching paths, or the same asset looks ambiguous.
- MW5 base installs ship **one** huge pak (`MW5Mercs-WinGDK.pak`, ~99 GiB) with DLC content nested inside
  it (`MW5Mercs/Content/DLC2`, `DLC7`, …). Don't assume `<DLC>/Content/...` roots.

## 2. Native codecs — do not let CUE4Parse download them

`OodleHelper.Initialize(path)` and `ZlibHelper.Initialize(path)` `await DownloadOodleDllAsync(...)` when the
library is missing — i.e. they reach GitHub. A read-only tool must call them **only with an explicit path**
(or rely on `CUE4ParseNatives` when available). Check `OodleHelper.Instance is not null` afterwards.
MW5's pak mounts and exports meshes without either; textures/`ubulk` are the usual Oodle consumers.

## 3. Reading a mesh

```csharp
var mesh = provider.LoadPackageObject<USkeletalMesh>("MW5Mercs/Content/Objects/Mechs/Atlas/Model/Body/Atlas_SKM");

mesh.LODModels[i].NumVertices                          // 40,931 at LOD0 for the Atlas
mesh.LODModels[i].Sections.Sum(s => s.NumTriangles)    // 38,268 — the engine's own triangle count
mesh.ReferenceSkeleton.FinalRefBoneInfo[].Name.Text    // bones (110 for Atlas, 272 for Mech_SKL)
mesh.ReferenceSkeleton.FinalRefBonePose[].Translation  // bind pose
mesh.SkeletalMaterials[].MaterialSlotName.Text         // slot names: Body, Variant, Body_LOD, …
mesh.Sockets                                          // FPackageIndex[]; often EMPTY for mech bodies
mesh.Skeleton?.Load()                                 // USkeleton: ReferenceSkeleton + Sockets + BoneCount
mesh.Bounds                                           // FBoxSphereBounds in cm, Z-up
```

Sockets are `FPackageIndex` and must be resolved:

```csharp
var socket = index.Load() as USkeletalMeshSocket;   // SocketName, BoneName, RelativeLocation/Rotation/Scale
```

`USkeletalMeshSocket` properties are stock and readable; MW5 stores weapon **mount** sockets on the shared
`Mech_SKL` (`Torso_Weapon`, `Forearm_Left_Weapon`, `Clavicle_Left_Weapon`, `Weapon_Melee_*`,
`WeldTarget_*`), not on individual bodies and not per hardpoint.

Animations: `LoadPackageObject<UAnimSequence>(...)` → `SequenceLength`, `NumFrames`,
`CompressedTrackToSkeletonMapTable`, `Skeleton`. `UAnimMontage` / `_AOF` are separate exports.

## 4. Exporting: `ExportSession` + `ExportOptions`

```csharp
var options = new ExportOptions(
    EMeshFormat.Gltf2,                 // ActorX | Gltf2 | UEFormat | USD
    socketFormat: ESocketFormat.Socket, // Socket | Bone | None
    exportMaterials: true,
    textureFormat: ETextureFormat.Png);

var session = new ExportSession();
session.Add(exportObject);                     // UObject; dispatches to the right exporter
var results = await session.RunAsync(outDir, options, null, ct);
// results[i].Success, .DiskFilePaths, .Error
```

- `ExportOptions` is a **readonly-field record with a big constructor** — object-initializer assignment
  fails to compile. Use positional/named constructor arguments.
- Output is written **mirroring the virtual path** under `baseDirectory`
  (`outDir/MW5Mercs/Content/…/Atlas_SKM.glb`), one file per export, plus material/texture files.
- `JsonPropertiesExporter` (via `session.Add(new JsonPropertiesExporter(obj))`, `_ => "json"` in
  MW5Extractor) writes the **full tagged structure** of an export to `.json`. This is how MW5's custom
  classes become readable (see §5).
- glTF export of the Atlas body: ~5.5 MB `.glb`, ~0.9 s, LOD0, 4 material slots, no separate textures.
  Materials resolve to MW5 material instances; the board look is stage 2's responsibility.

## 5. Custom cooked classes ("nothing is readable") — actually not true

MW5's data assets are custom classes: `MWMechHardpointAsset`, `MWMechDataAsset`,
`MWMechLoadoutAsset`, `MWMechModelDataAsset`. CUE4Parse returns them as plain `UObject`, but:

- `export.ExportType` still gives the real class name (`MWMechHardpointAsset`).
- `export.Properties` is a list of `FPropertyTag` (`CUE4Parse.UE4.Assets.Objects`), readable generically:
  `Name` (FName), `PropertyType` (FName), `Tag` (`FPropertyTagType`), `Size`, `ArrayIndex`.
- Struct members are untyped fallbacks: `tag.Tag` is a `StructProperty` whose value is an
  `FStructFallback` (`Properties` may be empty — the struct layout is unknown to CUE4Parse).
- The reliable escape hatch is `JsonPropertiesExporter`: `Atlas_HPS.json` (304 KB) contains
  `MechHardpoints.WeaponSlots[]` with `HardpointSlotID` (`Torso_Left_MH1_lrm10`),
  `MechSkeletonSocketID` (`Torso_Weapon`), `MechPart`, `SlotTypeAssetId` and `HardpointVisuals`
  (weapon tag → weapon Blueprint path). `AS7-D_Loadout.json` contains `InstalledWeapons[]` as
  `HardpointSlotID` → weapon data asset.
- Every one of these assets also carries `ReimportFileName` — the `.json` the asset was imported from in the
  MW5 Mod Editor. Useful as a sanity check that the data was authored as data, not code.

So: **do not write a bespoke deserialiser before trying `--format json`.**

## 6. MechWarrior 5 layout and naming gotchas

| Thing | Reality |
|---|---|
| Chassis folder | `…/Objects/Mechs/<Folder>/`, but Clan mechs are one level deeper: `…/Objects/Mechs/CLANS/<Chassis>/`. Resolve the folder from the child folder (`Model`, `Loadout`, `MDA`, `Skins`, `Animation`, …), not from the segment after `Mechs`. |
| Monolithic body | `Model/Body/<Folder>_SKM` (case varies: `Kitfox_SKM` in folder `KitFox`) |
| Modular body | Newer DLC chassis have no `<Folder>_SKM`; body parts are individual meshes (`Model/Body/Forearm_Left`, `Clavicle_Left`, …) plus a body mesh named after the model code (`Model/Body/AS7II`). Detect via the weapon-name chassis token. |
| Cockpit | `Model/Cockpit/<Chassis>_Cockpit_SKM` + `<Chassis>_CockpitGlass_STM`; not every chassis has glass, and modular chassis name it differently (`Model/AtlasII_Cockpit`). |
| Weapons | `Model/Weapons/Weapon_Mech_<Chassis>_<Location>_<Hardpoint>_<Weapon>_SKM` with `AH|EH|BH|MH|EB` hardpoint tokens; `_ANI`/`_PHY` are sidecars, and some weapons have no hardpoint token (mount adapters) or an empty weapon token (`EB1_Blank`). |
| Variants | `MDA/<Model>_MDA`, `Loadout/<Model>_Loadout`, `UnitCard/…`, sometimes directly in the chassis folder (DLC8 `AtlasC_Loadout`). |
| Animations | `Animation/<Chassis>_<Clip>_ANI` (plus `_AOF`, `_Montage`, `_BLS`). |
| Units / axes | UE cm and Z-up: a 18.4 m Atlas has `Bounds.BoxExtent ≈ (347, 714, 919)`. Divide by 100 for metres; mech faces **+X**. |
| Materials | Custom graphs; exported albedo is a plain base colour. Never expect board-ready sprites from stage 1. |
| DLC overrides | The same relative path can exist in several `DLCn` roots; load order decides. Report duplicates instead of silently picking one. |

## 7. Paths worth knowing in this repo

- `src/MakaMek.Tools/MW5Extractor/MW5Extractor/UnrealArchive.cs` — provider lifecycle, name resolution.
- `…/Discovery/Mw5AssetPath.cs` — the path/name classifier (chassis folder, weapon slots, variants).
- `…/Extraction/AssetExtractor.cs` — glTF/PSK/JSON export.
- `…/Reporting/*.cs` — report DTOs that end up in `artifacts/mw5/`.
- `docs/analysis/mw5-model-extraction.md` — extraction fact base (some §2.2 assumptions are now corrected).
- `docs/architecture/asset-pipeline.md` — how the extractor becomes the `unreal` adapter.

## 8. Anti-patterns

- Loading assets to answer a discovery question (285k paths are cheap; 285k packages are not).
- Hard-coding `EGame.GAME_UE5_x` for MW5; drive the version from `MountOptions.Game` → `VersionContainer`.
- Assuming one body mesh per chassis or one socket set per body (see §6).
- Treating `FStructFallback` as "unreadable" — try the JSON exporter first, then walk `FPropertyTag`.
- Keeping any exported `.glb`/`.png`/`.json` in the repo: MW5 output is never committed.

## 9. Verify against source

- `CUE4Parse` repo: `CUE4Parse/FileProvider/DefaultFileProvider.cs`, `FileProvider/Vfs/AbstractVfsFileProvider.cs`,
  `UE4/Assets/Objects/FPropertyTag.cs`, `Compression/OodleHelper.cs`.
- `CUE4Parse-Conversion` repo: `ExportSession.cs`, `Options/ExportOptions.cs`, `Exporters/SkinnedAssetExporter.cs`,
  `Formats/Meshes/GltfMeshFormat.cs`, `Exporters/JsonPropertiesExporter.cs`.
- Sample usage: `CUE4Parse.Example/Program.cs` (mount + key + load), `Exporter.cs`/`Exporter2.cs`.
- The pinned package API may differ from `master`: compile against the NuGet package and trust the compiler.
