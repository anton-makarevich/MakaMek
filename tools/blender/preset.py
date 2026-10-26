"""Load and validate the render preset for the Blender sprite driver.

The preset is the only description of the *look*. It is validated strictly: an unknown key is an
error, because a typo in a preset must never silently render the default scene
(see docs/architecture/blender-render-driver.md, section 4).
"""

import json

# Values are one of:
#   (type, ...)  - leaf, must be an instance of one of the given types (bool is not a number)
#   {str: spec}  - object with exactly these keys
#   [spec]       - list whose items match spec
_SCHEMA = {
    "name": (str,),
    "camera": {
        "projection": (str,),
        "pitchDegrees": (int, float),
        "yawDegrees": (int, float),
    },
    "scale": {
        "mode": (str,),
        "referenceChassis": (str,),
        "referenceCoverage": (int, float),
        "minimumCoverage": (int, float),
        "drawBoxHeightPx": (int, float),
        "hexAreaPx": (int, float),
    },
    "output": {
        "cellWidth": (int,),
        "cellHeight": (int,),
        "channels": (str,),
        "background": (str,),
        "viewTransform": (str,),
    },
    "engine": {
        "name": (str,),
        "device": (str,),
        "samples": (int,),
        "denoise": (str,),
    },
    "lights": [
        {
            "type": (str,),
            "position": [(int, float)],
            "size": (int, float),
            "color": (str,),
            "intensity": (int, float),
        }
    ],
    "styling": {
        "materialOverride": (str,),
        "outline": {
            "enabled": (bool,),
            "width": (int, float),
            "color": (str,),
            "crease": (bool,),
        },
        "posterize": (int,),
    },
    "shading": {
        "groundShadow": {
            "enabled": (bool,),
            "opacity": (int, float),
            "blurPx": (int, float),
        }
    },
    "clips": {
        "*": {
            "frames": (int,),
            "fps": (int,),
            "row": (int,),
        }
    },
    "atlas": {
        "layout": (str,),
        "padding": (int,),
        "powerOfTwo": (bool,),
    },
}

# Keys the driver dereferences unconditionally. Missing ones are a hard error rather than a default,
# so a trimmed preset cannot silently produce a different look.
_REQUIRED = (
    "name",
    "camera",
    "output",
    "engine",
    "lights",
    "styling",
    "shading",
    "clips",
)

# Keys read below the top level, as dotted paths. `_check` catches unknown keys and wrong types; this
# catches omissions, which would otherwise surface as a KeyError traceback after the scene is built.
_REQUIRED_NESTED = (
    "camera.projection",
    "camera.pitchDegrees",
    "camera.yawDegrees",
    "output.cellWidth",
    "output.cellHeight",
    "output.channels",
    "output.background",
    "output.viewTransform",
    "engine.name",
    "engine.device",
    "engine.samples",
    "engine.denoise",
    "styling.materialOverride",
    "styling.outline.enabled",
    "styling.outline.width",
    "styling.outline.color",
    "shading.groundShadow.enabled",
)

_LIGHT_KEYS = ("type", "position", "size", "color", "intensity")


class PresetError(ValueError):
    """Raised when a preset is unreadable or invalid."""


def _matches(value, types):
    if isinstance(value, bool):
        return bool in types
    return isinstance(value, types)


def _check(node, schema, path):
    where = path or "<root>"

    if isinstance(schema, dict):
        if not isinstance(node, dict):
            raise PresetError(f"{where}: expected an object, got {type(node).__name__}")
        wildcard = schema.get("*")
        for key, value in node.items():
            child = schema.get(key, wildcard)
            if child is None:
                raise PresetError(f'{where}: unknown key "{key}"')
            _check(value, child, f"{path}.{key}" if path else key)
        return

    if isinstance(schema, list):
        if not isinstance(node, list):
            raise PresetError(f"{where}: expected a list, got {type(node).__name__}")
        for index, item in enumerate(node):
            _check(item, schema[0], f"{path}[{index}]")
        return

    if not _matches(node, schema):
        expected = " or ".join(t.__name__ for t in schema)
        raise PresetError(f"{where}: expected {expected}, got {type(node).__name__}")


def load(path):
    """Read, parse and validate a preset file. Returns the preset as a plain dict."""
    try:
        with open(path, "r", encoding="utf-8") as handle:
            preset = json.load(handle)
    except FileNotFoundError as error:
        raise PresetError(f"preset not found: {path}") from error
    except json.JSONDecodeError as error:
        raise PresetError(f"preset is not valid JSON: {path}: {error}") from error

    if not isinstance(preset, dict):
        raise PresetError(f"{path}: expected a JSON object at the root")

    _check(preset, _SCHEMA, "")

    missing = [key for key in _REQUIRED if key not in preset]
    if missing:
        raise PresetError(f'{path}: missing required key(s): {", ".join(missing)}')

    for dotted in _REQUIRED_NESTED:
        node = preset
        for part in dotted.split("."):
            if not isinstance(node, dict) or part not in node:
                raise PresetError(f"{path}: missing required key: {dotted}")
            node = node[part]

    if not preset["lights"]:
        raise PresetError(f"{path}: lights must not be empty")

    for index, light in enumerate(preset["lights"]):
        missing_light = [key for key in _LIGHT_KEYS if key not in light]
        if missing_light:
            raise PresetError(
                f'{path}: lights[{index}] is missing required key(s): {", ".join(missing_light)}'
            )
        if len(light["position"]) != 3:
            raise PresetError(f"{path}: lights[{index}].position must have exactly 3 values")

    if not preset["clips"]:
        raise PresetError(f"{path}: clips must not be empty")

    for clip, spec in preset["clips"].items():
        if "frames" not in spec:
            raise PresetError(f"{path}: clips.{clip}.frames is required")

    if preset["camera"]["projection"] != "ortho":
        raise PresetError(
            f'{path}: camera.projection must be "ortho" (the board sprite is an orthographic plan '
            f'view); got "{preset["camera"]["projection"]}"'
        )

    if preset["output"]["channels"] != "luminance":
        raise PresetError(
            f'{path}: output.channels must be "luminance" (sprites are tinted at runtime); got '
            f'"{preset["output"]["channels"]}"'
        )

    for clip, spec in preset["clips"].items():
        if spec["frames"] < 1:
            raise PresetError(f'{path}: clips.{clip}.frames must be >= 1')

    return preset
