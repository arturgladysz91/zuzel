"""Verify approved extensions before restoring exact frozen #130–#132 bytes."""
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def source133(name):
    current=(ROOT/'tools/shared-world-speedway'/name).read_bytes()
    manifest=ROOT/'docs/experiments/track-awareness-anticipatory-riding/source-extensions.json'
    if not manifest.exists():return current
    item=json.loads(manifest.read_bytes())['Sources'].get(name)
    if item is None:return current
    digest=lambda b:hashlib.sha256(b).hexdigest().upper()
    if digest(current)!=item['ApprovedSha256']:raise AssertionError('Unreviewed #134 source bytes: '+name)
    restored=item['Before'].encode('utf-8')
    if digest(restored)!=item['BaseSha256']:raise AssertionError('Cannot restore exact #133: '+name)
    return restored

def source132(name):
    current=source133(name)
    manifest=ROOT/'docs/experiments/rider-decision-optimization/source-extensions.json'
    if not manifest.exists():return current
    item=json.loads(manifest.read_bytes())['Sources'].get(name)
    if item is None:return current
    digest=lambda b:hashlib.sha256(b).hexdigest().upper()
    if digest(current)!=item['ApprovedSha256']:raise AssertionError('Unreviewed #133 source bytes: '+name)
    restored=item['Before'].encode('utf-8')
    if digest(restored)!=item['BaseSha256']:raise AssertionError('Cannot restore exact #132: '+name)
    return restored

def source131(name):
    current=source132(name)
    manifest=ROOT/'docs/experiments/full-throttle-learning/source-extensions.json'
    if not manifest.exists():
        return current
    item=json.loads(manifest.read_bytes())['Sources'].get(name)
    if item is None:
        return current
    digest=lambda b:hashlib.sha256(b).hexdigest().upper()
    if digest(current)!=item['ApprovedSha256']:
        raise AssertionError('Unreviewed #132 source bytes: '+name)
    restored=item['Before'].encode('utf-8')
    if digest(restored)!=item['BaseSha256']:
        raise AssertionError('Cannot restore exact #131: '+name)
    return restored

def source130(name):
    current=source131(name)
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
