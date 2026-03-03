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
│   └── ...
└── edges/                  # Optional: Edge effect images
    ├── top-{direction}-{variant}.png
    └── bottom-{direction}-{variant}.png
```

## manifest.json Schema

```json
{
  "themeId": "makamek.themes.grasslands",
  "name": "Grasslands",
  "version": "1.0.0",
  "requiredMakaMekVersion": "0.53.6",
  "description": "Grasslands terrain style",
  "author": "MakaMek"
}
```

### Required Fields

- **themeId**: Unique identifier for the theme (alphanumeric, dashes allowed)
- **name**: Human-readable display name
- **version**: Semantic version (major.minor.patch)
- **requiredMakaMekVersion**: Minimum MakaMek version compatibility

### Optional Fields

- **description**: Theme description
- **author**: Creator attribution

## Asset Naming Conventions

### Base Terrain

Base terrain images represent the underlying hex background:

```
base-{variant}.png
```

- Located in root directory
- **variant**: Optional numeric suffix (1-indexed)
  - `base-1.png` → variant 0
  - `base-2.png` → variant 1
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

- **terrainType**: Lowercase terrain identifier (lightwoods, heavywoods, rough etc.)
- **variant**: Optional numeric suffix (1-indexed)

Examples:
- `terrains/lightwoods-1.png` - Light woods overlay, variant 0
- `terrains/heavywoods-2.png` - Heavy woods overlay, variant 1
- `terrains/rough.png` - Rough terrain overlay, variant 0

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
- `edges/top-0-1.png` - Top edge at direction 0, variant 0
- `edges/bottom-3-2.png` - Bottom edge at direction 3, variant 1
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
- `lightwoods-1.png` → variant 0
- `lightwoods-2.png` → variant 1
- `lightwoods-3.png` → variant 2

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
├── base-1.png
├── base-2.png
├── terrains/
│   ├── lightwoods-1.png
│   ├── lightwoods-2.png
│   ├── heavywoods-1.png
│   └── rough-1.png
└── edges/
    ├── top-0-1.png
    ├── top-1-1.png
    ├── top-2-1.png
    ├── top-3-1.png
    ├── top-4-1.png
    ├── top-5-1.png
    ├── bottom-0-1.png
    ├── bottom-1-1.png
    ├── bottom-2-1.png
    ├── bottom-3-1.png
    ├── bottom-4-1.png
    └── bottom-5-1.png
```

## Integration with MakaMek

### Loading Process

1. `TerrainCachingService` receives MMTX stream from `IResourceStreamProvider`
2. ZIP archive is extracted
3. `manifest.json` is parsed for theme metadata
4. Images are cached with keys: `{themeId}/{assetType}/{assetName}/{variant}`
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
- Missing `themeId` in manifest: Package is skipped
- Invalid image files: Individual images are skipped, others loaded
- Missing directories: Optional directories (terrains/, edges/) can be omitted

## Comparison with MMUX

| Feature | MMUX (Units) | MMTX (Terrain) |
|---------|--------------|----------------|
| Extension | `.mmux` | `.mmtx` |
| Manifest | `unit.json` | `manifest.json` |
| Images | `unit.png` (single) | Multiple with variants |
| Variants | Not supported | Fully supported |
| Directory structure | Flat | Hierarchical |
| Key identifier | Model name | Theme ID |
