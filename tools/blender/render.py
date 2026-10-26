"""Headless Blender driver: canonical mech model -> 84x72 top-down board sprite frames.

This is the only supported entry point (docs/architecture/blender-render-driver.md, section 2):

    blender -b --factory-startup -noaudio --python-exit-code 1 \
            -P tools/blender/render.py -- \
            --model  <model.glb> \
            --preset tools/blender/board-topdown.json \
            --scale  <effectiveScale> \
            --out    <output directory> \
            [--clips Idle,Walk] [--report render-report.json]

The driver is a renderer, not a policy engine: it never reads manifests, tonnage, overrides or
coverage thresholds. `--scale` is the only scale input, and it is in pixel space - after it,
1 model unit == 1 pixel.

Exit codes: 0 ok, 2 bad arguments, 3 missing input, 4 invalid preset / failed verification.
"""

import argparse
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import preset as preset_module  # noqa: E402
import scene as scene_module  # noqa: E402

# Blender major version the scene contract was written against. A mismatch is refused rather than
# rendered with a different default scene.
PINNED_BLENDER_MAJOR = 5

EXIT_OK = 0
EXIT_BAD_ARGS = 2
EXIT_MISSING_INPUT = 3
EXIT_INVALID = 4

_DEFAULT_CLIPS = "Idle"


def parse_args(argv):
    """Parse the driver's command line (everything after Blender's `--`)."""
    parser = argparse.ArgumentParser(
        prog="render.py",
        description="Render top-down board sprites from a glTF/GLB model with headless Blender.",
    )
    parser.add_argument("--model", required=True, help="model to render (.glb or .gltf)")
    parser.add_argument("--preset", required=True, help="render preset (JSON)")
    parser.add_argument("--scale", required=True, type=float,
                        help="effective scale in pixel space (1 model unit -> N pixels)")
    parser.add_argument("--out", required=True, help="output directory for the frames")
    parser.add_argument("--clips", default=_DEFAULT_CLIPS,
                        help=f"comma separated clip names from the preset (default: {_DEFAULT_CLIPS})")
    parser.add_argument("--report", default=None, help="write a JSON render report here")
    parser.add_argument("--samples", default=None, type=int, help="override the preset sample count")
    parser.add_argument("--resolution", default=None, type=int,
                        help="override the cell size with an NxN square (local inspection only)")
    parser.add_argument("--supersample", default=1, type=int,
                        help="render N x the cell size so the pipeline can downscale it to 1x "
                             "(--scale is still given in 1x pixel space)")
    parser.add_argument("--engine", default="cycles", choices=["cycles", "eevee"],
                        help="cycles (CI/reproducible) or eevee (local iteration only)")
    parser.add_argument("--lighting", default=None, choices=["lights", "ambient", "flat"],
                        help="override preset lighting.mode: 'lights' = camera lamp rig, "
                             "'ambient' = uniform world light (no cast shadows), 'flat' = emissive "
                             "material with no lighting at all (silhouette + outline only)")
    parser.add_argument("--device", default="cpu", choices=["cpu", "gpu"],
                        help="cycles device; cpu is the reproducible path")
    parser.add_argument("--frame", default=None, type=int, help="render a single frame per clip")
    parser.add_argument("--fit", action="store_true",
                        help="size the cell to this model's plan-view projection (+2 px margin at 1x) "
                             "instead of the preset's fixed cell - every unit gets its own canvas and is "
                             "meant to be drawn 1:1, centred in the hex")
    parser.add_argument("--no-outline", action="store_true", help="disable the Freestyle outline")
    parser.add_argument("--save-blend", default=None, help="save the generated scene for inspection")
    parser.add_argument("--model-yaw", default=0.0, type=float,
                        help="spike-only: extra yaw for source exports that are not yet canonical "
                             "(+Z forward). Canonical models need 0.")
    return parser.parse_args(argv)


def frame_numbers(preset, clips, single_frame=None):
    """Absolute frame numbers the render loop will visit, de-duplicated and in order.

    `--fit` measures exactly these frames, so the fitted canvas cannot disagree with what is actually
    rendered.
    """
    numbers = []
    for clip in clips:
        if single_frame is not None:
            count, first = 1, single_frame
        else:
            count, first = int(preset["clips"][clip]["frames"]), 1
        for offset in range(count):
            frame = first + offset
            if frame not in numbers:
                numbers.append(frame)
    return numbers


def main(argv):
    """Build the scene from arguments + preset, render, verify the frames and print the report."""
    args = parse_args(argv)

    if bpy.app.version[0] != PINNED_BLENDER_MAJOR:
        raise SystemExit(
            f"blender {bpy.app.version_string}: this driver targets Blender "
            f"{PINNED_BLENDER_MAJOR}.x; refusing to render with a different major version"
        )

    if not os.path.isfile(args.model):
        print(f"model not found: {args.model}", file=sys.stderr)
        return EXIT_MISSING_INPUT

    try:
        # The override is applied inside `load` so mode-dependent validation (a `lights` render needs
        # a non-empty rig) sees the mode the run will actually use.
        preset = preset_module.load(args.preset, lighting=args.lighting)
    except preset_module.PresetError as error:
        print(f"invalid preset: {error}", file=sys.stderr)
        return EXIT_INVALID

    clips = [name.strip() for name in args.clips.split(",") if name.strip()]
    unknown = [name for name in clips if name not in preset["clips"]]
    if unknown:
        print(f"preset has no clip(s) {', '.join(unknown)}; known: {', '.join(preset['clips'])}",
              file=sys.stderr)
        return EXIT_INVALID

    if args.resolution and args.fit:
        print("--resolution and --fit are mutually exclusive", file=sys.stderr)
        return EXIT_BAD_ARGS

    if args.resolution:
        preset["output"]["cellWidth"] = args.resolution
        preset["output"]["cellHeight"] = args.resolution

    if args.scale <= 0:
        print(f"--scale must be positive, got {args.scale}", file=sys.stderr)
        return EXIT_BAD_ARGS

    supersample = max(1, args.supersample)
    render_scale = args.scale * supersample
    preset["output"]["cellWidth"] *= supersample
    preset["output"]["cellHeight"] *= supersample
    # The rig is expressed in pixels, so the whole lighting setup scales with the cell (inverse-square
    # for power) - otherwise a supersampled render would be lit by a rig that sits inside the model.
    for light in preset["lights"]:
        light["position"] = [value * supersample for value in light["position"]]
        light["size"] *= supersample
        light["intensity"] *= supersample * supersample
    # Freestyle thickness is in pixels, so it has to scale with the render or the outline disappears
    # when the pipeline downsamples back to 1x.
    preset["styling"]["outline"]["width"] *= supersample

    scene_module.reset()
    imported = scene_module.import_model(args.model)
    _, metrics = scene_module.normalise(imported, render_scale, args.model_yaw)

    if args.fit:
        # Per-unit canvas: the projection plus a small margin so the 1 px outline is not clipped. At
        # 1x this is exactly the mech's own size, so the pipeline can draw it 1:1 and centred. The cell
        # is rounded up to a multiple of the supersample factor so the downsampled 1x size is integral.
        margin = 2 * supersample

        def _fit(value):
            return int(math.ceil((value + 2 * margin) / supersample)) * supersample

        # Measured over every frame this run renders, not just the pose `normalise` saw, so a limb
        # that swings past the rest footprint cannot be clipped by its own canvas. The camera keeps
        # looking at the world origin (the sprite is drawn centred in the hex), so the canvas must
        # cover the largest extent on *either* side of the origin, not just the union's width.
        minimum, maximum = scene_module.animated_bounds(
            imported, frame_numbers(preset, clips, args.frame))
        preset["output"]["cellWidth"] = _fit(2 * max(-minimum.x, maximum.x))
        preset["output"]["cellHeight"] = _fit(2 * max(-minimum.y, maximum.y))

    scene_module.override_materials(imported, preset["lighting"]["mode"])
    camera = scene_module.add_camera(preset)
    scene_module.setup_lighting(preset, camera)

    if args.no_outline:
        preset["styling"]["outline"]["enabled"] = False
    scene_module.setup_freestyle(preset)
    scene_module.setup_render(preset, engine=args.engine, samples=args.samples, device=args.device)

    if args.save_blend:
        os.makedirs(os.path.dirname(os.path.abspath(args.save_blend)), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args.save_blend))

    scene = bpy.context.scene
    out_dir = os.path.abspath(args.out)
    expected = []
    for clip in clips:
        spec = preset["clips"][clip]
        frame_count = 1 if args.frame is not None else int(spec["frames"])
        first = args.frame if args.frame is not None else 1
        clip_dir = os.path.join(out_dir, "frames", clip)
        os.makedirs(clip_dir, exist_ok=True)
        for offset in range(frame_count):
            frame = first + offset
            path = os.path.join(clip_dir, f"{frame:03d}.png")
            # Remove the previous frame first: verification must only be able to pass on output this
            # run produced, never on a stale file from an earlier render.
            if os.path.exists(path):
                os.remove(path)
            scene.frame_set(frame)
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            expected.append(path)
            print(f"rendered {path}", file=sys.stderr)

    # Blender can exit 0 with partial output, so verify every expected frame on disk.
    missing = [path for path in expected if not os.path.isfile(path) or os.path.getsize(path) == 0]
    if missing:
        print("missing output frames:\n  " + "\n  ".join(missing), file=sys.stderr)
        return EXIT_INVALID

    report = {
        "appliedScale": args.scale,
        "supersample": supersample,
        "renderScale": render_scale,
        "preset": preset["name"],
        "model": os.path.abspath(args.model),
        "blender": bpy.app.version_string,
        "engine": scene.render.engine,
        "device": scene.cycles.device if scene.render.engine == "CYCLES" else "n/a",
        "samples": scene.cycles.samples if scene.render.engine == "CYCLES" else "n/a",
        "lighting": preset["lighting"]["mode"],
        "cellWidth": scene.render.resolution_x,
        "cellHeight": scene.render.resolution_y,
        "outline": bool(scene.render.use_freestyle),
        "clips": clips,
        "frames": [os.path.abspath(path) for path in expected],
        "metrics": metrics,
    }

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8") as handle:
            json.dump(report, handle, indent=2)
            handle.write("\n")

    print(json.dumps(report))
    return EXIT_OK


if __name__ == "__main__":
    if "--" in sys.argv:
        script_args = sys.argv[sys.argv.index("--") + 1:]
    else:
        script_args = []
    try:
        sys.exit(main(script_args))
    except preset_module.PresetError as error:
        print(f"invalid preset: {error}", file=sys.stderr)
        sys.exit(EXIT_INVALID)
    except SystemExit:
        raise
    except Exception as error:  # Blender's own --python-exit-code already fails the run
        print(f"render failed: {error}", file=sys.stderr)
        raise
