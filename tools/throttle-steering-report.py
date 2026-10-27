"""Read saved Box2D captures; never create worlds, query forces, or select autonomous actions."""
import csv
import gzip
import hashlib
import io
import json
import math
from pathlib import Path
import shutil
import sys
import time
from functools import lru_cache

ROOT = Path(__file__).resolve().parents[1]
D = ROOT / sys.argv[1]
RAW = ROOT / sys.argv[2]
MODE = sys.argv[3]
START = time.process_time()


def read(p):
    p = Path(p)
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix == '.gz' else p.read_bytes())


def write(name, value):
    p = D / name
    p.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(value, ensure_ascii=False, indent=None if p.suffix == '.gz' else 2, allow_nan=False).encode()
    p.write_bytes(gzip.compress(data, mtime=0) if p.suffix == '.gz' else data)


def dot(a, b):
    return a['X']*b['X']+a['Y']*b['Y']


def norm(a):
    return math.hypot(a['X'], a['Y'])


def checkpoint(samples, reference):
    return next((s for s in samples if s['Input']['Rider']['TotalReferenceMeters'] >= reference-1e-8), None)


def kph(s):
    return s['Input']['Rider']['Speed']*3.6 if s else None


@lru_cache(maxsize=None)
def geometry(name):
    return read(D/(name+'-geometry.json'))


@lru_cache(maxsize=None)
def exits(name):
    return [geometry(name)['segments'][i]['startReferenceMeters'] for i in [4, 8]]


def result_files():
    return sorted(D.glob('result-*.json'))


def lock():
    results = {p.stem[7:]: read(p) for p in result_files()}
    assert all(f'{n}-{a}' in results for n in ['D0', 'D1', 'S0', 'S1'] for a in 'ABCD')
    assert not any(n.startswith('H') for n in results), 'Confirmation already opened'
    tests = []
    for n in ['D0', 'D1', 'S0', 'S1']:
        b, d = results[n+'-B'], results[n+'-D']
        corner = b['State']['Definition']['Corner']-1
        ex = exits(b['State']['Definition']['Case']['Geometry'])[corner]
        rb, rd = read(RAW/(n+'-B.json.gz')), read(RAW/(n+'-D.json.gz'))
        sb, sd = checkpoint(rb['Replay']['Samples'], ex), checkpoint(rd['Replay']['Samples'], ex)
        time_b, time_d = b['Metrics']['Native']['GateSeconds'], d['Metrics']['Native']['GateSeconds']
        passed = bool(b['Metrics']['Native']['Completed'] and d['Metrics']['Native']['Completed'] and sb and sd
                      and kph(sb) >= kph(sd)+2 and time_b is not None and time_d is not None and time_b <= time_d-.1)
        tests.append({'State': n, 'Passed': passed, 'BCompleted': b['Metrics']['Native']['Completed'],
                      'DCompleted': d['Metrics']['Native']['Completed'], 'BExitKph': kph(sb), 'DExitKph': kph(sd)})
    # Both original diagnostic families must pass; selection is additional evidence, never a repair pool.
    gate = all(x['Passed'] for x in tests)
    write('selection-lock.json', {'ConfirmationNotOpened': True, 'ProtocolCommit': 'dded3ec',
          'RuleUnchanged': True, 'PrototypeGate': gate, 'Tests': tests,
          'Decision': 'Build a separate causal prototype before confirmation' if gate else
          'No autonomous prototype: repeatable legal long full-gas benefit was not demonstrated. Keep all held factorial tests; do not tune on them.',
          'NumericalThresholds': {'ExitGainKph': 2, 'HistoricalGateGainSeconds': .1, 'DistinctFamilies': 3,
                                  'NewDnf': 0, 'SearchCpuSecondsPerReview': .163482, 'CpuSeconds': 650, 'NativeWorlds': 48}})
    print(json.dumps(read(D/'selection-lock.json'), ensure_ascii=False))


def frames(run):
    telemetry = {round(t['Time'], 8): t for t in run['Telemetry']}
    rows = []
    for sample in run['Replay']['Samples']:
        i = sample['Input']; v = i['Rider']; t = telemetry.get(round(i['Time'], 8)); f = t['Forces'] if t else None
        wheel = t['Wheel'] if t else None
        r = dict(time=i['Time'], x=v['Position']['X'], y=v['Position']['Y'], vx=v['Velocity']['X'], vy=v['Velocity']['Y'],
                 psi=v['Heading'], chi=math.atan2(v['Velocity']['Y'], v['Velocity']['X']), reference=v['TotalReferenceMeters'],
                 speed=v['Speed']*3.6, gas=i['Active']['Drive'], requestedGas=sample['RequestedDrive'],
                 steering=i['Steering']*180/math.pi, requestedSteering=sample['RequestedSteering']*180/math.pi,
                 activeSteering=i['Active']['SteeringRadians']*180/math.pi, beta=v['SlideRadians']*180/math.pi,
                 yaw=v['AngularVelocity'], omega=i['Omega'], inner=sample['Inner'], outer=sample['Outer'],
                 certificate=t['AcceptedCertificate'] if t else None)
        if f:
            loads = f['TyreLoads']; engine = f['Engine']
            r.update(frontFx=dot(f['FrontForce'], f['BodyForward']), frontFy=dot(f['FrontForce'], f['BodyLeft']),
                     rearFx=f['RearLongitudinal'], rearFy=f['RearLateral'], frontTorque=f['FrontTorque'], rearTorque=f['RearTorque'],
                     frontUse=norm(f['FrontForce'])/loads['FrontCapacityNewtons'], rearUse=norm(f['RearForce'])/loads['RearCapacityNewtons'],
                     slip=wheel['InstantSlipRatio'], longitudinalSlipVelocity=wheel['MidSlipVelocityMetersPerSecond'],
                     frontLateralVelocity=dot(f['FrontVelocity'], f['FrontLeft']), rearLateralVelocity=dot(f['RearVelocity'], f['BodyLeft']),
                     curvature=(v['Velocity']['X']*f['NetForce']['Y']-v['Velocity']['Y']*f['NetForce']['X'])/(147*max(.1,v['Speed'])**3),
                     rpm=engine['EngineRpm'], availableTorque=engine['AvailableWheelTorqueNewtonMeters'],
                     driveTorque=wheel['DriveTorqueNewtonMeters'], intervalStart=t['Time']-t['Seconds'], intervalSeconds=t['Seconds'],
                     steeringRate=abs(f['SteeringAfter']-f['SteeringBefore'])*180/math.pi/t['Seconds'])
        rows.append(r)
    return rows


def main_report():
    ledger = read(D/'attempts.json')
    assert all(x['Complete'] for x in ledger)
    assert sum(x['NativeWorlds'] for x in ledger) <= 48
    assert sum(x['CpuSeconds'] for x in ledger) <= 650
    rows, manifest, dnfs, checkpoints, effects, viewer_index = [], [], [], [], [], []
    old_manifest={x['Name']:x['Sha256'] for x in read(D/'raw-manifest.json')} if (D/'raw-manifest.json').exists() else {}
    captures = {}
    for entry in ledger:
        if not entry['NativeWorlds']:
            continue
        name = entry['Name']; path = RAW/(name+'.json.gz'); digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        assert digest == entry['RawSha256']
        dst = D/'native'/path.name; dst.parent.mkdir(exist_ok=True); shutil.copyfile(path, dst)
        manifest.append({'Name': path.name, 'Bytes': path.stat().st_size, 'Sha256': digest})
        viewer_path=D/'viewer'/(name+'.json.gz');csv_path=D/'telemetry'/(name+'.csv.gz')
        if old_manifest.get(path.name)==digest and viewer_path.exists() and csv_path.exists():
            cached=read(viewer_path);fs=cached['Frames'];run={'GeometryName':cached['Geometry'],'Metrics':cached['Metrics']}
        else:
            run = read(path); fs = frames(run)
            write('viewer/'+name+'.json.gz', {'Name': name, 'NativeRawSha256':digest,'Geometry': run['GeometryName'], 'Metrics': run['Metrics'], 'Frames': fs})
            fields = list(dict.fromkeys(k for f in fs for k in f))
            stream = io.StringIO(); writer = csv.DictWriter(stream, fields, lineterminator='\n'); writer.writeheader(); writer.writerows(fs)
            csv_path.parent.mkdir(exist_ok=True); csv_path.write_bytes(gzip.compress(stream.getvalue().encode(), mtime=0))
        captures[name] = fs
        viewer_index.append({'Name': name, 'Geometry': run['GeometryName'], 'Outcome': run['Metrics']['Native']['Outcome']})
        result_path = D/('result-'+name+'.json')
        if not result_path.exists() or name.startswith('probe-'):
            continue
        r = read(result_path); state = r['State']; definition = state['Definition']; studied = definition['Corner']; start = state['Input']['Time']
        scope = read(D/('scope-'+name+'.json')); native = r['Metrics']['Native']
        cp = []
        for corner, ex in enumerate(exits(run['GeometryName']), 1):
            for distance in [-20, -15, -10, -5, 0, 10, 20, 40, 62]:
                s = next((s for s in fs if s['reference'] >= ex+distance-1e-8), None)
                cp.append({'Corner': corner, 'Distance': distance, 'Time': s['time'] if s else None,
                           'ObservedReferenceError': s['reference']-ex-distance if s else None,
                           'BeforeIntervention': s['time'] < start if s else None, 'State': s})
        checkpoints.append({'Name': name, 'Checkpoints': cp})
        studied_exit = next(x['State'] for x in cp if x['Corner']==studied and x['Distance']==0)
        active = [f for f in fs if start+.44-1e-8 <= f['time'] < start+1.84-1e-8 and 'rearFx' in f]
        full_straight = [f for f in fs if studied_exit and studied_exit['reference'] <= f['reference'] < exits(run['GeometryName'])[studied-1]+62 and f['gas'] >= .99]
        horizon = min(fs, key=lambda x: abs(x['time']-start-.9))
        if abs(horizon['time']-start-.9) > 1e-6:
            horizon = None
        row = {'Name': name, 'Arm': r['Arm'], 'Pool': r['Pool'], 'Family': definition['Case']['Scenario']['Name'],
               'Geometry': run['GeometryName'], 'StudiedCorner': studied, 'TriggerTime': start,
               'ActualBeforeExitMeters': state['ActualBeforeExit'], 'Correction': r['Plan'],
               'Completed': native['Completed'], 'HistoricalCompleted': scope['HistoricalTwoBendsCompleted'],
               'Outcome': native['Outcome'], 'GateSeconds': scope['HistoricalGateSeconds'], 'Duration': native['DurationSeconds'],
               'CpuSeconds': native['CpuSeconds'], 'SearchCpuPerReview': None,
               'MinimumCertificate': native['MinimumClearanceMeters'], 'MaxBeta': native['MaximumBetaDegrees'],
               'MaxSpeedKph': max(f['speed'] for f in fs), 'Exit': studied_exit,
               'FullGasStraightSeconds': sum(f.get('intervalSeconds', 0) for f in full_straight),
               'Window': {'Samples': len(active), 'MeanRearFx': sum(f['rearFx'] for f in active)/len(active) if active else None,
                          'MeanRearUse': sum(f['rearUse'] for f in active)/len(active) if active else None,
                          'MaxRearUse': max((f['rearUse'] for f in active), default=None),
                          'MaxAbsSlip': max((abs(f['slip']) for f in active), default=None),
                          'MaxSteeringRate': max((f['steeringRate'] for f in active), default=None)},
               'H09': horizon, 'ExactPrefix': r['ExactPrefix'], 'ExactObservation': r['ExactObservation']}
        rows.append(row)
        if not native['Completed']:
            dnfs.append({'Name': name, 'Cause': native['Outcome'], 'HistoricalCompleted': scope['HistoricalTwoBendsCompleted'],
                         'TimeFromIntervention': fs[-1]['time']-start, 'LastNativeState': fs[-1],
                         'PreviousNativeState': fs[-2], 'StopRule': 'Original #137 guards; beta45 is experimental control boundary, not a real fall threshold.'})
    by_name = {r['Name']: r for r in rows}
    for case in ['D0','D1','S0','S1','H0','H1','H2','H3','T0']:
        if not all(case+'-'+a in by_name for a in 'ABCD'):
            continue
        group = {a: by_name[case+'-'+a] for a in 'ABCD'}
        metrics = {}
        for key in ['speed', 'yaw', 'beta', 'chi', 'certificate']:
            vals = {a: group[a]['H09'][key] if group[a]['H09'] else None for a in 'ABCD'}
            def diff(a,b): return vals[a]-vals[b] if vals[a] is not None and vals[b] is not None else None
            metrics[key] = {'H09Native': vals, 'SteeringAtFull_BminusA': diff('B','A'), 'SteeringAtPartial_DminusC': diff('D','C'),
                            'GasUnderHold_AminusC': diff('A','C'), 'GasUnderCorrection_BminusD': diff('B','D'),
                            'Interaction': (vals['B']-vals['A'])-(vals['D']-vals['C']) if all(v is not None for v in vals.values()) else None}
        effects.append({'State': case, 'Metrics': metrics, 'Basis': 'Actual accepted H09 states; signed yaw/beta/chi are not zero-valued control objectives.'})
    physical = []
    for b in rows:
        if b['Arm'] != 'B': continue
        d = by_name[b['Name'][:-1]+'D']; be,de=b['Exit'],d['Exit']
        passed = bool(b['Completed'] and d['Completed'] and be and de and be['gas']>=.99 and be['speed']>=de['speed']+2
                      and b['GateSeconds'] is not None and d['GateSeconds'] is not None and b['GateSeconds']<=d['GateSeconds']-.1)
        physical.append({'State': b['Name'][:-2], 'Family': b['Family'], 'Passed': passed,
                         'ExitGainKph': be['speed']-de['speed'] if be and de else None,
                         'HistoricalTimeGainSeconds': d['GateSeconds']-b['GateSeconds'] if b['HistoricalCompleted'] and d['HistoricalCompleted'] else None})
    families = {p['Family'] for p in physical if p['Passed']}
    legal_b = [r for r in rows if r['Arm']=='B' and r['Completed']]
    verdict = 'PHYSICS FEASIBILITY CONFIRMED' if len(families)>=3 else 'PARTIAL' if legal_b else 'PHYSICS LIMITATION'
    identical = []
    for a,b in [('H0','H1'),('H2','H3')]:
        for arm in 'ABCD':
            if a+'-'+arm in captures and b+'-'+arm in captures:
                keys=['time','x','y','vx','vy','psi','speed','gas','steering','beta','omega']
                aa,bb=captures[a+'-'+arm],captures[b+'-'+arm]
                same=len(aa)==len(bb) and all(all(x[k]==y[k] for k in keys) for x,y in zip(aa,bb))
                identical.append({'A':a+'-'+arm,'B':b+'-'+arm,'IdenticalExecutedTrajectory':same})
    offline = read(D/'offline-18-complete.json')
    evidence = {'Verdict': verdict, 'Scope': 'Finite frozen maneuvers and sampled native states only; no global impossibility claim.',
                'AutonomousPrototypeBuilt': False, 'AutonomousGo': False, 'PrototypeReason': read(D/'selection-lock.json')['Decision'],
                'NativeWorlds': sum(x['NativeWorlds'] for x in ledger), 'CpuSeconds': sum(x['CpuSeconds'] for x in ledger),
                'Contacts': sum(x['Evaluations'] for x in ledger), 'Forecasts': sum(x['Forecasts'] for x in ledger),
                'Rows': len(rows), 'DnfCount': len(dnfs), 'LegalFullCorrected': len(legal_b),
                'StrictPhysicalPassFamilies': sorted(families), 'PhysicalTests': physical, 'IdenticalGeometryTrajectories': identical,
                'Offline18': {'FixedSteeringLowerYaw': sum(r['FixedGas138']['TerminalYawCost']<r['Held']['TerminalYawCost']-1e-6 for r in offline),
                              'FixedSteeringNoSpeedLossVsHeld': sum(r['FixedGas138']['SpeedKph']>=r['Held']['SpeedKph']-1e-4 for r in offline),
                              'VariableChosenGas0': sum(r['VariableGas138']['Actions'][1]['Drive']==0 for r in offline),
                              'Original137ClosesWhileFixedGasHasSameSafetyLowerYaw': sum(r['Original137']['Actions'][1]['Drive']==0 and r['FixedGas138']['Evaluation']['Safety']<=r['Original137']['Evaluation']['Safety'] and r['FixedGas138']['TerminalYawCost']<=r['Original137']['TerminalYawCost'] for r in offline)},
                'ReportingCpuSeconds': time.process_time()-START, 'NewNativeWorldsDuringReporting':0, 'NewForceQueriesDuringReporting':0}
    write('raw-manifest.json',manifest);write('native-summaries.json',rows);write('dnf.json',dnfs);write('checkpoints.json',checkpoints)
    write('factorial-effects.json',effects);write('analysis.json',evidence);write('viewer/index.json',viewer_index)
    plots(captures, rows)
    markdown(evidence, rows, checkpoints, dnfs, offline)
    costs=read(D/'report-cost-history.json') if (D/'report-cost-history.json').exists() else [read(D/'report-cost.json')] if (D/'report-cost.json').exists() else []
    cost={'CpuSeconds':time.process_time()-START,'NativeWorlds':0,'ForceQueries':0,
          'ScriptSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper()}
    costs.append(cost);write('report-cost-history.json',costs);write('report-cost.json',cost)
    print(json.dumps({k:v for k,v in evidence.items() if k not in ['PhysicalTests','IdenticalGeometryTrajectories']},ensure_ascii=False,indent=2))


def plots(captures, rows):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    fig, axs=plt.subplots(4,1,figsize=(11,11),sharex=True)
    colors={'A':'#d46b31','B':'#136ea5','C':'#a15196','D':'#248452'}
    for arm in 'ABCD':
        name='D0-'+arm; fs=captures[name]; trigger=next(r['TriggerTime'] for r in rows if r['Name']==name)
        xx=[f['time']-trigger for f in fs if f['time']>=trigger-.2]; ys=[f for f in fs if f['time']>=trigger-.2]
        for ax,key in zip(axs,['speed','gas','yaw','beta']): ax.plot(xx,[f[key] for f in ys],label=arm,color=colors[arm])
    for ax,label in zip(axs,['Speed [km/h]','Active throttle [0..1]','Yaw rate [rad/s]','Beta [deg]']):
        ax.set_ylabel(label);ax.grid(alpha=.2);ax.axvline(.24,ls=':',c='gray');ax.axvline(.44,ls='--',c='gray');ax.legend(ncol=4)
    axs[-1].set_xlabel('Time from request [s]');fig.suptitle('D0: native factorial interventions, followed by frozen A continuation');fig.tight_layout();fig.savefig(D/'native-telemetry.svg');plt.close(fig)
    fig,axs=plt.subplots(2,2,figsize=(12,8))
    for arm in 'ABCD':
        fs=[f for f in captures['D0-'+arm] if f['time']>=next(r['TriggerTime'] for r in rows if r['Name']=='D0-'+arm) and 'rearFx'in f]
        for ax,key in zip(axs.flat,['rearFx','rearFy','frontTorque','rearTorque']):
            ax.plot([f['time'] for f in fs],[f[key] for f in fs],label=arm,color=colors[arm]);ax.set_title(key+' [N or Nm]');ax.grid(alpha=.2);ax.legend()
    fig.tight_layout();fig.savefig(D/'tyre-forces.svg');plt.close(fig)
    fig,ax=plt.subplots(figsize=(12,7));g=geometry('progressive-b')
    for edge in ['inner','outer']:
        ps=[p[edge] for s in g['segments'] for p in s['sections']];ax.plot([p['x'] for p in ps],[p['y'] for p in ps],color='#777',lw=1)
    for arm in 'ABCD':
        fs=captures['D0-'+arm];ax.plot([f['x'] for f in fs],[f['y'] for f in fs],label='D0-'+arm,color=colors[arm])
    ax.axis('equal');ax.legend();ax.set_title('Native XY traces; unchanged egg-shaped Motoarena');fig.tight_layout();fig.savefig(D/'xy-traces.svg');plt.close(fig)
    for path in D.glob('*.svg'):
        path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8')


def markdown(e, rows, cps, dnfs, offline):
    def fmt(x):return 'N/D' if x is None else f'{x:.2f}'
    lines=['# #138 — Throttle–Steering Decoupling & Controlled Slide','',f'**{e["Verdict"]} · PROVISIONAL · domyślnie OFF.**',
           '',f'Baza dokładnie `{read(D/"protocol.json")["BaseHead"]}`; protokół i progi zamrożone w `dded3ec` przed nowymi światami. Bez CEM, merge, zmian silnika, opon, aktuatora, geometrii i #16015.',
           '',f'**{e["NativeWorlds"]} nowych światów; {e["CpuSeconds"]:.6f} s CPU epizodów i wyszukiwania; {e["Contacts"]:,} ocen kontaktu; {e["Forecasts"]:,} prognoz.** Limity 48/650 s niezmienione. Analiza/serializacja/build/testy osobno.',
           '', '## Jak czytać dowód', '',
           'Poziom 1: prognozy H09, w tym 18 dawnych stanów. Poziom 2: kontrolowane interwencje Box2D z pełną późniejszą kontynuacją. Poziom 3: autonomiczny zawodnik — bramka prototypu nie została spełniona, więc prototyp nie powstał. Żaden sampled oracle nie steruje motocyklem.',
           '', 'W t0 każde ramię żąda dotychczasowego aktywnego gazu. Kierownica przygotowawcza przechodzi przez kolejkę i aktywuje się po 0,24 s. Gaz docelowy jest żądany w t+0,2 s i aktywny od t+0,44 s. Żądania interwencji trwają 1,6 s; powrót #134 A przechodzi przez tę samą kolejkę i aktywuje się od t+1,84 s. A/C mają te same kąty żądane, B/D również. B wybiera parę kątów wyłącznie z początkowej prognozy; wszystkie ramiona zachowują stare pending.',
           '', 'Natywna próba zaczyna się od historycznego fizycznego początku i biegnie ciągle przez dwa łuki oraz pełną następną prostą 62 m lub do pierwszego DNF. Historyczna bramka pełnego obrysu +1 s jest osobna. β45° jest obecną eksperymentalną obwiednią kontroli, a nie zweryfikowanym progiem upadku prawdziwego motocykla.',
           '', '## A/B/C/D — wszystkie wyniki', '',
           '|Stan / pula|Ramię|Korekta|Dwa łuki legalnie|Całe +62m|Wyjście badane km/h|Gaz wyjścia|Bramka s|Vmax km/h|Wynik|',
           '|---|---|---|---|---|---:|---:|---:|---:|---|']
    for r in rows:
        s=r['Exit'];lines.append(f'|{r["Name"]} / {r["Pool"]}|{r["Arm"]}|{r["Correction"]["Name"]}|{r["HistoricalCompleted"]}|{r["Completed"]}|{fmt(s["speed"] if s else None)}|{fmt(s["gas"] if s else None)}|{fmt(r["GateSeconds"] if r["HistoricalCompleted"] else None)}|{fmt(r["MaxSpeedKph"])}|{r["Outcome"]}|')
    lines += ['', 'A=pełny/utrzymana kierownica; B=pełny/korekta; C=31,82%/utrzymana; D=31,82%/dokładnie kąty B. Vmax sprzed interwencji może być wyższe od prędkości manewru; nie jest dowodem korzyści.',
              '', '## Punkty obu wyjść', '', '|Przejazd|Łuk|−20|−15|−10|−5|0|+10|+20|+40|+62 m km/h|','|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|']
    for group in cps:
        for corner in [1,2]:
            points=[p for p in group['Checkpoints'] if p['Corner']==corner]
            lines.append('|'+group['Name']+'|'+str(corner)+'|'+'|'.join(fmt(p['State']['speed'] if p['State'] else None) for p in points)+'|')
    lines += ['', 'Wartości pochodzą z pierwszego rzeczywiście zaakceptowanego stanu na/dalej niż dany metr. Dokładne przesunięcie pomiaru i flaga „przed interwencją” są w checkpoints.json. N/D oznacza, że świat nie dotarł legalnie do punktu; nie interpolujemy brakującego przejazdu.',
              '', '## Odpowiedzi na 20 pytań', '']
    old=read(D/'existing-18.json');o=e['Offline18']
    answers=[
        f'W #137 gaz0 wybrano {old["ZeroCorrections"]}/18 razy przy tej samej klasie H09 we wszystkich 18. Funkcja celu premiowała końcowy yaw/kierunek i ma preferencję częściowego gazu. Średnia utrata prędkości to {old["MeanSpeedCostKph"]:.2f} km/h. Te dane nie dowodzą fizycznej konieczności gaz0.',
        f'W ograniczonej prognozie sama kierownica przy gaz1 zmniejsza terminalny koszt yaw w {o["FixedSteeringLowerYaw"]}/18 stanów względem utrzymania tej samej pierwszej akcji. To poziom1, nie certyfikat długiej jazdy.',
        f'W {o["FixedSteeringNoSpeedLossVsHeld"]}/18 prognoz korekta zachowuje co najmniej prędkość utrzymanej pierwszej akcji (tolerancja0,0001km/h). Pełne pary kosztów/prędkości są w offline-18-complete.json.',
        f'B kończy legalnie długą kontynuację w {e["LegalFullCorrected"]}/{sum(r["Arm"]=="B" for r in rows)} badanych stanów. Powtórzenia profili o identycznych śladach są zależne; nie oznaczają niezależnego potwierdzenia.',
        'β, yaw, ψ i χ są zapisane osobno w każdym natywnym kroku. factorial-effects.json podaje efekty samej kierownicy, samego gazu i interakcję w H09. Mniejszy yaw bez oceny rzeczywistego kierunku χ nie stanowi sukcesu.',
        'Sprawdzenie kontrolowanego uślizgu przy pełnym gazie wymaga legalnego całego zakresu i zysku prędkości. Krótki poprawny prefiks sond B/D tego nie dowodzi; legalne przypadki i wszystkie przekroczenia obwiedni są w tabeli.',
        'Większy uślizg nie otrzymuje premii. Jego wynik określają rzeczywisty postęp, prędkość, kierunek ruchu, prześwit i późniejszy DNF. Wystąpienie dużego β przed DNF pozostaje porażką.',
        f'W {o["Original137ClosesWhileFixedGasHasSameSafetyLowerYaw"]}/18 prognoz #137 zamyka gaz mimo pełnego gazu z co najmniej tą samą klasą H09 i mniejszym kosztem yaw w nowej siatce. To dowód wpływu decyzji/rankingu lokalnego; nie dowód zbędności gaz0 w całym przejeździe. Po zamrożeniu kierownicy zmienny gaz wybiera gaz0 w {o["VariableChosenGas0"]}/18 stanów.',
        'Wcześniejsze przygotowanie jest osobnym kandydatem: kierownica aktywuje się0,20s przed gazem docelowym. Aktywny gaz w geometrycznym wyjściu podano dla każdego ramienia; nie ma reguły cornerExit→throttle1.',
        'Prędkości badanych wyjść podaje główna tabela. Oba wyjścia i stan przed interwencją są rozdzielone w tabeli punktów.',
        'Prędkości +20/+40/+62m podano dla obu łuków. Brak odczytu po DNF pozostaje N/D; krótkotrwałe duże prędkości nie są ukończeniem.',
        'Legalne przejście drugiego łuku i dodatkowej prostej mają oddzielne kolumny. Nie ma resetów prędkości, β, yaw, wheelspin, kierownicy ani kolejki.',
        'Porównanie czasu jest dopuszczalne tylko dla par legalnie kończących ten sam zakres. B nie kończy żadnego długiego przejazdu, więc korelacja jego mniejszego yaw z legalnym pełnym czasem jest nieoznaczona; nie przypisujemy jej wartości0 ani sukcesu. Ścisłe bramki B vsD są w analysis.json/PhysicalTests.',
        'Pula końcowa zawiera dwie wcześniej wydzielone rodziny V0/V1 na progressive-b/c. Dokładną równość wykonanych śladów sprawdzono; nie liczymy jej podwójnie. Opcjonalny transfer jest osobno oznaczony, jeśli wykonano.',
        'Nie zbudowano prototypu autonomicznego: zamrożona bramka diagnostyczna/selekcyjna nie została spełniona. Kontrolowane interwencje nie są polityką wyścigową.',
        'W #138 druga akcja interwencji jest rzeczywiście wykonanym żądaniem przez kolejkę, nie autonomiczną obietnicą planu. Nie ma telemetrii zgodności nowego autonomicznego prototypu, bo nie powstał. #137 replanuje i nie zobowiązuje się do drugiej akcji.',
        f'Wszystkie {e["DnfCount"]} DNF są niżej i w dnf.json wraz z ostatnimi dwoma stanami i siłami ostatniego przedziału. „uncertified interval” oznacza brak certyfikacji, nie dowiedziony kontakt z bandą.',
        'Obecne wyniki nie identyfikują globalnej wady fizyki. Kolejną empiryczną kalibrację powinny zasilać zmierzone Fx/Fy vs kąt/slip, yaw moment przedniego/tylnego kontaktu i RPM pod obciążeniem. Nie zmieniamy modelu dla uzyskania oczekiwanego rezultatu.',
        f'Łączny koszt epizodów i wszystkich nowych prognoz: {e["CpuSeconds"]:.6f}CPU-s. Pełna jednorazowa tabela to≤7par kierownicy ×2gazy, czyli14prognoz, średnio0,291667CPU-s/stan (0,140625–0,484375). Wybór B rankinguje tylko7pełnych; D ma dokładnie jego kierownicę. Nie jest to zmierzony koszt autonomicznego zawodnika. Oddzielne rezerwacje są w attempts.json, a wszystkie wykonania raportowania w report-cost-history.json.',
        'Kolejny krok: porównać mierzoną odpowiedź obu kontaktów z empirycznymi próbami motocykla i rozważyć przyczynowe przygotowanie całego stanu przed gazem. Nowy ograniczony eksperyment powinien mieć własny protokół; nie stroić na tej puli końcowej.'
    ]
    for j,a in enumerate(answers,1):lines += [f'{j}. {a}', '']
    lines += ['## Wszystkie DNF i mechanizm ostatniego przedziału','',
              '|Próba|Przyczyna|t od interwencji s|β°|yaw rad/s|Moment przód/tył Nm|Fx tył N|Fy tył N|Rear use|Slip|ω rad/s|',
              '|---|---|---:|---:|---:|---|---:|---:|---:|---:|---:|']
    for x in dnfs:
        s=x['LastNativeState'];lines.append(f'|{x["Name"]}|{x["Cause"]}|{fmt(x["TimeFromIntervention"])}|{fmt(s["beta"])}|{fmt(s["yaw"])}|{fmt(s["frontTorque"])}/{fmt(s["rearTorque"])}|{fmt(s["rearFx"])}|{fmt(s["rearFy"])}|{fmt(s["rearUse"])}|{fmt(s["slip"])}|{fmt(s["omega"])}|')
    lines += ['', 'Siły/momenty odnoszą się do ostatniego ukończonego przedziału, a stan do jego końca. Dane pozwalają oddzielić moment przedniego kontaktu od tylnego, nasycenie budżetu od niskiego Fx, wheelspin od prędkości postępowej oraz opóźnienie aktuatora. To lokalne mechanizmy badanych manewrów, nie dowód globalnej niemożliwości.',
              '', '## Artefakty i odtwarzanie','',
              '[Replay A/B/C/D i klipy](../../../tools/replay-viewer/throttle-steering.html) · [Konkretny mechanizm fizyczny](MECHANISM.md) · [Protokół](protocol.json) · [Blokada przed finałem](selection-lock.json) · [Pełne wyniki](analysis.json) · [Natywne hashe](raw-manifest.json) · [Walidacja](validation.json)',
              '', 'Pełne natywne rekordy są w native/, CSV w telemetry/, wszystkie stany i siły do viewer w viewer/. Żadne XY nie zostało wymyślone. Standardowy Windows CI końcowego HEAD jest wskazany w Draft PR.',
              '', '![Natywne prędkość/gaz/yaw/β](native-telemetry.svg)','', '![Siły i momenty opon](tyre-forces.svg)','', '![XY](xy-traces.svg)']
    (D/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')


if MODE=='lock':lock()
elif MODE=='report':main_report()
else:raise ValueError('lock|report')
