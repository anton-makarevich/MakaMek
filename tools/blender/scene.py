"""Scene construction for the headless sprite driver.

Everything here is derived from the preset and the `--scale` value the pipeline passes in; nothing
in the scene is hand-edited between runs (see docs/architecture/blender-render-driver.md, section 3).

Scale contract: after normalisation 1 model unit == 1 pixel, because `ortho_scale` is set to
`output.cellWidth`. All preset light distances and sizes are therefore expressed in pixels too,
which keeps one rig valid for every chassis regardless of how big the model is in metres.
"""

import math

import bpy
from mathutils import Vector

BOARD_LOOK_MATERIAL = "board-look"


def hex_to_rgb(value):
    """'#RRGGBB' (sRGB, as authored in the preset) -> linear RGB, which is what Blender expects."""
    text = value.lstrip("#")
    if len(text) != 6:
        raise ValueError(f"expected #RRGGBB, got {value!r}")

    def to_linear(channel):
        srgb = int(channel, 16) / 255.0
        return srgb / 12.92 if srgb <= 0.04045 else ((srgb + 0.055) / 1.055) ** 2.4

    return tuple(to_linear(text[i : i + 2]) for i in (0, 2, 4))


def reset():
    """Start from an empty scene: no default cube, camera or light survives."""
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_model(path):
    """Import a glTF/GLB and return only the newly created model objects.

    Blender's glTF importer parks helper objects (bone shapes, gizmos) in a `glTF_not_exported`
    collection. They are not part of the model, must not be measured, shaded or rendered.
    """
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    imported = [obj for obj in bpy.data.objects if obj not in before]

    for obj in list(imported):
        if any(collection.name == "glTF_not_exported" for collection in obj.users_collection):
            imported.remove(obj)
            bpy.data.objects.remove(obj, do_unlink=True)

    for collection in list(bpy.data.collections):
        if collection.name == "glTF_not_exported" and not collection.objects:
            bpy.data.collections.remove(collection)

    meshes = [obj for obj in imported if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"{path}: import produced no mesh objects")
    return imported


def _mesh_objects(objects):
    return [obj for obj in objects if obj.type == "MESH"]


def world_bounds(objects):
    """Axis-aligned world-space bounds over every mesh's bounding box."""
    minimum = Vector((math.inf, math.inf, math.inf))
    maximum = Vector((-math.inf, -math.inf, -math.inf))
    for obj in _mesh_objects(objects):
        for corner in obj.bound_box:
            point = obj.matrix_world @ Vector(corner)
            minimum.x = min(minimum.x, point.x)
            minimum.y = min(minimum.y, point.y)
            minimum.z = min(minimum.z, point.z)
            maximum.x = max(maximum.x, point.x)
            maximum.y = max(maximum.y, point.y)
            maximum.z = max(maximum.z, point.z)
    return minimum, maximum


def normalise(objects, scale, model_yaw_deg=0.0):
    """Scale/rotate the imported rig, put the footprint centre at the origin and the feet on z = 0.

    The model is never re-centred by its bounding-box *centre in 3D* - only the plan-view footprint
    is centred - so a tall pose change cannot move the sprite in the hex. The Z origin is the lowest
    vertex, i.e. the ground.
    """
    root = bpy.data.objects.new("ModelRoot", None)
    bpy.context.scene.collection.objects.link(root)
    for obj in objects:
        if obj.parent is None:
            obj.parent = root
            obj.matrix_parent_inverse.identity()

    root.scale = (scale, scale, scale)
    root.rotation_euler = (0.0, 0.0, math.radians(model_yaw_deg))
    bpy.context.view_layer.update()

    minimum, maximum = world_bounds(objects)
    size = maximum - minimum
    metrics = {
        "modelExtentMeters": [round(size.x / scale, 4), round(size.y / scale, 4), round(size.z / scale, 4)],
        "planViewMaxExtentMeters": round(max(size.x, size.y) / scale, 4),
        "heightMeters": round(size.z / scale, 4),
        "triangles": sum(len(obj.data.polygons) for obj in _mesh_objects(objects)),
    }

    centre_x = (minimum.x + maximum.x) / 2.0
    centre_y = (minimum.y + maximum.y) / 2.0
    root.location = (-centre_x, -centre_y, -minimum.z)
    bpy.context.view_layer.update()

    metrics["spriteExtentPx"] = [round(size.x, 2), round(size.y, 2)]
    return root, metrics


def add_camera(preset):
    """Orthographic, straight down, with `ortho_scale` pinned to the cell width."""
    output = preset["output"]
    camera_data = bpy.data.cameras.new("board-camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = float(output["cellWidth"])
    camera_data.clip_start = 1.0
    camera_data.clip_end = 10000.0

    camera = bpy.data.objects.new("board-camera", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    bpy.context.scene.camera = camera

    # A 90 degree pitch looks straight down -Z in Blender, which is already "top-down".
    pitch = math.radians(preset["camera"]["pitchDegrees"])
    yaw = math.radians(preset["camera"]["yawDegrees"])
    camera.rotation_euler = (pitch - math.pi / 2.0, 0.0, yaw)
    camera.location = (0.0, 0.0, 3.0 * float(output["cellWidth"]))

    # Orthographic: distance only affects clipping, never framing.
    return camera


def add_lights(preset, camera):
    """Neutral-white area lamps parented to the camera, aimed at the footprint origin.

    Parenting keeps the rig identical for every mech and frame. Positions and sizes are in pixel
    space (1 unit == 1 px), so the preset numbers describe the rig directly.

    The lights are children of the camera, so their `location` is camera-local while the model sits at
    the *world* origin - which is not the camera-local origin. The aim target therefore has to be the
    world origin transformed into camera space.
    """
    bpy.context.view_layer.update()
    target = camera.matrix_world.inverted() @ Vector((0.0, 0.0, 0.0))

    for index, spec in enumerate(preset["lights"]):
        light_data = bpy.data.lights.new(f"light-{index}", type=spec["type"].upper())
        light_data.color = hex_to_rgb(spec["color"])
        light_data.energy = spec["intensity"]
        if hasattr(light_data, "size"):
            light_data.size = spec["size"]

        light = bpy.data.objects.new(f"light-{index}", light_data)
        bpy.context.scene.collection.objects.link(light)
        light.parent = camera
        light.location = Vector(spec["position"])

        direction = target - light.location
        light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def override_materials(objects):
    """Replace every imported material with the flat `board-look` shader.

    Imported albedo, normal and mask maps are discarded on purpose: the sprite must be a
    luminance-only grey ramp so the runtime owner tint composites cleanly.
    """
    material = bpy.data.materials.new(BOARD_LOOK_MATERIAL)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    for node in list(nodes):
        if node.type != "OUTPUT_MATERIAL":
            nodes.remove(node)
    output = next(node for node in nodes if node.type == "OUTPUT_MATERIAL")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    material.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])

    shader.inputs["Base Color"].default_value = (0.90, 0.90, 0.90, 1.0)
    shader.inputs["Roughness"].default_value = 0.45
    shader.inputs["Metallic"].default_value = 0.0
    # Blender 4+/5 renamed "Specular"; keep the highlight low so creases read, not glints.
    for name in ("Specular IOR Level", "Specular"):
        if name in shader.inputs:
            shader.inputs[name].default_value = 0.25
            break

    material.diffuse_color = (0.90, 0.90, 0.90, 1.0)

    for obj in _mesh_objects(objects):
        obj.data.materials.clear()
        obj.data.materials.append(material)


def setup_freestyle(preset):
    """Silhouette plus crease outline - the replacement for the current hand-drawn line art."""
    outline = preset["styling"]["outline"]
    scene = bpy.context.scene
    if not outline["enabled"]:
        scene.render.use_freestyle = False
        return

    scene.render.use_freestyle = True
    scene.render.line_thickness_mode = "ABSOLUTE"
    scene.render.line_thickness = float(outline["width"])

    settings = bpy.context.view_layer.freestyle_settings
    settings.crease_angle = math.radians(120.0)
    line_set = settings.linesets[0] if settings.linesets else settings.linesets.new("board-outline")
    line_set.select_silhouette = True
    line_set.select_border = True
    line_set.select_crease = bool(outline.get("crease", True))
    line_set.select_contour = False
    line_set.select_external_contour = False
    line_set.select_material_boundary = False
    line_set.select_ridge_valley = False
    line_set.select_suggestive_contour = False

    line_style = line_set.linestyle
    if line_style is None:
        line_style = bpy.data.linestyles.new("board-outline")
        line_set.linestyle = line_style
    line_style.color = hex_to_rgb(outline["color"])
    # RenderSettings.line_thickness is the base thickness and LineStyle.thickness multiplies it, so
    # only the base carries the preset value - scaling both would square it under --supersample.
    line_style.thickness = 1.0


def setup_render(preset, engine=None, samples=None, device=None, threads=4):
    """Pin every render setting that would otherwise vary between machines."""
    output = preset["output"]
    engine_spec = preset["engine"]
    requested = engine or engine_spec["name"]
    scene = bpy.context.scene

    if requested == "eevee":
        try:
            scene.render.engine = "BLENDER_EEVEE_NEXT"
        except TypeError:
            scene.render.engine = "BLENDER_EEVEE"
    else:
        scene.render.engine = "CYCLES"
        scene.cycles.device = "GPU" if (device or engine_spec["device"]) == "gpu" else "CPU"
        scene.cycles.samples = int(samples or engine_spec["samples"])
        scene.cycles.use_adaptive_sampling = False
        scene.cycles.use_denoising = engine_spec["denoise"] != "none"
        if scene.cycles.use_denoising:
            scene.cycles.denoiser = "OPENIMAGEDENOISE"
        scene.cycles.seed = 0
        scene.cycles.use_animated_seed = False
        scene.cycles.max_bounces = 2
        scene.cycles.diffuse_bounces = 2
        scene.cycles.glossy_bounces = 1
        scene.cycles.transmission_bounces = 0
        scene.cycles.transparent_max_bounces = 2

    scene.render.threads_mode = "FIXED"
    scene.render.threads = threads
    scene.render.use_persistent_data = False
    scene.render.use_motion_blur = False

    scene.render.resolution_x = int(output["cellWidth"])
    scene.render.resolution_y = int(output["cellHeight"])
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = 1.0
    scene.render.pixel_aspect_y = 1.0

    scene.render.film_transparent = output["background"] == "transparent"

    scene.display_settings.display_device = "sRGB"
    scene.view_settings.view_transform = output["viewTransform"]
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0

    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.image_settings.compression = 15

    scene.frame_start = 1
    scene.frame_end = 1
    scene.frame_set(1)

    bpy.context.view_layer.update()
