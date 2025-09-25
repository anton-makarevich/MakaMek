# MmuxPackager

A CLI tool for packaging MakaMek unit data into distributable .mmux files (which are renamed ZIP archives).

## Overview

The MmuxPackager tool creates .mmux packages that combine serialized unit data, images, and metadata into a single distributable file. Each .mmux file is essentially a ZIP archive with a specific structure.

## Package Structure

Each .mmux file contains:
```
Model.mmux (ZIP archive with .mmux extension)
├── unit.json          # Serialized UnitData 
├── unit.png           # Unit image file
└── manifest.json      # Package metadata
```

## Manifest Format

The manifest.json file contains metadata about the package:
```json
{
  "version": "1.0",
  "unitId": "{Model}",
  "author": "FASA", 
  "source": "TRO-3025",  
  "requiredMakaMekVersion": "0.43.2"
}
```

## Usage

```bash
MmuxPackager --data-source <path> --image-source <path> --output <path>
```

### Parameters

- `--data-source` or `-d`: Path to folder containing unit JSON files
- `--image-source` or `-i`: Path to folder containing unit PNG images  
- `--output` or `-o`: Path to output folder for generated .mmux files

### Example

```bash
MmuxPackager -d "C:\Units\Json" -i "C:\Units\Images" -o "C:\Output"
```

## Image File Matching

The tool uses a priority system to find corresponding image files:

1. **Exact match**: `{Name}_{Model}.png` (e.g., "Atlas_AS7-D.png")
2. **Name match**: `{Name}.png` (e.g., "Atlas.png")  
3. **Prefix match**: Any PNG file starting with `{Name}` (e.g., "Atlas_variant.png")

Where `{Name}` is derived as `{Chassis} {Model}` from the unit data.

## Processing Logic

For each unit JSON file:
1. Parse the JSON to extract unit `Name` and `Model` properties
2. Search for corresponding image file using the priority system
3. If no image found: print error message and continue to next file
4. If image found: generate manifest.json with appropriate metadata
5. Create ZIP archive containing unit.json, unit.png, and manifest.json
6. Rename ZIP file to have .mmux extension
7. Save as `{Model}.mmux` in the output folder

## Error Handling

The tool provides comprehensive error handling for:
- Missing or inaccessible directories
- Invalid JSON files
- Missing image files
- File system permission errors
- Invalid characters in model names

## Exit Codes

- `0`: Success
- `1`: General error
- `2`: Invalid argument
- `3`: File/directory not found
