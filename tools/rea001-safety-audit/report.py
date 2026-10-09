"""Create new REA-001 evidence; never replace the merged readiness catalog/captures."""
import gzip
import json
from pathlib import Path
import statistics
import sys


def load(directory, filename):
    with gzip.open(Path(directory) / filename) as stream:
        return json.load(stream)


def report(before_b, before_c, after_b, after_c, perf_before, perf_after):
    cases = {}
    for configuration, before, after in (("B", before_b, after_b), ("C", before_c, after_c)):
        original = load(before, f"case-outside-7-{configuration}.json.gz")
        corrected = load(after, f"case-outside-7-{configuration}.json.gz")
        trace = load(after, f"trace-outside-7-{configuration}.json.gz")
        initial = original["Failure"]["PreFailureState"]
        changed = {}
        for path, value in initial.items():
            if path.startswith(("root.Riders", "root.Surface")):
                final_path = path.replace("root.", "root.Final.", 1)
                if trace[final_path] != value:
                    changed[path] = {"Before": value, "After": trace[final_path]}
        final = {path: value for path, value in trace.items() if path.startswith("root.Final.") and not path.startswith("root.Final.Log.")}
        cases[configuration] = dict(BeforeFailed=original["Failed"], AfterFailed=corrected["Failed"],
            BeforeFailure={key: original["Failure"][key] for key in ("Type", "Message", "StackTrace")},
            Classification=corrected["Classification"], FinalHash=corrected["FinalHash"],
            BehaviorHash=corrected["BehaviorHash"], Applied=corrected["Applied"],
            ExactChangedStateLeaves=changed, ChangedStateLeafCount=len(changed), ExactCompletedFinalStateLeaves=final,
            FinalLogCoveredByFinalHash=True)
    left = json.loads(Path(perf_before).read_text()); right = json.loads(Path(perf_after).read_text())
    performance = []
    for before, after in zip(left["Cases"], right["Cases"]):
        def measure(case):
            runs = case["Runs"]
            assert all(run["Work"] == runs[0]["Work"] and run["Operations"] == runs[0]["Operations"]
                       and run["FinalHash"] == runs[0]["FinalHash"] for run in runs)
            return dict(MedianMilliseconds=statistics.median(r["TotalMilliseconds"] for r in runs),
                MedianAllocatedBytes=statistics.median(r["AllocatedBytes"] for r in runs),
                Failed=runs[0]["Failed"], Work=runs[0]["Work"], Operations=runs[0]["Operations"],
                FinalHash=runs[0]["FinalHash"])
        b, a = measure(before), measure(after)
        if before["Id"] != "outside": assert b["FinalHash"] == a["FinalHash"]
        performance.append(dict(Id=before["Id"], Seed=before["Seed"], Configuration=before["Configuration"], Before=b, After=a,
            TimeChangePercent=None if b["Failed"] else 100 * (a["MedianMilliseconds"] / b["MedianMilliseconds"] - 1),
            AllocationChangePercent=None if b["Failed"] else 100 * (a["MedianAllocatedBytes"] / b["MedianAllocatedBytes"] - 1)))
    return dict(Baseline="350f59cc53184399066bdf93b18341e4628ec8ef", Cases=cases,
        Performance=dict(Environment=right["Environment"], Warmups=right["Warmups"], Repeats=right["Repeats"],
            CountersSeparateWarmup=right["CountersSeparateWarmup"], Cases=performance))


if __name__ == "__main__":
    output = Path(sys.argv[7])
    output.write_text(json.dumps(report(*sys.argv[1:7]), indent=2) + "\n")
