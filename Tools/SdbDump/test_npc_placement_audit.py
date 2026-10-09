import unittest
from npc_placement_audit import classify, classification_sets, risks


class PlacementAuditTests(unittest.TestCase):
    def row(self, **changes):
        result = dict(behavior='', chassis_id=1, posetype_id=0, vendor_id=0, behavior_instance_id=0)
        result.update(changes)
        return result

    def test_classifier_inventory_tracks_explicit_sets(self):
        source = '''_neverWorldSpawned = new(StringComparer.OrdinalIgnoreCase) { "Null", "PlayerPet" };
_settlementBehaviors = new(StringComparer.OrdinalIgnoreCase) { "BasicCivilian" };'''
        excluded, settlement = classification_sets(source)
        self.assertEqual(classify(self.row(behavior='NULL'), None, excluded, settlement)[0], 'Excluded')
        self.assertEqual(classify(self.row(behavior='BasicCivilian'), None, excluded, settlement)[0], 'Settlement')
        self.assertEqual(classify(self.row(), 'chosen', excluded, settlement)[0], 'Melding|Wilderness')
        self.assertEqual(classify(self.row(chassis_id=0), None, excluded, settlement)[0], 'Excluded')

    def test_risk_flags_do_not_claim_original_location(self):
        self.assertIn('route/follow/named-point assignment missing', risks(self.row(behavior='OneOff_FollowRoute'), 'Wilderness'))
        self.assertIn('pose may require an assigned prop', risks(self.row(behavior='PerformEmote(emote="controlseat")'), 'Settlement'))
        self.assertIn('empty invocation has unresolved CAIS instance', risks(self.row(behavior_instance_id=362), 'Wilderness'))
        self.assertEqual(risks(self.row(vendor_id=7), 'Excluded'), [])
