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
        self.assertEqual(classify(self.row(behavior='NULL'), None, excluded, settlement, set(), set())[0], 'Excluded')
        self.assertEqual(classify(self.row(behavior='BasicCivilian'), None, excluded, settlement, set(), set())[0], 'Settlement')
        self.assertEqual(classify(self.row(), 'chosen', excluded, settlement, set(), set())[0], 'Melding|Wilderness')
        self.assertEqual(classify(self.row(chassis_id=0), None, excluded, settlement, set(), set())[0], 'Excluded')

    def test_risk_flags_do_not_claim_original_location(self):
        self.assertIn('route/follow/named-point assignment missing', risks(self.row(behavior='OneOff_FollowRoute'), 'Wilderness'))
        self.assertIn('pose may require an assigned prop', risks(self.row(behavior='PerformEmote(emote="controlseat")'), 'Settlement'))
        self.assertIn('empty invocation has unresolved CAIS instance', risks(self.row(behavior_instance_id=362), 'Wilderness'))
        self.assertEqual(risks(self.row(vendor_id=7), 'Excluded'), [])

    def test_assignment_requirements_are_procedural_exclusions(self):
        routes = {'oneoff_followroute'}
        poses = {'controlseat'}
        for behavior, vendor in [('OneOff_FollowRoute', 0), ('Wander(city_prefix=Town)', 0),
                                 ('Stand(climber=true)', 0), ('Wander(grounded=0)', 0),
                                 ('Wander(groundOffset=1.6)', 0), ('Stand(emote=CONTROLSEAT)', 0),
                                 ('PerformEmoteNoPhysics', 0), ('Wander', 22)]:
            with self.subTest(behavior=behavior):
                result = classify(self.row(behavior=behavior, vendor_id=vendor), 'chosen', set(), set(), routes, poses)
                self.assertEqual(result[0], 'Excluded')
                self.assertTrue(result[1].startswith('requires'))

    def test_ordinary_nested_and_invalid_settings_do_not_invent_requirements(self):
        for behavior in ['Wander(groundOffset=NaN)', 'Wander(groundOffset=Infinity)',
                         'Wander(groundOffset=-1)', 'Wander(child=Other(climber=true,grounded=0))',
                         'Wander(climber=0,grounded=1,inSpawnVolume=false)',
                         'Wander(emote=unknown_chair_pose)', 'Wander(restFunction=Work,city_prefix="")']:
            self.assertEqual(classify(self.row(behavior=behavior), None, set(), set(), set(), {'controlseat'}), ('Wilderness', ''))
