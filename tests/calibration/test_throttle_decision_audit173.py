"""Protect causal attribution and the sealed diagnostic evidence without new worlds."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[2]
TOOL=ROOT/'tools/throttle-decision-audit'
DOCS=ROOT/'docs/experiments/throttle-decision-audit'


class ThrottleDecisionAudit173Tests(unittest.TestCase):
    def test_observation_derivation_and_historical_inputs(self):
        subprocess.run([sys.executable,str(TOOL/'generate173.py'),'--check'],cwd=ROOT,
            check=True,capture_output=True,text=True,timeout=30)
        inputs=json.loads((DOCS/'INPUTS.json').read_bytes())
        self.assertEqual(inputs['base'],'c5f0a4cc4de34fc4c8c47ef912cafabf34055f29')
        for name,digest in inputs['files'].items():
            self.assertEqual(hashlib.sha256((ROOT/name).read_bytes()).hexdigest(),digest,name)

    def test_clamping_does_not_create_false_individual_causes(self):
        spec=importlib.util.spec_from_file_location('throttle173_analysis',TOOL/'analyze173.py')
        module=importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        hidden=module.baseline_effects(-.1,-.2,0,0)
        self.assertEqual(hidden['speed'],0)
        effective=module.baseline_effects(.5,-.2,.4,0)
        self.assertAlmostEqual(effective['speed'],.1)
        self.assertAlmostEqual(effective['slip'],.3)
        interacting=module.baseline_effects(.2,-.3,.3,0)
        self.assertEqual(interacting['speed'],0)
        self.assertEqual(interacting['slip'],0)
        self.assertGreater(.2,interacting['speed']+interacting['slip'])

    def test_full_audit_pending_and_controlled_native_prefixes(self):
        for line in (DOCS/'SHA256SUMS').read_text(encoding='utf-8').splitlines():
            digest,name=line.split('  ',1)
            self.assertEqual(hashlib.sha256((ROOT/name).read_bytes()).hexdigest(),digest,name)
        with tempfile.TemporaryDirectory() as temporary:
            for name in ('173-diagnostics.zip','173-native.zip'):
                with ZipFile(DOCS/name) as archive:archive.extractall(temporary)
            result=subprocess.run([sys.executable,str(TOOL/'verify173.py'),temporary],cwd=ROOT,
                check=True,capture_output=True,text=True,timeout=150)
        proof=json.loads(result.stdout)
        self.assertEqual(proof['matched_decisions'],1438)
        self.assertEqual(proof['s3_decisions'],1413)
        self.assertEqual(proof['straight_decisions'],505)
        self.assertEqual(proof['forecasts'],180)
        self.assertEqual(proof['native_worlds'],4)
        self.assertLess(proof['cpu_seconds'],100)
