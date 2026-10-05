"""Compare new rounded geometry evidence; existing raw #54 capture stays independent."""
import json
from pathlib import Path
import sys

captures = Path(sys.argv[1])
paths = [captures / f"determinism-{os}" / "physical-space/physical-occupancy-evidence.json"
         for os in ("windows-latest", "ubuntu-latest")]
windows, ubuntu = [p.read_bytes() for p in paths]
assert windows == ubuntu, "Canonical physical-space evidence differs across platforms"
data = json.loads(windows)
assert len(data["Scenarios"]) == 12
assert data["FourRiderStartAndFirstBend"]["Work"]["RiderPairs"] == 6
assert data["FourRiderStartAndFirstBend"]["Work"]["UnresolvedIntervals"] == 0
assert data["FourRiderStartAndFirstBend"]["IncompatibleFrameIntervals"] == 0
assert data["FourRiderCompleteHeat"]["IncompatibleFrameIntervals"] == 0
assert data["FourRiderCompleteHeat"]["Work"]["UnresolvedIntervals"] == 0
assert data["MetricEmbedding"]["Closure"]["SupportsLapWrap"]
print("Physical-space canonical evidence: byte-identical Windows/Ubuntu (10 decimal presentation precision).")
