#!/usr/bin/env python3
"""Validate and export a Royale map authored in Blender.

Run inside Blender, for example:
  blender courtyard.blend --background --python tools/blender/royale_map_export.py -- \
    --output-root .
Use --validate-only to perform every scene-contract check without writing files.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence


RENDER_COLLECTION = "Royale.Render"
COLLISION_COLLECTION = "Royale.Collision"
SPAWN_COLLECTION = "Royale.Spawns"
LOOT_COLLECTION = "Royale.Loot"
WAYPOINT_COLLECTION = "Royale.Navigation"
REQUIRED_COLLECTIONS = (
    RENDER_COLLECTION,
    COLLISION_COLLECTION,
    SPAWN_COLLECTION,
    LOOT_COLLECTION,
    WAYPOINT_COLLECTION,
)
ID_PATTERN = re.compile(r"^[A-Za-z0-9_-]+$")
MARKER_PREFIXES = {
    SPAWN_COLLECTION: "spawn.",
    LOOT_COLLECTION: "loot.",
    WAYPOINT_COLLECTION: "waypoint.",
}
SCENE_PROPERTIES = (
    "royale_map_id",
    "royale_map_name",
    "royale_bounds_min",
    "royale_bounds_max",
    "royale_safe_zone_center",
    "royale_safe_zone_radius",
    "royale_output_asset_id",
)


class ExportError(RuntimeError):
    """A contextual authoring-contract failure."""


@dataclass(frozen=True)
class Marker:
    collection: str
    name: str
    location: tuple[float, float, float]
    rotation: tuple[float, float, float]
    links: tuple[str, ...] = ()


@dataclass(frozen=True)
class SceneContract:
    properties: Mapping[str, Any]
    collection_names: frozenset[str]
    object_collections: Mapping[str, tuple[str, ...]]
    markers: tuple[Marker, ...]


def _finite_vector(value: Any, property_name: str) -> tuple[float, float, float]:
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)) or len(value) != 3:
        raise ExportError(f"scene property '{property_name}' must contain exactly three numbers")
    result = tuple(float(component) for component in value)
    if not all(math.isfinite(component) for component in result):
        raise ExportError(f"scene property '{property_name}' must be finite")
    return result  # type: ignore[return-value]


def blender_to_royale(position: Sequence[float]) -> tuple[float, float, float]:
    """Convert Blender (X, Y, Z) to Royale (X, Z, -Y)."""
    x, y, z = (float(component) for component in position)
    return (x, z, -y)


def marker_yaw_degrees(rotation: Sequence[float]) -> float:
    """Convert a marker's local Blender +Y forward to Royale yaw (0 faces -Z)."""
    rx, ry, rz = (float(component) for component in rotation)
    if abs(rx) > 1.0e-5 or abs(ry) > 1.0e-5:
        raise ExportError("marker rotation may only use Blender Z yaw (pitch/roll are unsupported)")
    yaw = math.degrees(rz)
    yaw = (yaw + 180.0) % 360.0 - 180.0
    return 0.0 if abs(yaw) < 1.0e-9 else yaw


def _marker_id(marker: Marker) -> str:
    prefix = MARKER_PREFIXES[marker.collection]
    if not marker.name.startswith(prefix):
        raise ExportError(
            f"marker '{marker.name}' in '{marker.collection}' must be named '{prefix}<id>'"
        )
    marker_id = marker.name[len(prefix) :]
    if not ID_PATTERN.fullmatch(marker_id):
        raise ExportError(f"marker '{marker.name}' has an invalid id '{marker_id}'")
    return marker_id


def validate_contract(contract: SceneContract) -> None:
    missing = [name for name in REQUIRED_COLLECTIONS if name not in contract.collection_names]
    if missing:
        raise ExportError(f"missing required collection(s): {', '.join(missing)}")

    for property_name in SCENE_PROPERTIES:
        if property_name not in contract.properties:
            raise ExportError(f"missing required scene property '{property_name}'")

    for property_name in ("royale_map_id", "royale_map_name", "royale_output_asset_id"):
        if not isinstance(contract.properties[property_name], str) or not contract.properties[property_name].strip():
            raise ExportError(f"scene property '{property_name}' must be a non-empty string")
    if not ID_PATTERN.fullmatch(contract.properties["royale_map_id"]):
        raise ExportError("scene property 'royale_map_id' contains unsupported characters")
    if not ID_PATTERN.fullmatch(contract.properties["royale_output_asset_id"]):
        raise ExportError("scene property 'royale_output_asset_id' contains unsupported characters")

    bounds_min = _finite_vector(contract.properties["royale_bounds_min"], "royale_bounds_min")
    bounds_max = _finite_vector(contract.properties["royale_bounds_max"], "royale_bounds_max")
    safe_center = _finite_vector(contract.properties["royale_safe_zone_center"], "royale_safe_zone_center")
    if any(low >= high for low, high in zip(bounds_min, bounds_max)):
        raise ExportError("scene bounds min must be less than max on every axis")
    radius = float(contract.properties["royale_safe_zone_radius"])
    if not math.isfinite(radius) or radius <= 0.0:
        raise ExportError("scene property 'royale_safe_zone_radius' must be finite and positive")
    if not all(low <= value <= high for low, value, high in zip(bounds_min, safe_center, bounds_max)):
        raise ExportError("safe-zone center must be inside scene bounds")

    ids_by_collection: dict[str, set[str]] = {name: set() for name in MARKER_PREFIXES}
    waypoint_links: dict[str, tuple[str, ...]] = {}
    for marker in contract.markers:
        owners = contract.object_collections.get(marker.name, ())
        if owners != (marker.collection,):
            raise ExportError(
                f"marker '{marker.name}' must belong only to '{marker.collection}', found {owners or 'no collection'}"
            )
        if not all(math.isfinite(component) for component in (*marker.location, *marker.rotation)):
            raise ExportError(f"marker '{marker.name}' has a non-finite transform")
        marker_yaw_degrees(marker.rotation)
        marker_id = _marker_id(marker)
        if marker_id in ids_by_collection[marker.collection]:
            raise ExportError(f"duplicate marker id '{marker_id}' in '{marker.collection}'")
        ids_by_collection[marker.collection].add(marker_id)
        if marker.collection == WAYPOINT_COLLECTION:
            waypoint_links[marker_id] = marker.links
        elif marker.links:
            raise ExportError(f"marker '{marker.name}' may not declare navigation links")

    if not ids_by_collection[SPAWN_COLLECTION]:
        raise ExportError("at least one spawn marker is required")
    if not ids_by_collection[WAYPOINT_COLLECTION]:
        raise ExportError("at least one waypoint marker is required")

    canonical_links: set[tuple[str, str]] = set()
    waypoint_ids = ids_by_collection[WAYPOINT_COLLECTION]
    for source, destinations in waypoint_links.items():
        for destination in destinations:
            if not isinstance(destination, str) or destination not in waypoint_ids:
                raise ExportError(f"waypoint '{source}' links to unknown waypoint '{destination}'")
            if source == destination:
                raise ExportError(f"waypoint '{source}' cannot link to itself")
            canonical_links.add(tuple(sorted((source, destination))))
    if len(waypoint_ids) > 1 and not canonical_links:
        raise ExportError("navigation requires at least one link")


def _number(value: float) -> int | float:
    rounded = round(float(value), 6)
    return int(rounded) if rounded.is_integer() else rounded


def _vector(value: Sequence[float]) -> dict[str, int | float]:
    return dict(zip(("x", "y", "z"), (_number(component) for component in value)))


def build_map_document(contract: SceneContract) -> dict[str, Any]:
    validate_contract(contract)
    properties = contract.properties
    groups: dict[str, list[tuple[str, Marker]]] = {name: [] for name in MARKER_PREFIXES}
    for marker in contract.markers:
        groups[marker.collection].append((_marker_id(marker), marker))
    for values in groups.values():
        values.sort(key=lambda item: item[0])

    waypoints = [
        {"id": marker_id, "position": _vector(blender_to_royale(marker.location))}
        for marker_id, marker in groups[WAYPOINT_COLLECTION]
    ]
    link_set = {
        tuple(sorted((marker_id, destination)))
        for marker_id, marker in groups[WAYPOINT_COLLECTION]
        for destination in marker.links
    }
    return {
        "id": properties["royale_map_id"],
        "name": properties["royale_map_name"],
        "worldBounds": {
            "min": _vector(properties["royale_bounds_min"]),
            "max": _vector(properties["royale_bounds_max"]),
        },
        "safeZone": {
            "center": _vector(properties["royale_safe_zone_center"]),
            "radius": _number(properties["royale_safe_zone_radius"]),
        },
        "spawnPoints": [
            {
                "id": marker_id,
                "position": _vector(blender_to_royale(marker.location)),
                "rotationEuler": {"x": 0, "y": _number(marker_yaw_degrees(marker.rotation)), "z": 0},
            }
            for marker_id, marker in groups[SPAWN_COLLECTION]
        ],
        "lootPoints": [
            {"id": marker_id, "position": _vector(blender_to_royale(marker.location))}
            for marker_id, marker in groups[LOOT_COLLECTION]
        ],
        "navigation": {
            "waypoints": waypoints,
            "links": [{"from": first, "to": second} for first, second in sorted(link_set)],
        },
        "staticModels": [
            {
                "id": "environment",
                "assetId": properties["royale_output_asset_id"],
                "position": {"x": 0, "y": 0, "z": 0},
                "rotationEuler": {"x": 0, "y": 0, "z": 0},
                "scale": {"x": 1, "y": 1, "z": 1},
            }
        ],
    }


def stable_json(document: Mapping[str, Any]) -> str:
    return json.dumps(document, ensure_ascii=True, indent=2, separators=(",", ": ")) + "\n"


def _links(value: Any, object_name: str) -> tuple[str, ...]:
    if value is None:
        return ()
    if isinstance(value, str):
        return tuple(part.strip() for part in value.split(",") if part.strip())
    if isinstance(value, Sequence):
        return tuple(str(part) for part in value)
    raise ExportError(f"waypoint '{object_name}' custom property 'royale_links' must be a list or comma-separated string")


def contract_from_blender() -> SceneContract:
    try:
        import bpy  # type: ignore
    except ImportError as error:
        raise ExportError("this command must run inside Blender") from error

    scene = bpy.context.scene
    collections = frozenset(collection.name for collection in bpy.data.collections)
    markers: list[Marker] = []
    ownership: dict[str, tuple[str, ...]] = {}
    for collection_name in MARKER_PREFIXES:
        collection = bpy.data.collections.get(collection_name)
        if collection is None:
            continue
        for obj in collection.objects:
            owners = tuple(sorted(owner.name for owner in obj.users_collection))
            ownership[obj.name] = owners
            if obj.type != "EMPTY":
                raise ExportError(f"marker '{obj.name}' in '{collection_name}' must be an Empty")
            markers.append(
                Marker(
                    collection=collection_name,
                    name=obj.name,
                    location=tuple(obj.location),
                    rotation=tuple(obj.rotation_euler),
                    links=_links(obj.get("royale_links"), obj.name),
                )
            )
    properties = {name: scene.get(name) for name in SCENE_PROPERTIES if name in scene}
    return SceneContract(properties, collections, ownership, tuple(markers))


def _export_collection(collection_name: str, output_path: Path, material_free: bool) -> None:
    import bpy  # type: ignore

    collection = bpy.data.collections[collection_name]
    objects = list(collection.all_objects)
    if not objects or any(obj.type != "MESH" for obj in objects):
        raise ExportError(f"collection '{collection_name}' must contain at least one mesh and only meshes")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    saved_materials: dict[str, list[Any]] = {}
    if material_free:
        for obj in objects:
            saved_materials[obj.name] = list(obj.data.materials)
            obj.data.materials.clear()
    try:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        result = bpy.ops.export_scene.gltf(
            filepath=str(output_path),
            export_format="GLB",
            use_selection=True,
            export_yup=True,
            export_apply=True,
            export_normals=True,
            export_materials="NONE" if material_free else "EXPORT",
            export_animations=False,
            export_cameras=False,
            export_lights=False,
        )
        if "FINISHED" not in result:
            raise ExportError(f"Blender GLB export failed for collection '{collection_name}'")
    finally:
        if material_free:
            for obj in objects:
                for material in saved_materials[obj.name]:
                    obj.data.materials.append(material)


def parse_arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", type=Path, default=Path.cwd())
    parser.add_argument("--validate-only", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_arguments(sys.argv[sys.argv.index("--") + 1 :] if argv is None and "--" in sys.argv else (argv or ()))
    try:
        contract = contract_from_blender()
        document = build_map_document(contract)
        map_id = str(contract.properties["royale_map_id"])
        asset_id = str(contract.properties["royale_output_asset_id"])
        if not arguments.validate_only:
            root = arguments.output_root.resolve()
            _export_collection(RENDER_COLLECTION, root / "assets" / "meshes" / map_id / f"{asset_id}.glb", False)
            _export_collection(COLLISION_COLLECTION, root / "assets" / "meshes" / map_id / f"{asset_id}-collision.glb", True)
            map_path = root / "src" / "Royale.Content" / "Maps" / f"{map_id}.json"
            map_path.parent.mkdir(parents=True, exist_ok=True)
            map_path.write_text(stable_json(document), encoding="utf-8", newline="\n")
        print(f"Royale map '{map_id}' validation succeeded" + ("" if arguments.validate_only else "; export complete"))
        return 0
    except (ExportError, OSError, ValueError) as error:
        print(f"Royale map export failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
