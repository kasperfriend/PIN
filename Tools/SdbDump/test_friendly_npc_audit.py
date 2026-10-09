import unittest
from friendly_npc_audit import faction_stances


class FriendlyAuditTests(unittest.TestCase):
    def test_wildcard_defaults_are_directional_and_overridden_by_alliance(self):
        factions = [{'id': 1, 'default_stance': 0}, {'id': 5, 'default_stance': -1}]
        relations = [{'faction_a': 0, 'faction_b': 0, 'hostility_stance': 0, 'hostility_bidirectional': 1}]
        result = faction_stances(factions, relations)
        self.assertEqual(result[5, 1], 'Hostile')
        self.assertEqual(result[1, 5], 'Neutral')
        relations.append({'faction_a': 0, 'faction_b': 1, 'hostility_stance': 1, 'hostility_bidirectional': 0})
        result = faction_stances(factions, relations)
        self.assertEqual(result[5, 1], 'Friendly')
        self.assertEqual(result[1, 5], 'Neutral')
        self.assertNotIn((0, 1), result)

    def test_bidirectional_specific_rows_and_missing_factions(self):
        factions = [{'id': 1, 'default_stance': 0}, {'id': 9, 'default_stance': 0}]
        relations = [{'faction_a': 9, 'faction_b': 1, 'hostility_stance': 1, 'hostility_bidirectional': 1},
                     {'faction_a': 99, 'faction_b': 1, 'hostility_stance': 1, 'hostility_bidirectional': 1}]
        result = faction_stances(factions, relations)
        self.assertEqual(result, {(9, 1): 'Friendly', (1, 9): 'Friendly'})


if __name__ == '__main__':
    unittest.main()
