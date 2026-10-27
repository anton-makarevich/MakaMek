"""Assemble rendered frames into a sprite sheet, a labelled contact sheet, clip metadata and GIF previews.

This is stage-2 post-processing, deliberately separate from the Blender driver: Pillow does labelling and
atlas packing better than Blender's image API, and the artefacts are review documents, not renders.

    python tools/blender/sheet.py \
        --frames artifacts/mw5/anim/AS7-D/frames \
        --report artifacts/mw5/anim/AS7-D/report.json \
        --assembly artifacts/mw5/assemble/AS7-D/AS7-D.animated.assembly.json \
        --preset tools/blender/board-topdown.json \
        --out artifacts/mw5/anim/AS7-D \
        --frames-out artifacts/mw5/anim/AS7-D/frames-1x \
        --name AS7-D

Outputs (in --out): unit-sprites.png, unit-sprites.json, contact-sheet.png, clips.csv, clips.json and
preview-<clip>.gif. Requires Pillow (`pip install pillow`); exit codes match the Blender driver.
"""

import argparse
import csv
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

EXIT_OK = 0
EXIT_BAD_ARGS = 2
EXIT_MISSING_INPUT = 3
EXIT_INVALID = 4

PREVIEW_SCALE = 4
CONTACT_SCALE = 2
PADDING = 2
BACKGROUNDS = [
    ("neutral", (208, 208, 208, 255)),
    ("hex", (92, 122, 74, 255)),
]


def parse_args(argv):
    parser = argparse.ArgumentParser(prog="sheet.py", description="Build sprite sheets from rendered frames.")
    parser.add_argument("--frames", required=True, help="directory containing <clip>/<frame:03>.png")
    parser.add_argument("--out", required=True, help="output directory")
    parser.add_argument("--preset", required=True, help="render preset (clip fps/loop/hold)")
    parser.add_argument("--report", default=None, help="driver render report (source frames per clip)")
    parser.add_argument("--assembly", default=None, help="extractor assembly JSON (source clip assets)")
    parser.add_argument("--name", default=None, help="label for the sheets (default: frames' parent name)")
    parser.add_argument("--padding", default=PADDING, type=int, help="atlas gutter in pixels")
    parser.add_argument("--downsample", default="auto",
                        help="downsample factor before packing: 'auto' uses the report's supersample, "
                             "'1' keeps the rendered size; the shipped sheet is 1x")
    parser.add_argument("--frames-out", default=None,
                        help="optional directory to write the downsampled 1x frames (<clip>/<index:03>.png); "
                             "must not be the same directory as --frames")
    return parser.parse_args(argv)


def load_json(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:  # Pillow < 10.1
        return ImageFont.load_default()


def clip_directories(root, preset):
    if not os.path.isdir(root):
        raise FileNotFoundError(root)

    clips = []
    for name in sorted(os.listdir(root)):
        path = os.path.join(root, name)
        if not os.path.isdir(path):
            continue
        frames = sorted(file for file in os.listdir(path) if file.lower().endswith(".png"))
        if frames:
            clips.append((name, [os.path.join(path, frame) for frame in frames]))

    # Atlas rows follow the preset's `row`, so the sheet layout is stable and matches unit-sprites.json.
    clips.sort(key=lambda clip: ((preset["clips"].get(clip[0]) or {}).get("row", 999), clip[0]))
    return clips


def load_frames(clips, downsample=1):
    loaded = []
    for name, paths in clips:
        images = [Image.open(path).convert("RGBA") for path in paths]
        if downsample > 1:
            images = [downsample_image(image, downsample) for image in images]
        loaded.append((name, images))
    return loaded


def downsample_image(image, factor):
    """Box-downsample a supersampled frame, matching the pipeline's 4x -> 1x contract."""
    width = max(1, image.width // factor)
    height = max(1, image.height // factor)
    return image.resize((width, height), Image.BOX)


def cell_size(loaded):
    width = max(image.width for _, images in loaded for image in images)
    height = max(image.height for _, images in loaded for image in images)
    return width, height


def next_power_of_two(value):
    return 1 if value <= 1 else 2 ** math.ceil(math.log2(value))


def build_atlas(loaded, width, height, padding):
    rows = max(1, len(loaded))
    columns = max(1, max(len(images) for _, images in loaded))
    raw_width = columns * width + (columns + 1) * padding
    raw_height = rows * height + (rows + 1) * padding
    atlas = Image.new("RGBA", (next_power_of_two(raw_width), next_power_of_two(raw_height)), (0, 0, 0, 0))

    placements = {}
    for row, (name, images) in enumerate(loaded):
        placements[name] = []
        for column, image in enumerate(images):
            x = padding + column * (width + padding)
            y = padding + row * (height + padding)
            # Centre the frame in its cell so a per-clip canvas of a different size stays aligned.
            x += (width - image.width) // 2
            y += (height - image.height) // 2
            atlas.alpha_composite(image, (x, y))
            placements[name].append([column, row])

    return atlas, placements, raw_width, raw_height


def draw_hex(draw, box, colour):
    """A 100 x 86.6 board hexagon inside `box` (review-only grounding for the contact sheet)."""
    x0, y0, x1, y1 = box
    width = x1 - x0
    height = y1 - y0
    centre_x = (x0 + x1) / 2
    centre_y = (y0 + y1) / 2
    radius = min(width / 2, height / 2)
    points = []
    for index in range(6):
        angle = math.radians(60 * index - 30)
        points.append((centre_x + radius * math.cos(angle), centre_y + radius * math.sin(angle)))
    draw.polygon(points, outline=colour, width=max(1, round(radius / 40)))


def build_contact_sheet(loaded, preset, report, name):
    width, height = cell_size(loaded)
    scale = CONTACT_SCALE
    cell_w = width * scale
    cell_h = height * scale
    label_h = 22
    gap = 6
    header_h = 30
    title_h = 32

    columns = max(len(images) for _, images in loaded)
    rows = len(loaded)
    panel_w = 150 + columns * (cell_w + gap)
    panel_h = header_h + rows * (cell_h + label_h + gap)
    sheet_w = panel_w
    sheet_h = title_h + panel_h * len(BACKGROUNDS)

    sheet = Image.new("RGBA", (sheet_w, sheet_h), (28, 28, 28, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = font(16)
    label_font = font(12)
    draw.text((8, 8), f"{name} — contact sheet ({scale}x, 1x cell {width}x{height})", fill=(240, 240, 240), font=title_font)

    for panel_index, (background_name, colour) in enumerate(BACKGROUNDS):
        panel_x = 0
        panel_y = title_h + panel_index * panel_h
        draw.rectangle([panel_x, panel_y, panel_x + panel_w - gap, panel_y + panel_h], fill=colour)
        draw.text((panel_x + 8, panel_y + 6), background_name, fill=(255, 255, 255), font=title_font)

        for row, (clip_name, images) in enumerate(loaded):
            row_y = panel_y + header_h + row * (cell_h + label_h + gap)
            draw.text((panel_x + 8, row_y + 4), clip_name, fill=(255, 255, 255), font=title_font)
            spec = preset["clips"].get(clip_name, {})
            draw.text(
                (panel_x + 8, row_y + 24),
                f"{spec.get('fps', '?')} fps {spec.get('frames', len(images))}f",
                fill=(230, 230, 230),
                font=label_font,
            )

            for column, image in enumerate(images):
                x = panel_x + 150 + column * (cell_w + gap)
                y = row_y
                cell = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))
                if background_name == "hex":
                    hex_draw = ImageDraw.Draw(cell)
                    draw_hex(hex_draw, (0, 0, cell_w - 1, cell_h - 1), (220, 230, 210, 160))
                cell.alpha_composite(image.resize((cell_w, cell_h), Image.NEAREST), (0, 0))
                sheet.alpha_composite(cell, (x, y))

                source = ""
                frames = (report or {}).get("animations", [])
                for entry in frames:
                    if entry.get("clip") == clip_name and column < len(entry.get("sourceFrames", [])):
                        source = f" src {entry['sourceFrames'][column]:.1f}"
                draw.text((x, y + cell_h + 3), f"f{column + 1:02d}{source}", fill=(255, 255, 255), font=label_font)

    return sheet


def build_preview(images, path, fps):
    scaled = [image.resize((image.width * PREVIEW_SCALE, image.height * PREVIEW_SCALE), Image.NEAREST)
              for image in images]
    duration = max(10, round(1000 / max(1, fps)))
    scaled[0].save(path, save_all=True, append_images=scaled[1:], duration=duration, loop=0, disposal=2)


def write_metadata(out_dir, name, loaded, width, height, padding, preset, report, assembly):
    source_clips = {}
    if assembly:
        for entry in assembly.get("animations", []):
            source_clips[entry.get("name")] = entry

    rendered = {}
    if report:
        for entry in report.get("animations", []):
            rendered[entry.get("clip")] = entry

    animations = {}
    rows = []
    for row, (clip_name, images) in enumerate(loaded):
        spec = dict(preset["clips"].get(clip_name, {}))
        source = source_clips.get(clip_name, {})
        render = rendered.get(clip_name, {})
        clip = {
            "row": row,
            "frames": len(images),
            "fps": spec.get("fps"),
            "loop": spec.get("loop", True),
            "hold": spec.get("hold"),
        }
        if source:
            clip["source"] = {
                "asset": source.get("assetPath"),
                "skeleton": source.get("skeleton"),
                "fps": source.get("fps"),
                "frames": source.get("frames"),
                "sampledFrames": source.get("sampledFrames"),
                "durationSeconds": source.get("durationSeconds"),
                "rootMotionMeters": source.get("rootMotionMeters"),
                "loopClosureDegrees": source.get("loopClosureDegrees"),
                "loopClosureMeters": source.get("loopClosureMeters"),
            }
        if render:
            clip["sourceFrames"] = render.get("sourceFrames")
            clip["sourceRange"] = render.get("sourceRange")

        animations[clip_name] = clip
        source_range = render.get("sourceRange") or [None, None]
        rows.append({
            "name": clip_name,
            "asset": source.get("assetPath"),
            "sourceFrames": source.get("frames"),
            "sourceFps": source.get("fps"),
            "sourceFrameStart": source_range[0],
            "sourceFrameEnd": source_range[1],
            "sampledFrames": source.get("sampledFrames"),
            "shippedFrames": len(images),
            "shippedFps": spec.get("fps"),
            "durationSeconds": source.get("durationSeconds"),
            "loop": spec.get("loop", True),
            "hold": spec.get("hold"),
            "rootMotionMeters": source.get("rootMotionMeters"),
            "loopClosureDegrees": source.get("loopClosureDegrees"),
            "notes": "; ".join(source.get("warnings") or []),
        })

    document = {
        "version": 1,
        "model": name,
        "sheet": "unit-sprites.png",
        "cellWidth": width,
        "cellHeight": height,
        "padding": padding,
        "colorSpace": "luminance",
        "pivot": [0.5, 0.5],
        "animations": animations,
    }

    with open(os.path.join(out_dir, "unit-sprites.json"), "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=2)
        handle.write("\n")

    with open(os.path.join(out_dir, "clips.json"), "w", encoding="utf-8") as handle:
        json.dump({"model": name, "clips": rows}, handle, indent=2)
        handle.write("\n")

    fields = ["name", "asset", "sourceFrames", "sourceFps", "sourceFrameStart", "sourceFrameEnd",
              "sampledFrames", "shippedFrames", "shippedFps", "durationSeconds", "loop", "hold",
              "rootMotionMeters", "loopClosureDegrees", "notes"]
    with open(os.path.join(out_dir, "clips.csv"), "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)

    return document


def main(argv):
    args = parse_args(argv)
    if not os.path.isdir(args.frames):
        print(f"frames directory not found: {args.frames}", file=sys.stderr)
        return EXIT_MISSING_INPUT
    if not os.path.isfile(args.preset):
        print(f"preset not found: {args.preset}", file=sys.stderr)
        return EXIT_MISSING_INPUT
    if args.frames_out and os.path.abspath(args.frames_out) == os.path.abspath(args.frames):
        # Writing the 1x frames over the supersampled source would make the next run downsample twice.
        print("--frames-out must differ from --frames (it holds the downsampled 1x frames)", file=sys.stderr)
        return EXIT_BAD_ARGS

    try:
        preset = load_json(args.preset)
        report = load_json(args.report) if args.report and os.path.isfile(args.report) else None
        assembly = load_json(args.assembly) if args.assembly and os.path.isfile(args.assembly) else None
    except json.JSONDecodeError as error:
        print(f"invalid JSON input: {error}", file=sys.stderr)
        return EXIT_INVALID

    clips = clip_directories(args.frames, preset)
    if not clips:
        print(f"no frames found under {args.frames}", file=sys.stderr)
        return EXIT_MISSING_INPUT

    os.makedirs(args.out, exist_ok=True)
    name = args.name or os.path.basename(os.path.dirname(os.path.abspath(args.frames))) or "model"

    downsample = 1
    if args.downsample == "auto":
        downsample = int((report or {}).get("supersample", 1) or 1)
    else:
        try:
            downsample = max(1, int(args.downsample))
        except ValueError:
            print(f"--downsample must be a positive integer or 'auto', got '{args.downsample}'", file=sys.stderr)
            return EXIT_BAD_ARGS

    loaded = load_frames(clips, downsample)
    width, height = cell_size(loaded)

    if args.frames_out:
        for clip_name, images in loaded:
            clip_dir = os.path.join(args.frames_out, clip_name)
            os.makedirs(clip_dir, exist_ok=True)
            for index, image in enumerate(images, start=1):
                image.save(os.path.join(clip_dir, f"{index:03d}.png"))

    atlas, placements, raw_width, raw_height = build_atlas(loaded, width, height, args.padding)
    atlas.save(os.path.join(args.out, "unit-sprites.png"))

    sheet = build_contact_sheet(loaded, preset, report, name)
    sheet.save(os.path.join(args.out, "contact-sheet.png"))

    for clip_name, images in loaded:
        clip_fps = (preset["clips"].get(clip_name) or {}).get("fps", 12)
        build_preview(images, os.path.join(args.out, f"preview-{clip_name}.gif"), clip_fps)

    document = write_metadata(
        args.out, name, loaded, width, height, args.padding, preset, report, assembly)

    print(json.dumps({
        "model": name,
        "clips": list(document["animations"].keys()),
        "cell": [width, height],
        "atlas": [atlas.width, atlas.height],
        "atlasContent": [raw_width, raw_height],
        "downsample": downsample,
        "frames": sum(len(images) for _, images in loaded),
        "out": os.path.abspath(args.out),
    }))
    return EXIT_OK


if __name__ == "__main__":
    sys.exit(main([arg for arg in sys.argv[1:]]))
