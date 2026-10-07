"""Require exact #56C2 structured outcomes, production runs and typed IEEE bits."""
import hashlib
from pathlib import Path
import sys

root = Path(sys.argv[1])
repo = Path(__file__).resolve().parents[2]
for name in ('physical-contact-consequences.json', 'physical-contact-consequence-bits.json',
             'physical-contact-arithmetic.json', 'physical-contact-arithmetic-bits.json'):
    a = (root/'determinism-windows-latest'/'physical-contact-consequences'/name).read_bytes()
    b = (root/'determinism-ubuntu-latest'/'physical-contact-consequences'/name).read_bytes()
    assert a == b, f'#56C2 cross-platform drift: {name}'
    if name == 'physical-contact-consequences.json':
        assert a == (repo/'docs/calibration'/name).read_bytes().replace(b'\r\n', b'\n'), '#56C2 golden drift'
    print(name, hashlib.sha256(a).hexdigest(), 'exact across Windows/Ubuntu')
