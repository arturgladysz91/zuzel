"""Rank EventPipe sampled thread stacks exported by dotnet-trace as speedscope."""
import collections
import json
import pathlib
import sys

data = json.loads(pathlib.Path(sys.argv[1]).read_text())
frames = [row["name"] for row in data["shared"]["frames"]]
inclusive = collections.Counter()
exclusive = collections.Counter()
total = 0.0
for profile in data["profiles"]:
    stack = []
    previous = profile["startValue"]
    for event in profile["events"]:
        elapsed = event["at"] - previous
        if stack and elapsed > 0:
            total += elapsed
            names = [frames[f] for f in stack if frames[f] not in ("CPU_TIME", "UNMANAGED_CODE_TIME")]
            exclusive[names[-1]] += elapsed
            for name in set(names):
                inclusive[name] += elapsed
        if event["type"] == "O":
            stack.append(event["frame"])
        else:
            assert stack.pop() == event["frame"]
        previous = event["at"]
result = {"TotalSampledThreadMilliseconds": total,
          "Note": "Sampled managed thread time includes waits; use synchronous hot stacks for CPU attribution, and measured process CPU for totals.",
          "Exclusive": [{"Method": f, "Milliseconds": t, "Percent": 100*t/total} for f,t in exclusive.most_common(40)],
          "CoreSimInclusive": [{"Method": f, "Milliseconds": t, "Percent": 100*t/total} for f,t in inclusive.most_common() if "CoreSim!" in f][:50]}
print(json.dumps(result, indent=2))
if len(sys.argv) > 2:
    pathlib.Path(sys.argv[2]).write_text(json.dumps(result, indent=2)+"\n")
