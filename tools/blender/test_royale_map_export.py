import math
import unittest

from royale_map_export import (
    COLLISION_COLLECTION,
    LOOT_COLLECTION,
    RENDER_COLLECTION,
    SPAWN_COLLECTION,
    WAYPOINT_COLLECTION,
    ExportError,
    Marker,
    SceneContract,
    blender_to_royale,
    build_map_document,
    stable_json,
    validate_contract,
)


def valid_contract() -> SceneContract:
    markers = (
        Marker(SPAWN_COLLECTION, "spawn.alpha", (1, 2, 3), (0, 0, math.pi / 2)),
        Marker(LOOT_COLLECTION, "loot.alpha", (0, 0, 0.35), (0, 0, 0)),
        Marker(WAYPOINT_COLLECTION, "waypoint.alpha", (0, 0, 0), (0, 0, 0), ("beta",)),
        Marker(WAYPOINT_COLLECTION, "waypoint.beta", (2, 0, 0), (0, 0, 0), ("alpha",)),
    )
    return SceneContract(
        properties={
            "royale_map_id": "test-map",
            "royale_map_name": "Test Map",
            "royale_bounds_min": (-10, -2, -10),
            "royale_bounds_max": (10, 5, 10),
            "royale_safe_zone_center": (0, 0, 0),
            "royale_safe_zone_radius": 8,
            "royale_output_asset_id": "test-environment",
        },
        collection_names=frozenset((RENDER_COLLECTION, COLLISION_COLLECTION, SPAWN_COLLECTION, LOOT_COLLECTION, WAYPOINT_COLLECTION)),
        object_collections={marker.name: (marker.collection,) for marker in markers},
        markers=markers,
    )


class ExportContractTests(unittest.TestCase):
    def test_coordinate_conversion(self):
        self.assertEqual((1.0, 3.0, -2.0), blender_to_royale((1, 2, 3)))

    def test_json_is_deterministic_and_links_are_canonical(self):
        contract = valid_contract()
        first = stable_json(build_map_document(contract))
        second = stable_json(build_map_document(contract))
        self.assertEqual(first, second)
        self.assertEqual(1, first.count('"from": "alpha"'))
        self.assertIn('"y": 90', first)

    def test_missing_collection_fails(self):
        contract = valid_contract()
        with self.assertRaisesRegex(ExportError, "missing required collection"):
            validate_contract(SceneContract(contract.properties, frozenset(), contract.object_collections, contract.markers))

    def test_malformed_marker_fails(self):
        contract = valid_contract()
        markers = (Marker(SPAWN_COLLECTION, "wrong", (0, 0, 0), (0, 0, 0)),) + contract.markers[1:]
        with self.assertRaisesRegex(ExportError, "must be named"):
            validate_contract(SceneContract(contract.properties, contract.collection_names, {**contract.object_collections, "wrong": (SPAWN_COLLECTION,)}, markers))

    def test_duplicate_id_fails(self):
        contract = valid_contract()
        duplicate = Marker(WAYPOINT_COLLECTION, "waypoint.alpha", (1, 0, 0), (0, 0, 0))
        with self.assertRaisesRegex(ExportError, "duplicate marker id"):
            validate_contract(SceneContract(contract.properties, contract.collection_names, contract.object_collections, contract.markers + (duplicate,)))

    def test_invalid_link_fails(self):
        contract = valid_contract()
        markers = contract.markers[:-2] + (Marker(WAYPOINT_COLLECTION, "waypoint.alpha", (0, 0, 0), (0, 0, 0), ("missing",)), contract.markers[-1])
        with self.assertRaisesRegex(ExportError, "unknown waypoint"):
            validate_contract(SceneContract(contract.properties, contract.collection_names, contract.object_collections, markers))

    def test_unsupported_transform_fails(self):
        contract = valid_contract()
        markers = (Marker(SPAWN_COLLECTION, "spawn.alpha", (0, 0, 0), (0.1, 0, 0)),) + contract.markers[1:]
        with self.assertRaisesRegex(ExportError, "pitch/roll"):
            validate_contract(SceneContract(contract.properties, contract.collection_names, contract.object_collections, markers))


if __name__ == "__main__":
    unittest.main()
