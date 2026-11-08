"""Verify hashes, isolation and optionally a fresh read-only #175 measurement."""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / 'docs/experiments/track-grip-root-cause'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def input_bytes(path, normalize_text):
    data = path.read_bytes()
    return data.replace(b'\r\n', b'\n') if normalize_text else data


def from_archive(archive, name):
    return json.loads(gzip.decompress(archive.read(name)))


def close(a, b, atol=2e-5):
    return math.isclose(a, b, rel_tol=2e-6, abs_tol=atol)


def check_fresh(fresh, old_geometry, old_rows):
    def read(name):
        return json.loads(gzip.decompress((fresh / name).read_bytes()))
    new_geometry = read('geometry.json.gz')
    new_rows = read('contact-pairs.json.gz')
    assert len(new_geometry) == len(old_geometry) and len(new_rows) == len(old_rows)
    for new, old in zip(new_geometry, old_geometry):
        assert new['Name'] == old['Name']
        assert len(new['Dense']) == len(old['Dense'])
        assert all(close(a,b) for a,b in zip(new['Starts'],old['Starts']))
        for a,b in zip(new['Dense'][::17],old['Dense'][::17]):
            assert all(close(a[k],b[k]) for k in ('K','Width'))
            for key in ('Reference','Inner','Outer','Middle'):
                assert all(close(a[key][axis],b[key][axis]) for axis in ('X','Y'))
    for new, old in zip(new_rows, old_rows):
        assert (new['Source'], new['Region'], new['StateSha256']) == (old['Source'], old['Region'], old['StateSha256'])
        for a,b in zip(new['Results'],old['Results'],strict=True):
            assert a['Id'] == b['Id']
            for key in ('RearLongitudinal','RearLateral','FrontLateral','NetTorque'):
                assert close(a['Force'][key],b['Force'][key],.002), (a['Id'],key)
        for a,b in zip(new['Forecasts'],old['Forecasts'],strict=True):
            assert a['Id'] == b['Id'] and a['FirstFailure'] == b['FirstFailure']
            assert all(close(a[k],b[k],.001) for k in ('Pose','Sweep','Beta'))
    cost = read('measure-cost.json.gz')
    assert cost['NativeWorlds'] == 0 and cost['Failure'] is None


def verify(fresh=None):
    for line in (DOC / 'SHA256SUMS').read_text(encoding='utf-8').splitlines():
        digest, name = line.split('  ', 1)
        assert sha((ROOT / name).read_bytes()) == digest, name
    inputs = json.loads((DOC / 'INPUTS.json').read_bytes())
    for entry in inputs['files']:
        name = entry['path']
        assert sha(input_bytes(ROOT / name, entry['normalize_crlf'])) == entry['sha256'], name
    summary = json.loads((DOC / 'summary175.json').read_bytes())
    assert summary['base'] == inputs['base']
    assert summary['native_worlds'] == 0 and summary['s3_new'] == 'UNKNOWN'
    assert summary['sport'] == 'NOT_TESTED'
    assert len(summary['native']) == 23 and max(q['rpm_max'] for q in summary['native']) < 12000
    assert len(summary['old_native']['168']) == 26 and len(summary['old_native']['169']) == 52
    assert any(not q['legal'] for q in summary['old_native_peaks']['169'])
    k4 = [q for q in summary['native'] if q['study'] == '172-K4']
    assert len(k4) == 5 and all(q['s3'] and q['reserve'] for q in k4)
    assert all(abs(q['ledger']['energy_J']['closure_residual']) < 3 for q in k4)
    with ZipFile(DOC / '175-dowody.zip') as z:
        rows = from_archive(z, 'contact-pairs.json.gz')
        geoms = from_archive(z, 'geometry.json.gz')
        params = from_archive(z, 'parameters.json.gz')
        assert from_archive(z, 'measure-cost.json.gz')['NativeWorlds'] == 0
    assert len(rows) == 56 and len({(q['Source'], q['Region']) for q in rows}) == 56
    assert all(len(q['StateSha256']) == 64 for q in rows)
    assert close(params['Capacities']['EffectiveFrictionCoefficient'], 1.2845)
    assert params['Physics']['Enable500CcEngine'] and params['Physics']['EnableLoadBasedTyreGrip']
    assert not params['Physics'].get('EnableDynamicAxleLoad', False)
    b,c = geoms[:2]
    assert b['Starts'] == c['Starts']
    assert all(p['Reference'] == q['Reference'] and p['K'] == q['K'] for p,q in zip(b['Dense'],c['Dense'],strict=True))
    assert close(max(q['Width'] for q in b['Dense']),16.6)
    assert close(1/max(q['K'] for q in b['Dense']),24.478396)
    for row in rows:
        assert row['ArchivedForceError'] <= .015
        result = {q['Id']:q for q in row['Results']}
        base = result['BASE']['Force']
        width = result['C-width-only']['Force']
        m4 = result['M4']['Force']
        assert base['NetForce'] == width['NetForce'] and base['NetTorque'] == width['NetTorque']
        assert m4['TyreLoads'] == base['TyreLoads'] and m4['FrontForce'] == base['FrontForce']
        for key in ('M1','M2','M3','M4','F-load-55cm'):
            q = result[key]
            f = q['Force']
            cap = f['TyreLoads']['RearCapacityNewtons']
            assert math.hypot(f['RearLongitudinal'],f['RearLateral']) <= cap + .001
            assert q['Power']['Wheel']['LongitudinalLossJoules'] >= -1e-6
            assert q['Power']['ClutchHeatJoules'] >= -1e-6
            assert abs(q['Power']['EnergyResidualJoules']) < 1e-6
        load = result['F-load-55cm']['Force']['TyreLoads']
        assert close(load['FrontNormalNewtons'] + load['RearNormalNewtons'],147*9.81)
        assert load['EffectiveFrictionCoefficient'] == base['TyreLoads']['EffectiveFrictionCoefficient']
        control = row['Forecasts'][0]
        t = row['ReviewInput']['Time']
        baseline = [p for p in control['Points'] if p['Time'] < t + .24 - 1e-8]
        assert all(abs(p['ActivationAt']-p['ObservedAt']-.24) < 1e-8 for p in row['ReviewInput']['Pending'])
        for other in row['Forecasts'][1:]:
            pre = [p for p in other['Points'] if p['Time'] < t + .24 - 1e-8]
            assert baseline == pre
        assert len(row['Forecasts']) == 4
    if fresh is not None:
        check_fresh(fresh, geoms, rows)
    return dict(archived_runs=23, paired_states=56, forecast_receipts=224,
                new_native_worlds=0, new_s3='UNKNOWN', diagnostic_replay='PASS')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fresh',type=Path)
    print(json.dumps(verify(parser.parse_args().fresh)))
