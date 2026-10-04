"""Prove the full discovery set has exactly one stable Trait responsibility."""
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[2]
output = Path(sys.argv[1] if len(sys.argv)>1 else "test-results/discovery")
output.mkdir(parents=True, exist_ok=True)
config = json.loads((root / "tools/ci/shards.json").read_text())

def discover(shard=None):
    command=["dotnet","test",str(root/"tests/CoreSim.Tests"),"-c","Release","--no-build","--list-tests"]
    if shard: command += ["--filter", "Shard="+shard]
    result=subprocess.run(command, text=True, capture_output=True, encoding="utf-8", errors="replace")
    (output / ((shard or "full") + ".log")).write_text(result.stdout+result.stderr, encoding="utf-8")
    if result.returncode: raise RuntimeError(result.stdout+result.stderr)
    names=[line.strip() for line in result.stdout.splitlines() if line.strip().startswith("CoreSim.Tests.")]
    if not names: raise RuntimeError("No test cases discovered")
    if len(names)!=len(set(names)): raise RuntimeError("Nonunique discovery display names")
    return sorted(names)

full=discover()
shards={shard:discover(shard) for shard in config["Shards"]}
counts=Counter(name for names in shards.values() for name in names)
missing=sorted(set(full)-counts.keys()); extra=sorted(counts.keys()-set(full))
duplicates=sorted(name for name,count in counts.items() if count!=1)
report={"FullDiscovered":len(full),"UnionShards":len(counts),"Missing":missing,"Extra":extra,
    "UnintendedDuplicates":duplicates,"ShardCounts":{k:len(v) for k,v in shards.items()},"Full":full,"Shards":shards}
(output/"coverage.json").write_text(json.dumps(report,indent=2)+"\n")
print(json.dumps({k:v for k,v in report.items() if k not in ("Full","Shards")},indent=2))
assert not missing and not extra and not duplicates
