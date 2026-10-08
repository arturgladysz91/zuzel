"""Compare frozen behavior exactly; allow only three documented work counters to fall."""
import json
from pathlib import Path
import sys

REDUNDANT_WORK = {"ProductionResolutions", "NarrowPhaseEvaluations", "ActualProductionVerifications"}
WORK_SHAPE = {"JointCombinations", "ProductionResolutions", "NarrowPhaseEvaluations", "ResponsePasses", "Clusters",
              "UniqueRiderAlternativeProjections", "PairAlternativeChecks", "JointCombinationsScored",
              "ActualProductionVerifications", "SafetyPasses", "LegacyFallbackAttempts", "SafetyContactComponents",
              "FinalContactComponents", "SafetyJointCombinations"}

class ExactJsonFloat(str):
    """Preserve the numeric token and its JSON number type, distinct from text."""

def compare(before, after, path="root"):
    changes = []
    if isinstance(before, dict):
        assert isinstance(after, dict) and before.keys() == after.keys(), f"Schema changed: {path}"
        is_work = set(before) == WORK_SHAPE and path.endswith(".Work")
        for key in before:
            if is_work and key in REDUNDANT_WORK:
                assert type(before[key]) is int and type(after[key]) is int and 0 <= after[key] <= before[key], f"Counter increased: {path}.{key}"
                if before[key] != after[key]:
                    changes.append({"Path": path+"."+key, "Before": before[key], "After": after[key]})
            else:
                changes.extend(compare(before[key], after[key], path+"."+key))
    elif isinstance(before, list):
        assert isinstance(after,list) and len(before)==len(after), f"Length changed: {path}"
        for i,(a,b) in enumerate(zip(before,after)):
            changes.extend(compare(a,b,f"{path}[{i}]"))
    else:
        # JSON's raw numeric tokens are parsed as strings by read(): no float tolerance.
        assert type(before) is type(after) and before == after, f"Behavior changed: {path}: {before} != {after}"
    return changes

def read(path):
    # Floats retain their exact emitted text. Work counters remain integer typed.
    return json.loads(Path(path).read_text(encoding="utf-8"), parse_float=ExactJsonFloat)

if __name__ == "__main__":
    changes=compare(read(sys.argv[1]),read(sys.argv[2]))
    if len(sys.argv)>3:
        Path(sys.argv[3]).write_text(json.dumps(changes,indent=2)+"\n")
    print(f"Frozen behavior exact; {len(changes)} documented work-counter decreases.")
