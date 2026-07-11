"""Create the editable Courtyard Compound source scene inside Blender."""

import math
import os
import sys

import bpy


ROOT = "/Users/chronium/Developer/royale"
ASSET_DIR = os.path.join(ROOT, "assets", "meshes", "courtyard-compound")
BLEND_PATH = os.path.join(ASSET_DIR, "courtyard-compound.blend")


def royale_location(x, y, z):
    return (x, -z, y)


def royale_dimensions(x, y, z):
    return (x, z, y)


def collection(name):
    value = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(value)
    return value


def material(name, color, metallic=0.0, roughness=0.8):
    value = bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.use_nodes = True
    bsdf = value.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return value


def cube(name, position, size, render_material, collision=True, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=royale_location(*position))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = royale_dimensions(*size)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    render.objects.link(obj)
    obj.data.materials.append(render_material)
    if bevel:
        modifier = obj.modifiers.new("Subtle edge bevel", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
    if collision:
        duplicate = obj.copy()
        duplicate.data = obj.data.copy()
        duplicate.name = "collision." + name
        duplicate.data.materials.clear()
        duplicate.modifiers.clear()
        collision_collection.objects.link(duplicate)
    return obj


def ramp(name, start, end, width, thickness, render_material):
    sx, sy, sz = start
    ex, ey, ez = end
    dx, dy, dz = ex - sx, ey - sy, ez - sz
    length = math.hypot(dx, dz)
    center = ((sx + ex) / 2, (sy + ey) / 2 - thickness / 2, (sz + ez) / 2)
    obj = cube(name, center, (width, thickness, length), render_material, collision=True)
    if abs(dx) > 1.0e-6:
        raise ValueError("Courtyard ramps currently support Z-aligned runs only")
    obj.rotation_euler[0] = -math.copysign(math.atan2(dy, length), dz)
    for candidate in collision_collection.objects:
        if candidate.name == "collision." + name:
            candidate.rotation_euler = obj.rotation_euler
            break
    return obj


def steps(name, start, direction, count, width, tread, rise, render_material):
    x, y, z = start
    dx, dz = direction
    for index in range(count):
        height = rise * (index + 1)
        cube(
            f"{name}.{index + 1:02d}",
            (x + dx * tread * (index + 0.5), y + height / 2, z + dz * tread * (index + 0.5)),
            (width if dz else tread, height, tread if dz else width),
            render_material,
            collision=False,
        )


def marker(target_collection, prefix, marker_id, position, yaw=0.0, links=None):
    obj = bpy.data.objects.new(prefix + marker_id, None)
    target_collection.objects.link(obj)
    obj.empty_display_type = "ARROWS" if prefix == "spawn." else "PLAIN_AXES"
    obj.empty_display_size = 0.7
    obj.location = royale_location(*position)
    obj.rotation_euler[2] = math.radians(yaw)
    if links:
        obj["royale_links"] = ",".join(links)
    return obj


# Start from an empty, deterministic scene.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for value in list(bpy.data.collections):
    bpy.data.collections.remove(value)

render = collection("Royale.Render")
collision_collection = collection("Royale.Collision")
spawns = collection("Royale.Spawns")
loot = collection("Royale.Loot")
navigation = collection("Royale.Navigation")

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene["royale_map_id"] = "courtyard-compound"
scene["royale_map_name"] = "Courtyard Compound"
scene["royale_bounds_min"] = [-50.0, -2.0, -50.0]
scene["royale_bounds_max"] = [50.0, 10.0, 50.0]
scene["royale_safe_zone_center"] = [0.0, 0.0, 0.0]
scene["royale_safe_zone_radius"] = 45.0
scene["royale_output_asset_id"] = "courtyard-compound-environment"

turf = material("Turf", (0.19, 0.34, 0.18), roughness=0.95)
concrete = material("Cool Concrete", (0.42, 0.49, 0.52), roughness=0.85)
plaster = material("Warm Plaster", (0.67, 0.56, 0.43), roughness=0.9)
red = material("Muted Red", (0.48, 0.16, 0.12), roughness=0.78)
steel = material("Dark Steel", (0.09, 0.12, 0.14), metallic=0.55, roughness=0.55)
wood = material("Limited Wood", (0.33, 0.20, 0.11), roughness=0.82)

# Play surface and restrained visible boundary.
cube("ground", (0, -0.2, 0), (100, 0.4, 100), turf)
for name, position, size in (
    ("boundary.north", (0, 0.55, -49), (98, 1.1, 0.35)),
    ("boundary.south", (0, 0.55, 49), (98, 1.1, 0.35)),
    ("boundary.west", (-49, 0.55, 0), (0.35, 1.1, 98)),
    ("boundary.east", (49, 0.55, 0), (0.35, 1.1, 98)),
):
    cube(name, position, size, red)

# Building slabs: ground, split upper floor leaving an interior stair void, and roof edge.
cube("building.ground-slab", (0, -0.1, -11), (28, 0.2, 18), concrete)
cube("building.upper-floor-west", (-8.5, 3.1, -11), (11, 0.2, 18), concrete)
cube("building.upper-floor-east", (8.5, 3.1, -11), (11, 0.2, 18), concrete)
cube("building.upper-floor-south-bridge", (0, 3.1, -3.75), (6, 0.2, 3.5), concrete)

# Exterior walls are segmented for permanently open doors and real window apertures.
wall_t = 0.35
for level_y in (1.55, 4.75):
    # West/east walls with two window openings per level.
    for x in (-13.825, 13.825):
        for z, length in ((-18.0, 4.0), (-13.0, 3.2), (-8.0, 3.2), (-3.0, 2.0)):
            cube(f"building.wall.{x}.{level_y}.{z}", (x, level_y, z), (wall_t, 3.1, length), plaster)
        for z in (-15.4, -10.4, -5.4):
            cube(f"building.sill.{x}.{level_y}.{z}", (x, level_y - 1.1, z), (wall_t, 0.9, 1.2), red)
            cube(f"building.window-head.{x}.{level_y}.{z}", (x, level_y + 1.25, z), (wall_t, 0.6, 1.2), plaster)
    # North wall with windows and an exterior-stair upper doorway at east.
    north_segments = ((-11.0, 6.0), (-4.5, 3.0), (1.0, 6.0), (9.5, 5.0)) if level_y < 3 else ((-11.0, 6.0), (-4.5, 3.0), (1.0, 6.0))
    for x, width in north_segments:
        cube(f"building.north.{level_y}.{x}", (x, level_y, -19.825), (width, 3.1, wall_t), plaster)
    for x in (-7.0, 5.0):
        cube(f"building.north-sill.{level_y}.{x}", (x, level_y - 1.1, -19.825), (2.0, 0.9, wall_t), red)
        cube(f"building.north-head.{level_y}.{x}", (x, level_y + 1.25, -19.825), (2.0, 0.6, wall_t), plaster)
    # South facade: three wide, permanently open entrances on ground; windows above.
    if level_y < 3:
        for x, width in ((-11.0, 6.0), (-4.0, 4.0), (4.0, 4.0), (11.0, 6.0)):
            cube(f"building.south-ground.{x}", (x, level_y, -2.175), (width, 3.1, wall_t), plaster)
        for x in (-7.0, 0.0, 7.0):
            cube(f"building.south-door-head.{x}", (x, 2.75, -2.175), (2.0, 0.7, wall_t), red)
    else:
        for x, width in ((-11.0, 5.0), (-5.0, 3.0), (0.0, 5.0), (6.0, 3.0), (11.5, 5.0)):
            cube(f"building.south-upper.{x}", (x, level_y, -2.175), (width, 3.1, wall_t), plaster)
        for x in (-7.0, 3.5):
            cube(f"building.south-upper-sill.{x}", (x, level_y - 1.1, -2.175), (2.0, 0.9, wall_t), red)
            cube(f"building.south-upper-head.{x}", (x, level_y + 1.25, -2.175), (2.0, 0.6, wall_t), plaster)

# Interior hall/rooms with open doorway gaps and standing clearance.
for x in (-5.0, 5.0):
    for level_y in (1.55, 4.75):
        for z in (-16.0, -7.0):
            cube(f"room-wall.{x}.{level_y}.{z}.a", (x, level_y, z - 1.5), (0.25, 3.1, 3.0), concrete)
            cube(f"room-wall.{x}.{level_y}.{z}.b", (x, level_y, z + 2.0), (0.25, 3.1, 2.0), concrete)
            cube(f"room-door-head.{x}.{level_y}.{z}", (x, level_y + 1.25, z + 0.25), (0.25, 0.6, 1.5), red)

# Interior U route: two gentle collision ramps with a broad landing; visible 0.32 m risers.
steps("interior-stairs-a", (-2.4, 0, -7.5), (0, -1), 6, 2.2, 1.04, 1.6 / 6, concrete)
ramp("interior-ramp-a", (-2.4, 0, -7.0), (-2.4, 1.6, -13.25), 2.2, 0.18, concrete)
cube("interior-stair-landing", (0, 1.5, -14.0), (7.0, 0.2, 2.0), concrete)
steps("interior-stairs-b", (2.4, 1.6, -13.5), (0, 1), 6, 2.2, 1.04, 1.6 / 6, concrete)
ramp("interior-ramp-b", (2.4, 1.6, -13.25), (2.4, 3.2, -7.0), 2.2, 0.18, concrete)

# Rear exterior stair gives a second upper route, with matching collision ramp.
steps("exterior-stairs", (10.0, 0, -30.0), (0, 1), 11, 2.4, 10.0 / 11, 3.2 / 11, steel)
ramp("exterior-ramp", (10.0, 0, -30.0), (10.0, 3.2, -20.0), 2.4, 0.18, steel)
cube("exterior-upper-landing", (10.0, 3.1, -19.0), (4.5, 0.2, 2.0), steel)

# Courtyard fence at X -20..20, Z -2..33 with south, east, west gates.
for name, position, size in (
    ("fence.west.north", (-20, 1.0, 23), (0.35, 2.0, 20)),
    ("fence.west.south", (-20, 1.0, 5), (0.35, 2.0, 10)),
    ("fence.east.north", (20, 1.0, 23), (0.35, 2.0, 20)),
    ("fence.east.south", (20, 1.0, 5), (0.35, 2.0, 10)),
    ("fence.south.west", (-12.5, 1.0, 33), (15, 2.0, 0.35)),
    ("fence.south.east", (12.5, 1.0, 33), (15, 2.0, 0.35)),
):
    cube(name, position, size, steel)

# Moderate cover, kept away from authored navigation corridors.
for name, position, size, mat in (
    ("cover.barrier-west", (-11, 0.55, 12), (5, 1.1, 0.7), concrete),
    ("cover.barrier-east", (11, 0.55, 19), (5, 1.1, 0.7), concrete),
    ("cover.planter-a", (-6, 0.45, 25), (4, 0.9, 2), plaster),
    ("cover.planter-b", (7, 0.45, 9), (3, 0.9, 2), plaster),
    ("cover.utility-a", (-27, 0.8, 6), (3, 1.6, 3), steel),
    ("cover.utility-b", (29, 0.8, -7), (3, 1.6, 3), steel),
    ("cover.crate-a", (-29, 0.75, 24), (1.5, 1.5, 1.5), wood),
    ("cover.crate-b", (27, 0.75, 25), (1.5, 1.5, 1.5), wood),
    ("cover.crate-c", (4, 0.75, 27), (1.5, 1.5, 1.5), wood),
):
    cube(name, position, size, mat, bevel=0.06)

# Spawn ring, all facing approximately toward the origin.
spawn_data = (
    ("north", (0, 0, -42)), ("north-east", (30, 0, -34)),
    ("east-north", (42, 0, -17)), ("east-south", (42, 0, 17)),
    ("south-east", (30, 0, 40)), ("south", (0, 0, 42)),
    ("south-west", (-30, 0, 40)), ("west-south", (-42, 0, 17)),
    ("west-north", (-42, 0, -17)), ("north-west", (-30, 0, -34)),
    ("north-inner-east", (17, 0, -40)), ("north-inner-west", (-17, 0, -40)),
)
for marker_id, position in spawn_data:
    x, _, z = position
    yaw = math.degrees(math.atan2(-x, -z))
    marker(spawns, "spawn.", marker_id, position, yaw)

loot_data = (
    ("north-approach", (0, 0.35, -27)), ("west-approach", (-27, 0.35, 10)),
    ("east-approach", (28, 0.35, 5)), ("courtyard-south", (0, 0.35, 29)),
    ("courtyard-west", (-15, 0.35, 16)), ("courtyard-east", (15, 0.35, 14)),
    ("hall-ground", (0, 0.35, -5)), ("west-room-ground", (-9, 0.35, -12)),
    ("east-room-ground", (9, 0.35, -12)), ("upper-west", (-9, 3.54, -11)),
    ("upper-east", (9, 3.54, -11)), ("upper-landing", (1, 3.54, -4)),
)
for marker_id, position in loot_data:
    marker(loot, "loot.", marker_id, position)

# Connected graph with coverage points and explicit gates, entrances, stairs, rooms and flanks.
waypoints = {
    **{f"spawn-{marker_id}": position for marker_id, position in spawn_data},
    **{f"loot-{marker_id}": (position[0], position[1] - 0.35, position[2]) for marker_id, position in loot_data},
    "north-flank": (0, 0, -34), "north-building": (0, 0, -23),
    "west-flank": (-34, 0, 0), "east-flank": (34, 0, 0),
    "west-gate-out": (-23, 0, 11.5), "west-gate-in": (-17, 0, 11.5),
    "east-gate-out": (23, 0, 11.5), "east-gate-in": (17, 0, 11.5),
    "south-gate-out": (0, 0, 37), "south-gate-in": (0, 0, 29),
    "courtyard-center": (0, 0, 16), "courtyard-north": (0, 0, 5),
    "entrance-west": (-7, 0, -1), "entrance-center": (0, 0, -1), "entrance-east": (7, 0, -1),
    "hall-center": (0, 0, -7),
    "room-west-ground": (-9, 0, -12), "room-east-ground": (9, 0, -12),
    "interior-stair-bottom": (-2.4, 0, -6.2), "interior-stair-landing-a": (-2.4, 1.59, -14.5),
    "interior-stair-landing-b": (2.4, 1.59, -14.5), "interior-stair-ramp-top": (2.4, 3.11, -7.3),
    "interior-stair-top": (4.0, 3.19, -7.3),
    "upper-landing": (0, 3.19, -5), "upper-west-south": (-4, 3.19, -5),
    "upper-east-south": (4, 3.19, -5),
    "room-west-upper": (-9, 3.19, -11), "room-east-upper": (9, 3.19, -11),
    "exterior-stair-bottom": (10, 0, -30.8), "exterior-stair-top": (10, 3.19, -19.0),
    "rear-upper-door": (10, 3.19, -18),
}
links = [
    ("spawn-north", "north-flank"), ("spawn-north-east", "spawn-north-inner-east"),
    ("spawn-east-north", "east-flank"), ("spawn-east-south", "east-flank"),
    ("spawn-south-east", "south-gate-out"), ("spawn-south", "south-gate-out"),
    ("spawn-south-west", "south-gate-out"), ("spawn-west-south", "west-flank"),
    ("spawn-west-north", "west-flank"), ("spawn-north-west", "spawn-north-inner-west"),
    ("spawn-north-inner-east", "north-flank"), ("spawn-north-inner-west", "north-flank"),
    ("north-flank", "north-building"), ("north-building", "loot-north-approach"),
    ("north-building", "exterior-stair-bottom"), ("west-flank", "loot-west-approach"),
    ("loot-west-approach", "west-gate-out"), ("west-gate-out", "west-gate-in"),
    ("east-flank", "loot-east-approach"), ("loot-east-approach", "east-gate-out"),
    ("east-gate-out", "east-gate-in"), ("south-gate-out", "south-gate-in"),
    ("south-gate-in", "loot-courtyard-south"), ("loot-courtyard-south", "courtyard-center"),
    ("west-gate-in", "loot-courtyard-west"), ("loot-courtyard-west", "courtyard-center"),
    ("east-gate-in", "loot-courtyard-east"), ("loot-courtyard-east", "courtyard-center"),
    ("courtyard-center", "courtyard-north"), ("courtyard-north", "entrance-center"),
    ("courtyard-north", "entrance-west"), ("courtyard-north", "entrance-east"),
    ("entrance-center", "loot-hall-ground"), ("loot-hall-ground", "hall-center"),
    ("entrance-west", "room-west-ground"), ("room-west-ground", "loot-west-room-ground"),
    ("entrance-east", "room-east-ground"), ("room-east-ground", "loot-east-room-ground"),
    ("hall-center", "interior-stair-bottom"),
    ("interior-stair-bottom", "interior-stair-landing-a"),
    ("interior-stair-landing-a", "interior-stair-landing-b"),
    ("interior-stair-landing-b", "interior-stair-ramp-top"),
    ("interior-stair-ramp-top", "interior-stair-top"),
    ("interior-stair-top", "upper-east-south"), ("upper-landing", "loot-upper-landing"),
    ("upper-landing", "upper-west-south"), ("upper-west-south", "room-west-upper"),
    ("room-west-upper", "loot-upper-west"), ("upper-landing", "upper-east-south"),
    ("upper-east-south", "room-east-upper"), ("room-east-upper", "loot-upper-east"),
    ("room-east-upper", "rear-upper-door"),
    ("exterior-stair-bottom", "exterior-stair-top"),
    ("exterior-stair-top", "rear-upper-door"),
]
neighbors = {waypoint_id: [] for waypoint_id in waypoints}
for first, second in links:
    neighbors[first].append(second)
    neighbors[second].append(first)
for waypoint_id, position in waypoints.items():
    grounded_position = (position[0], -0.01, position[2]) if position[1] == 0 else position
    marker(navigation, "waypoint.", waypoint_id, grounded_position, links=sorted(neighbors[waypoint_id]))

# Save the editable source; exporter validation/export is invoked separately.
os.makedirs(ASSET_DIR, exist_ok=True)
bpy.ops.outliner.orphans_purge(do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
result = {
    "blend": BLEND_PATH,
    "render_objects": len(render.objects),
    "collision_objects": len(collision_collection.objects),
    "spawns": len(spawns.objects),
    "loot": len(loot.objects),
    "waypoints": len(navigation.objects),
    "links": len(links),
}
