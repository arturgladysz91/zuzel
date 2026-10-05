"""Require byte-identical #56A captures across Windows and Ubuntu."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
windows = root / "determinism-windows-latest/rider-compatibility"
ubuntu = root / "determinism-ubuntu-latest/rider-compatibility"
expected = {"adaptive.json", "convergence.json", "rider-abilities-compatibility-evidence.json"}
assert {p.name for p in windows.iterdir()} == {p.name for p in ubuntu.iterdir()} == expected
for name in sorted(expected):
    assert (windows / name).read_bytes() == (ubuntu / name).read_bytes(), name
print("All #56A canonical and complete legacy heat captures match byte-for-byte across platforms.")
