# MMTX Terrain Asset Bundle Format

## Overview

MMTX (MakaMek Terrain eXchange) is a package format for distributing terrain assets. It follows the same pattern as MMUX (MakaMek Unit eXchange) but is specifically designed for terrain themes with variant support.

## Bundle Structure

MMTX packages are standard ZIP archives with the following structure:

```text
theme.mmtx
├── manifest.json           # Required: Theme metadata
├── base-{variant}.png      # Base terrain images
├── terrains/                # Optional: Terrain overlay images
│   ├── lightwoods-{variant}.png
│   ├── heavywoods-{variant}.png
│   ├── rough-{variant}.png
│   ├── water-{variant}.png
│   ├── road/                # Optional: Road/bridge bitmask textures
│   │   ├── 000001.png       # Bitmask texture (6-bit binary)
│   │   ├── 000011.png
│   │   └── ...
│   └── ...
└── edges/                  # Optional: Edge effect images
    ├── top-{direction}-{variant}.png
    └── bottom-{direction}-{variant}.png
```

## manifest.json Schema

```json
{
  "id": "makamek.biomes.grasslands",
  "name": "Grasslands",
  "version": "1.0.0",
  "requiredMakaMekVersion": "0.53.6",
  "description": "Grasslands biome",
  "author": "MakaMek"
}
```

### Required Fields

- **id**: Unique identifier for the biome (alphanumeric, dashes allowed)
- **name**: Human-readable display name
- **version**: Semantic version (major.minor.patch)
- **requiredMakaMekVersion**: Minimum MakaMek version compatibility

### Optional Fields

- **description**: Theme description
- **author**: Creator attribution

## Terrain Data Encoding

Terrain properties beyond type are serialized via `TerrainData`. The following terrain-specific properties use the `Height` and `ConstructionFactor` fields:

| Terrain Type | Height | ConstructionFactor |
|---|---|---|
| **Bridge** | Bridge surface elevation above hex base (e.g., `1` for one level above ground). Positive integer. | Maximum tonnage the bridge can support (e.g., `60` for 60 tons). Used for bridge collapse mechanics. |
| **Water** | Water depth as non-positive value: `0` = shallow/fordable, `-1` = standard, `-2+` = deep. | `null` (not used) |
| **Road**, **Pavement**, **Clear**, **Woods**, **Rough** | `null` (not used) | `null` (not used) |

## Asset Naming Conventions

### Base Terrain

Base terrain images represent the underlying hex background:

```
base-{variant}.png
```

- Located in root directory
- **variant**: Optional numeric suffix (1-indexed)
  - `base-1.png` → variant 1
  - `base-2.png` → variant 2
  - `base.png` (no suffix) → variant 0

Examples:
- `base-1.png` - First base terrain variant
- `base-2.png` - Second base terrain variant
- `base.png` - Default base terrain (variant 0)

### Terrain Overlays

Terrain overlays are placed in the `terrains/` directory:

```
terrains/{terrainType}-{variant}.png
```

- **terrainType**: Lowercase terrain identifier (lightwoods, heavywoods, rough, road, pavement, bridge etc.)
- **variant**: Optional numeric suffix (1-indexed)

Examples:
- `terrains/lightwoods-1.png` - Light woods overlay, variant 1
- `terrains/heavywoods-2.png` - Heavy woods overlay, variant 2
- `terrains/rough.png` - Rough terrain overlay, variant 0
- `terrains/road.png` - Road overlay, variant 0
- `terrains/pavement.png` - Pavement overlay, variant 0
- `terrains/bridge.png` - Bridge overlay, variant 0

### Bitmask Road Textures

Road and bridge terrain types use a 6-bit neighbor bitmask system (same mechanism as water) for seamless road-network rendering. Bitmask textures are placed in the `terrains/road/` subdirectory:

```
terrains/road/{bitmask}.png
```

- **bitmask**: 6-character binary string representing which neighboring hexes contain road or bridge terrain
  - Each character position corresponds to a hex direction (0–5)
  - `1` = connected, `0` = not connected
  - Canonical bitmask after rotation normalization (13 canonical patterns)

Examples:
- `terrains/road/000001.png` - Road connects to neighbor in direction 0 only
- `terrains/road/000011.png` - Road connects to neighbors in directions 0 and 1
- `terrains/road/111111.png` - Road connects to all 6 neighbors

Rotation is applied at render time based on the canonical bitmask's rotation steps (same as water bitmask textures).

### Edge Effects

Edge effects represent terrain transitions at hex boundaries:

```
edges/{type}-{direction}-{variant}.png
```

- **type**: Either `top` or `bottom`
  - `top`: Cliff dropping away from viewer (hex edge is lower)
  - `bottom`: Cliff rising toward viewer (hex edge is higher)
- **direction**: Hex edge direction (0-5, matching HexDirection enum)
  - 0 = Top
  - 1 = TopRight
  - 2 = BottomRight
  - 3 = Bottom
  - 4 = BottomLeft
  - 5 = TopLeft
- **variant**: Optional numeric suffix (1-indexed)

Examples:
- `edges/top-0-1.png` - Top edge at direction 0, variant 1
- `edges/bottom-3-2.png` - Bottom edge at direction 3, variant 2
- `edges/top-5.png` - Top edge at direction 5, variant 0

## Image Requirements

### Format

- **File format**: PNG with alpha channel
- **Color space**: sRGB
- **Transparency**: Required for overlays and edges

### Dimensions

Images should match the hex geometry defined by the application:

- **Base terrain**: Full hex dimensions
- **Overlays**: Full hex dimensions with transparent areas
- **Edge effects**: Width matching hex edge, appropriate height for effect

### Visual Guidelines

1. **Base terrain**: Should tile seamlessly
2. **Overlays**: Transparent background, only terrain features visible
3. **Edge effects**: 
   - Top edges: Show the edge of the current hex dropping away
   - Bottom edges: Show the rising terrain from adjacent hex

## Variant System

### Purpose

Variants provide visual variety while maintaining deterministic rendering:

- Multiple variants of the same terrain type can be provided
- The engine selects variants based on hex coordinates
- Same hex always renders the same variant (deterministic)

### Variant Numbering

- Variants are 1-indexed in filenames but 0-indexed internally
- `lightwoods.png` → variant 0
- `lightwoods-1.png` → variant 1
- `lightwoods-2.png` → variant 2
- `lightwoods-3.png` → variant 3

### Selection Algorithm

The engine uses SHA256 hash-based selection:

```csharp
var combined = $"{themeId}-{assetName}-{seed}";
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
var hashValue = BitConverter.ToUInt32(hash, 0);
var index = hashValue % variants.Count;
```

For edge assets, the seed is derived from hex coordinates:
```csharp
var seed = coordinates.Q + coordinates.R * 31;
```

## Example Package

A minimal MMTX package:

```
classic.mmtx
├── manifest.json
├── base.png
├── terrains/
│   ├── lightwoods.png
│   ├── heavywoods.png
│   ├── rough.png
│   ├── road.png
│   ├── pavement.png
│   ├── bridge.png
│   ├── water.png
│   └── road/
│       ├── 000001.png
│       ├── 000010.png
│       ├── 000011.png
│       ├── 000100.png
│       ├── 000101.png
│       ├── 000110.png
│       ├── 000111.png
│       ├── 001001.png
│       ├── 001010.png
│       ├── 001011.png
│       ├── 010010.png
│       ├── 010101.png
│       └── 111111.png
└── edges/
    ├── top-0.png
    ├── top-1.png
    ├── top-2.png
    ├── top-3.png
    ├── top-4.png
    ├── top-5.png
    ├── bottom-0.png
    ├── bottom-1.png
    ├── bottom-2.png
    ├── bottom-3.png
    ├── bottom-4.png
    └── bottom-5.png
```

## Integration with MakaMek

### Loading Process

1. `TerrainCachingService` receives MMTX stream from `IResourceStreamProvider`
2. ZIP archive is extracted
3. `manifest.json` is parsed for theme metadata
4. Images are cached with keys: `{id}/{assetType}/{assetName}/{variant}`
5. Variant availability is tracked per asset type

### Service Interface

```csharp
// Get base terrain with random variant
var baseImage = await terrainService.GetBaseTerrainImage("classic");

// Get specific overlay variant
var overlay = await terrainService.GetTerrainOverlayImage("classic", "lightwoods", variant: 0);

// Get edge with coordinate-based variant selection
var edge = await terrainService.GetEdgeImage("classic", HexDirection.Top, 
    TerrainAssetType.EdgeTop, coordinates);
```

## Error Handling

The parser handles malformed packages gracefully:

- Missing `manifest.json`: Package is skipped, logged as warning
- Missing `id` in manifest: Package is skipped
- Invalid image files: Individual images are skipped, others loaded
- Missing directories: Optional directories (terrains/, edges/) can be omitted

