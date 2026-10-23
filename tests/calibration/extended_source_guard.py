"""Verify exact #131 extensions before reconstructing frozen #130 source bytes."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def source130(name):
    current=(ROOT/'tools/shared-world-speedway'/name).read_bytes()
    manifest=ROOT/'docs/experiments/autonomous-speedway-extended-learning/source-extensions.json'
    item=json.loads(manifest.read_bytes())['Sources'].get(name)
    if item is None:
        return current
    digest=lambda b:hashlib.sha256(b).hexdigest().upper()
    if digest(current)!=item['ApprovedSha256']:
        raise AssertionError('Unreviewed #131 source bytes: '+name)
    text=current.decode('utf-8')
    for edit in reversed(item['Edits']):
        if text.count(edit['After'])!=1:
            raise AssertionError('Nonunique reverse extension: '+name)
        text=text.replace(edit['After'],edit['Before'],1)
    restored=text.encode('utf-8')
    if digest(restored)!=item['BaseSha256']:
        raise AssertionError('Cannot restore exact #130: '+name)
    return restored
