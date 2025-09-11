# MTF to JSON Converter

A command-line tool for converting MTF files to MakaMek JSON data format using the MakaMek.Core library.

## Usage

```bash
MtfConverter -i <input> -o <output>
```

### Parameters

- `-i, --input` (required): Path to the source MTF file or directory containing MTF files
- `-o, --output` (required): Directory where the converted JSON files should be saved

### Examples

Convert a single MTF file:
```bash
MtfConverter -i "LCT-1V.mtf" -o "output"
```

Convert all MTF files in a directory:
```bash
MtfConverter -i "mechs" -o "json_output"
```

## Output Format

The tool converts MTF files to JSON format with the following features:

- **Consistent camelCase naming**: All JSON properties use camelCase convention
- **String component names**: Equipment components are represented as strings (e.g., "MachineGun") instead of numeric enum values
- **Structured data**: Armor values, location equipment, quirks, and additional attributes are properly structured
- **Pretty-printed JSON**: Output is formatted with indentation for readability

## Example Output

```json
{
  "id": null,
  "chassis": "Locust",
  "model": "LCT-1V",
  "mass": 20,
  "walkMp": 8,
  "engineRating": 160,
  "engineType": "Fusion",
  "armorValues": {
    "leftArm": {
      "frontArmor": 4,
      "rearArmor": 0
    }
  },
  "locationEquipment": {
    "leftArm": [
      "Shoulder",
      "UpperArmActuator",
      "MachineGun"
    ]
  },
  "quirks": {
    "quirk1": "compact_mech"
  }
}
```

## Error Handling

The tool provides comprehensive error handling for:

- Non-existent input files or directories
- Invalid MTF file format
- File I/O errors
- Missing command-line arguments

When processing multiple files, the tool will continue processing remaining files even if some fail, and report a summary of successes and errors.
