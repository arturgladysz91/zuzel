"""Require exact #56C2 structured outcomes, production runs and typed IEEE bits."""
import hashlib
from pathlib import Path
import sys
import runpy

root = Path(sys.argv[1])
minimal_yield = len(sys.argv) == 3 and sys.argv[2] == "--minimal-yield"
assert minimal_yield or len(sys.argv) == 2
repo = Path(__file__).resolve().parents[2]
for name in ('physical-contact-consequences.json', 'physical-contact-consequence-bits.json',
             'physical-contact-arithmetic.json', 'physical-contact-arithmetic-bits.json',
             *(('minimal-yield-native-b-probe.json',) if minimal_yield else ())):
    a = (root/'determinism-windows-latest'/'physical-contact-consequences'/name).read_bytes()
    b = (root/'determinism-ubuntu-latest'/'physical-contact-consequences'/name).read_bytes()
    assert a == b, f'#56C2 cross-platform drift: {name}'
    if name == 'physical-contact-consequences.json':
        if minimal_yield:
            target = runpy.run_path(str(repo / "tools/minimal-yield-audit/compatibility.py"))
            for platform in ("windows", "ubuntu"):
                directory = root / f"determinism-{platform}-latest"
                target["contact_presentation"](repo / "docs/calibration" / name,
                    directory / "physical-contact-consequences" / name,
                    directory / "physical-contact-consequences/minimal-yield-target-work.json",
                    directory / "minimal-yield-contact-consequence-changes.json.gz", consequences=True)
        else:
            assert a == (repo/'docs/calibration'/name).read_bytes().replace(b'\r\n', b'\n'), '#56C2 golden drift'
    print(name, hashlib.sha256(a).hexdigest(), 'exact across Windows/Ubuntu')
