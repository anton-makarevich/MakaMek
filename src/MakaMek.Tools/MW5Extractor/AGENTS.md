# AGENTS.md — MW5Extractor

Rules for agents (and humans) changing this tool.

## Before touching extractor code

1. **Load the `skills/cue4parse` skill first.** It is a reference manual for this exact folder: API recipes,
   MW5 gotchas and the source paths to verify claims against. It is not loaded by unrelated tasks.
2. Read this file and `README.md`; the verified pak facts in the README are the baseline for any change.

## Non-negotiables

- **Read-only on the game.** Never modify, repack or write into the game install. Output goes to
  `artifacts/mw5/` only (gitignored).
- **No MW5-derived file in git.** Meshes, textures, exported JSON, sprites, logs — none of it. If a change
  needs a fixture, generate it at test time or use a synthetic path string.
- **The game path is configuration.** It comes from `--paks` / `MW5_PAKS_PATH`. Never hard-code a machine
  path, never commit one.
- **Do not enable implicit native downloads.** `OodleHelper`/`ZlibHelper` download DLLs from GitHub when no
  path is given; the extractor deliberately initialises them only from an explicit path. Keep it that way.
- **No behaviour only in the CLI.** Logic belongs in the library so it can become the `unreal` adapter of
  `MakaMek.Pipeline.Import`. The CLI parses arguments, calls the library, renders the envelope.

## Enumerate before you load

`provider.Files` is a cheap pak-index walk (285k paths, no deserialisation). `LoadPackage`/`LoadObject` are
not. Discovery and classification must stay path-based; only `inspect` and `extract` load assets.

## Layout

```
MW5Extractor/            library
  UnrealArchive.cs       provider lifecycle, AES key, name resolution, enumeration
  MountOptions.cs        configuration (paks, extra dirs, key, native libraries, EGame)
  NativeSupport.cs       optional Oodle / zlib-ng availability
  Discovery/             Mw5AssetPath (path/name classifier), ChassisDiscovery (report assembly)
  Inspection/            AssetInspector (exports, tags, bones, sockets, LODs)
  Extraction/            AssetExtractor (glTF / PSK / properties JSON)
  Animation/             ClipVocabulary (clip-map.json), ClipInventory, AnimationClipReader
  Assembly/              MechAssembler, ModelMerger (static), SkinnedModelBuilder (animated)
  Reporting/             JSON DTOs for mount, discovery, inspect, extract, clips, assembly
  Json/                  serializer options and the --json envelope
clip-map.json            canonical clip vocabulary + aliases (data, not code)
MW5Extractor.Cli/        CLI: mount, ls, find, inspect, extract, clips, assemble, discovery-report
```

## Animation rules

- **Aliases live in `clip-map.json`** (ordered patterns per canonical clip + an exclusion list). Do not add a
  chassis-name switch; add a pattern.
- **Tracks map to bones by name.** `Mech_SKL` (272 bones), `Atlas_SKM` (110) and an animated clip (39) have
  different indices; index-based mapping silently animates the wrong limb.
- **Convert keys the way CUE4Parse converts bones**: y/z swap with negated quaternion W, translations cm → m.
  `CUE4Parse-Conversion.AnimationExporter` cannot write glTF; the glTF animation is written with SharpGLTF.
- **Strip root motion** (horizontal only), report how much, and drop a looping clip's duplicate closing key.
  Never let stage 2 compensate for motion the game rules do not produce.
- **Animated models stay in MW5 source space** (Y-up, facing +X, `modelSpace = "mw5-source"`,
  `modelYawDegrees = 90`): Blender drops an armature-level transform on skinned meshes, which would split body
  and attachments. Static models stay canonical. Do not "fix" this by baking the basis into the armature.
- **Attachments are rigid skinned vertices**, bound 100% to their mount joint. Bone-parented glTF nodes survive
  Blender's importer unreliably.

## Tests

Pak-dependent behaviour cannot be unit-tested without a game install, and the repo's tool projects are not
covered. Keep pure logic (path classification, report assembly) free of CUE4Parse calls so it *can* be
tested later; verify changes by running the verbs against a real install and reading the JSON reports.

## Conventions

- net10.0, nullable enabled, file-scoped namespaces, records for DTOs, `System.CommandLine` for verbs.
- Exit codes: `0` ok, `1` error, `2` bad arguments, `3` input missing, `4` export failed.
- Sort everything that ends up in JSON (ordinal, invariant culture) so reports diff cleanly.
- Bump `Directory.Build.props` `VersionPrefix` (patch only) when changing `src/` code.
