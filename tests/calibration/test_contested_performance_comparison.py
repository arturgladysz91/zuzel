import copy
from pathlib import Path
import runpy
import tempfile
import unittest

helper = runpy.run_path(str(Path(__file__).resolve().parents[2] / "tools/contested-performance/compare-frozen.py"))

class FrozenPerformanceComparisonTests(unittest.TestCase):
    def setUp(self):
        self.before = {"Speed": "22.000000000000004", "Work": dict.fromkeys(helper["WORK_SHAPE"],10)}

    def test_only_documented_work_may_decrease(self):
        after=copy.deepcopy(self.before)
        for key in helper["REDUNDANT_WORK"]:
            after["Work"][key]=5
        self.assertEqual(3,len(helper["compare"](self.before,after)))

    def test_behavior_counter_increase_schema_and_other_work_changes_fail(self):
        for path,value in [("Speed","22.0"),("ProductionResolutions",11),("JointCombinations",9)]:
            after=copy.deepcopy(self.before)
            if path=="Speed": after[path]=value
            else: after["Work"][path]=value
            with self.assertRaises(AssertionError): helper["compare"](self.before,after)
        after=copy.deepcopy(self.before); del after["Work"]["SafetyPasses"]
        with self.assertRaises(AssertionError): helper["compare"](self.before,after)

    def test_raw_numeric_tokens_remain_distinct_from_json_strings(self):
        with tempfile.TemporaryDirectory() as directory:
            numeric=Path(directory)/"number.json"
            text=Path(directory)/"text.json"
            numeric.write_text('{"Speed":22.0}',encoding="utf-8")
            text.write_text('{"Speed":"22.0"}',encoding="utf-8")
            with self.assertRaises(AssertionError):
                helper["compare"](helper["read"](numeric),helper["read"](text))
