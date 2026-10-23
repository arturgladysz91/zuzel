"""Saved evidence only. No simulator or native process is invoked."""
import csv
import gzip
import hashlib
import html
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs/experiments/free-racing'

def load(name):
    p = DATA / name
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix == '.gz' else p.read_bytes())

def save(name, value):
    (DATA / name).write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

def digest(data):
    return hashlib.sha256(data).hexdigest().upper()

def source_extensions():
    sources = {}
    names = ['WorldRunner.cs', 'WorldModel.cs', 'Program.cs']
    for name in names:
        path = 'tools/shared-world-speedway/' + name
        original = subprocess.check_output(['git', 'show', '9a7ba4daef3e7702e1bac3e9bc9271eedeca64df:' + path]).decode().replace('\r\n','\n')
        current = (ROOT / path).read_text(encoding='utf-8').replace('\r\n','\n')
        # SequenceMatcher stores exact, individually reversible integration edits.
        # Reversing ALL edits must recover the byte-normalized base source.
        import difflib
        a, b = original.splitlines(keepends=True), current.splitlines(keepends=True)
        edits = [{'Before': ''.join(a[i:j]), 'After': ''.join(b[k:l])}
                 for tag,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes() if tag != 'equal']
        restored = current
        for e in reversed(edits):
            assert restored.count(e['After']) == 1
            restored = restored.replace(e['After'],e['Before'],1)
        assert restored == original
        sources[name] = {'BaseSha256':digest(original.encode()), 'ApprovedSha256':digest(current.encode()), 'Edits':edits}
    save('source-extensions.json', {'BaseHead':'9a7ba4daef3e7702e1bac3e9bc9271eedeca64df', 'Sources':sources,
         'Rule':'Reverse the exact additive OFF-gated integration edits to recover #127. Old source manifests are retained.'})

def svg(name, body, width=960, height=500):
    (DATA/name).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}"><rect width="100%" height="100%" fill="#10212b"/><g font-family="Segoe UI,Arial" fill="#e0edf0">{body}</g></svg>',encoding='utf-8')

def main():
    run = load('preflight-train-16-3.json.gz'); gate=load('preflight.json'); offline=load('offline-validation.json')
    samples=run['Samples']; decisions=run['Decisions']; metrics=run['Metrics']; first_no_safe=next(d['Time'] for d in decisions if all(c['MinimumClearanceMeters']<0 for c in d['Candidates']))
    query_validation=sum(r['ForceEvaluations'] for r in offline['Records'])
    save('analysis.json', {'Grade':'PARTIAL','Gate':'STOP','NativeRuns':len(load('budget.json')),'TrainingRuns':0,'ValidationRuns':0,'FinalRuns':0,
        'LearnedPolicyExists':False,'PreflightCompleted':False,'PreflightMetrics':metrics,
        'FirstObservationWithNoPredictedLegalCandidateSeconds':first_no_safe,
        'NativeEndpointOuterClearanceMeters':samples[-1]['Outer'],
        'PredictionComparisons':len(gate['Records']),'AllAvailablePredictionComparisonsPassed':all(r['Passed'] for r in gate['Records']),
        'MissingPredictionComparisonsAfterNativeStop':2,
        'MaximumEndpointPositionErrorMeters':max(r['XY'] for r in gate['Records']),
        'MaximumAlongContinuationPositionErrorMeters':max(r['MaximumPositionErrorMeters'] for r in offline['Records']),
        'PhysicsEvaluations':{'NativeAndController':metrics['PhysicsEvaluations'],'PreflightPredictionValidation':query_validation,'IndependentOfflineAudit':offline['PhysicsEvaluations'],'TotalExperimentAndAudit':metrics['PhysicsEvaluations']+query_validation+offline['PhysicsEvaluations']},
        'CpuSeconds':{'NativeAndController':metrics['CpuSeconds'],'IndependentOfflineAudit':offline['CpuSeconds'],'InitialPredictionValidation':'not separately instrumented; no fabricated total'},
        'BestCompletedRun':None,'WorstAndOnlyRun':run['Name'],'IncompleteRuns':1,
        'HistoricalComparison':'Native #124 overlay only: different initial speed/offset (18m/s/1m vs16m/s/3m), no matched time comparison.',
        'Limitation':'Initial reactive action set plus soft finite safety cost fails before the first bend. At1.10s all candidate continuations already cross an edge; no safety certificate or fallback actuator exists. Predictor accuracy is not policy adequacy.'})
    save('initial-policy.json',{'PolicyId':0,'Policy':run['Policy'],'Trained':False})
    save('index.json',{'Grade':'PARTIAL','TrainingStopped':True,'Runs':[{'Name':run['Name'],'PolicyId':0,'Scenario':run['Scenario'],'Outcome':metrics['Outcome']}], 'MissingStages':['intermediate','learned','validation','two-bend final']})
    with (DATA/'attempts.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.writer(f);writer.writerow(['native_run','stage','policy','scenario','completed','time_s','gate_s','minimum_clearance_m','cpu_s','physics_evaluations'])
        writer.writerow([1,'preflight',0,run['Scenario']['Name'],False,metrics['DurationSeconds'],'',metrics['MinimumClearanceMeters'],metrics['CpuSeconds'],metrics['PhysicsEvaluations']])
    source_extensions()
    # Plot actual native path with immutable edges; no ideal line is introduced.
    geometry=json.loads((ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json').read_bytes())
    def path(points,x,y): return ' '.join(f'{x(p):.2f},{y(p):.2f}' for p in points)
    transform_x=lambda p:65+(p['x']+3)*11
    transform_y=lambda p:285-p['y']*11
    body='<text x="35" y="35" font-size="23">Free Racing: rzeczywista próba wstępna, STOP 1,91 s</text><text x="35" y="62" font-size="14">16 m/s, offset początkowy 3 m · 1 świat Box2D · trening nieuruchomiony</text>'
    for key,color in [('inner','#eeeecc'),('outer','#ffb36d')]:
        points=[p[key] for s in geometry['segments'] for p in s['sections'] if -3<=p[key]['x']<=65 and -14<=p[key]['y']<=13]
        body+=f'<polyline points="{path(points,transform_x,transform_y)}" fill="none" stroke="{color}" stroke-width="2"/>'
    points=[{'x':s['Input']['Rider']['Position']['X'],'y':s['Input']['Rider']['Position']['Y']} for s in samples]
    body+=f'<polyline points="{path(points,transform_x,transform_y)}" fill="none" stroke="#70e2e4" stroke-width="3"/>'
    end=points[-1];body+=f'<circle cx="{transform_x(end):.2f}" cy="{transform_y(end):.2f}" r="5" fill="#ff7777"/>'
    body+='<text x="35" y="467" font-size="15">Turkus: natywny ruch · złoty: banda · obrys przekroczył granicę przed łukiem</text>'
    svg('native-preflight.svg',body)
    body='<text x="35" y="35" font-size="23">Postęp eksperymentu: bramka zamknięta przed treningiem</text>'
    body+='<line x1="90" y1="340" x2="890" y2="340" stroke="#72919f"/><circle cx="130" cy="180" r="9" fill="#ff7777"/>'
    body+='<text x="110" y="385">Próba 1</text><text x="105" y="140">DNF · banda · 1,91 s</text>'
    body+='<text x="350" y="230" font-size="23">0 / 24 ocen treningowych</text><text x="350" y="265">Brak krzywej uczenia: nie powstała wyuczona polityka.</text>'
    body+='<text x="60" y="455">Budżet: 1 / 40 przejazdów. Pozostałych prób nie uruchomiono po nieudanej kontroli.</text>'
    svg('learning-progress.svg',body)
    body='<text x="35" y="35" font-size="23">Sterowanie w natywnej próbie 0–1,91 s</text>'
    charts=[('Prędkość m/s',lambda s:s['Input']['Rider']['Speed'],15,19),('Gaz aktywny %',lambda s:100*s['Input']['Rider']['Drive'],0,100),('Skręt wykonany °',lambda s:s['Input']['Steering']*180/3.141592653589793,-35,35),('Prześwit do bandy m',lambda s:s['Outer'],-.1,8)]
    for i,(label,value,lo,hi) in enumerate(charts):
        top=75+i*96;body+=f'<text x="20" y="{top+12}" font-size="13">{label}</text>'
        points=' '.join(f'{220+s["Input"]["Time"]/1.91*680:.2f},{top+70-(value(s)-lo)/(hi-lo)*60:.2f}' for s in samples)
        body+=f'<line x1="220" y1="{top+70}" x2="900" y2="{top+70}" stroke="#496a79"/><polyline points="{points}" fill="none" stroke="#70e2e4" stroke-width="2"/>'
    body+='<text x="220" y="488">0 s</text><text x="850" y="488">1,91 s</text>'
    svg('native-controls.svg',body)
    save('manifest.json',{p.name:digest(p.read_bytes()) for p in sorted(DATA.iterdir()) if p.is_file() and p.name not in {'manifest.json','budget.lock'}})
    print(json.dumps(load('analysis.json'),indent=2,ensure_ascii=False))

if __name__=='__main__':main()
