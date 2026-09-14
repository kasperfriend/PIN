import tempfile
from pathlib import Path
import unittest

from npc_movement_audit import parse_behavior, csv_text
from sdb_dump import harvest_record_table_names, harvest_pin_names, ffnv32


class MovementAuditTests(unittest.TestCase):
    def test_nested_invocations_and_quoted_commas_are_not_flattened(self):
        name, params = parse_behavior('ChainWithPush(behaviorA="Crouch",behaviorB="LookAtNearestPlayer(maxDistance=15,emote=guard)",walk=true)')
        self.assertEqual(name, "ChainWithPush")
        self.assertEqual(params, {"behaviora": "Crouch", "behaviorb": "LookAtNearestPlayer(maxDistance=15,emote=guard)", "walk": "true"})
        self.assertNotIn("emote", params)

    def test_unquoted_nesting_and_quoted_text(self):
        _, params = parse_behavior('Wander(child=Other(a=1,b=2),text="hello, world",distance=12)')
        self.assertEqual(params["child"], "Other(a=1,b=2)")
        self.assertEqual(params["text"], "hello, world")
        self.assertNotIn("b", params)

    def test_empty_and_incomplete_inputs(self):
        self.assertEqual(parse_behavior(None), ("", {}))
        self.assertEqual(parse_behavior(" AggressiveWanderer "), ("AggressiveWanderer", {}))
        self.assertEqual(parse_behavior("Wander(,distance=2,DISTANCE=12,broken"), ("Wander", {"distance": "12"}))

    def test_unloaded_records_are_discovered_without_being_reported_as_loaded(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            records = root / "StaticDB" / "Records" / "clientmissions"
            records.mkdir(parents=True)
            (records / "MissionWaypoint.cs").write_text("public record class MissionWaypoint {}", encoding="utf-8")
            self.assertEqual(harvest_record_table_names(root), ["clientmissions::MissionWaypoint"])
            loaded, _ = harvest_pin_names(root)
            self.assertEqual(loaded, [])
            self.assertNotEqual(ffnv32("clientmissions::MissionWaypoint"), ffnv32("dbmissions::MissionWaypoint"))

    def test_csv_is_deterministic_and_preserves_quoted_arguments(self):
        result = csv_text(["id", "behavior"], [{"id": 1, "behavior": 'Wander(distance=10,emote="calm")'}])
        self.assertEqual(result, 'id,behavior\n1,"Wander(distance=10,emote=""calm"")"\n')


if __name__ == "__main__":
    unittest.main()
