"""Exact repaired-case portability contracts must reject extra drift, never tolerances."""
import copy
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[2] / 'tools/rea009-handoff-audit/compare.py'
spec = importlib.util.spec_from_file_location('rea009_compare', SOURCE)
compare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(compare)


class RepairedPlatformContractTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.windows = Path(self.temp.name) / 'windows'
        self.ubuntu = Path(self.temp.name) / 'ubuntu'
        self.expected = {'Schema': 'rea009-native-b-portability-v1',
                         'SummaryBehaviorHashes': {}, 'Divergences': {}}
        self.paths = {}
        for scenario in ('three-squeeze', 'four-close-regain'):
            for configuration in 'ABC':
                name = f'after-{scenario}-19-{configuration}'
                native = configuration == 'B'
                if native:
                    self.expected['SummaryBehaviorHashes'][name] = {'Windows': 'wb', 'Ubuntu': 'ub'}
                    self.expected['Divergences'][name] = {
                        'root.Steps[1].Interaction.Episodes[0].Candidates[0].MinimumSeparationMeters': {
                            'Windows': {'Type': 'double', 'Bits': '3FF0000000000000'},
                            'Ubuntu': {'Type': 'double', 'Bits': '3FF0000000000001'}}}
                for os, root in (('Windows', self.windows), ('Ubuntu', self.ubuntu)):
                    directory = root / name
                    directory.mkdir(parents=True)
                    manifest = {'Completed': True, 'Mode': 'repro', 'Cases': [{
                        'Id': scenario, 'Seed': 19, 'Configuration': configuration,
                        'Failed': False, 'ReversedExact': True, 'ObserverExact': True,
                        'FinalHash': 'same-final', 'BehaviorHash': ('wb' if os == 'Windows' else 'ub') if native else 'same-behavior'}]}
                    (directory / 'manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
                    trace = {'root.Steps[1].Changes[0].Position.TotalSegmentProgress': 'double:3FF0000000000000'}
                    if native:
                        path = next(iter(self.expected['Divergences'][name]))
                        trace[path] = 'double:3FF000000000000' + ('0' if os == 'Windows' else '1')
                    path = directory / f'trace-{scenario}-19-{configuration}.json.gz'
                    self.paths[(name, os)] = path
                    with gzip.open(path, 'wt', encoding='utf-8') as stream:
                        json.dump(trace, stream)

    def test_complete_native_map_and_all_other_leaves_are_exact(self):
        report = compare.compare_platforms(self.windows, self.ubuntu, self.expected)
        self.assertEqual(report['ObservedDivergences'], self.expected['Divergences'])
        self.assertEqual(len(report['Cases']), 6)

    def test_extra_executed_state_drift_is_rejected(self):
        path = self.paths[('after-three-squeeze-19-B', 'Ubuntu')]
        with gzip.open(path) as stream:
            trace = json.load(stream)
        trace['root.Steps[1].Changes[0].Position.TotalSegmentProgress'] = 'double:3FF0000000000001'
        with gzip.open(path, 'wt', encoding='utf-8') as stream:
            json.dump(trace, stream)
        with self.assertRaisesRegex(ValueError, 'divergence map changed'):
            compare.compare_platforms(self.windows, self.ubuntu, self.expected)

    def test_omitted_diagnostic_delta_is_rejected(self):
        expected = copy.deepcopy(self.expected)
        expected['Divergences'].pop('after-four-close-regain-19-B')
        with self.assertRaisesRegex(ValueError, 'divergence map changed'):
            compare.compare_platforms(self.windows, self.ubuntu, expected)
