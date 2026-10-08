"""Require exact #56C1 raw values and IEEE bits on both platforms, plus unchanged #56B golden."""
import hashlib
import json
from pathlib import Path
import sys
import runpy

root = Path(sys.argv[1])
repo = Path(__file__).resolve().parents[2]
windows = root / "determinism-windows-latest" / "physical-contact"
ubuntu = root / "determinism-ubuntu-latest" / "physical-contact"
for name in ("physical-contact-analysis.json", "physical-contact-bits.json"):
    a, b = (windows / name).read_bytes(), (ubuntu / name).read_bytes()
    assert a == b, f"#56C1 platform drift: {name}"
    if name == "physical-contact-analysis.json":
        assert a == (repo / "docs/calibration" / name).read_bytes().replace(b"\r\n", b"\n"), "#56C1 golden drift"
        evidence = json.loads(a)
        assert not evidence["Warnings"], evidence["Warnings"]
        assert len(evidence["RawGrid"]) == 4374
        assert set(row["Name"][0] for row in evidence["Controlled"]) >= set("ABCDEFGHIJKLMNOPQR")
    print(name, hashlib.sha256(a).hexdigest(), "exact across Windows/Ubuntu")
for platform in ("windows", "ubuntu"):
    old = root / f"determinism-{platform}-latest" / "contested-space/contested-space-racing-response.json"
    frozen = runpy.run_path(str(repo / "tools/contested-performance/compare-frozen.py"))
    frozen["compare"](frozen["read"](repo / "docs/calibration/contested-space-racing-response.json"), frozen["read"](old))
