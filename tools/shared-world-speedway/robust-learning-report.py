"""Aggregate frozen evidence; never starts a native world or edits129 data."""
import csv,difflib,gzip,hashlib,html,json,math,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/robust-free-racing-learning'
BASE='9470f63a11ccc31e6c7bdb862743d11e4be053d4'
def sha(b):return hashlib.sha256(b).hexdigest().upper()
def load(name):return json.loads((DATA/name).read_bytes())
def save(name,value):(DATA/name).write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def source_guard():
    sources={}
    for name in ['Program.cs','FreeLearningController.cs']:
        relative='tools/shared-world-speedway/'+name;original_raw=subprocess.check_output(['git','show',BASE+':'+relative])
        original=original_raw.decode().replace('\r\n','\n');current_raw=(ROOT/relative).read_bytes();current=current_raw.decode().replace('\r\n','\n')
        a,b=original.splitlines(keepends=True),current.splitlines(keepends=True)
        edits=[{'Before':''.join(a[i:j]),'After':''.join(b[k:l])} for tag,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes() if tag!='equal']
        restored=current
        for e in reversed(edits):assert e['After'] and restored.count(e['After'])==1;restored=restored.replace(e['After'],e['Before'],1)
        assert restored==original
        sources[name]={'BaseSha256':sha(original.encode()),'ApprovedSha256':sha(current.encode()),'BaseRawSha256':sha(original_raw),'ApprovedRawSha256':sha(current_raw),'Edits':edits}
    save('source-extensions.json',{'BaseHead':BASE,'Sources':sources,'Rule':'CLI dispatch and optional diagnostic chooser/horizon only. Main0.6s decisions and full native reaction/physics unchanged.'})
def completed(e):return sum(r['Native']['Completed'] for r in e['Results'])
def worst_time(e):return max(r['Native']['GateSeconds'] for r in e['Results']) if all(r['Native']['Completed'] for r in e['Results']) else None
def summary(e):
    return {'Id':e['Id'],'Variant':e['Variant'],'Completed':completed(e),'Cases':len(e['Results']),'WorstTime':worst_time(e),
        'MeanCompletedTime':sum(r['Native']['GateSeconds'] for r in e['Results'] if r['Native']['Completed'])/max(1,completed(e)),
        'WorstFailedProgress':min((r['Native']['LegalProgress'] for r in e['Results'] if not r['Native']['Completed']),default=None),
        'ConfirmedCrossings':sum(r['BoundaryClass']=='confirmed crossing' for r in e['Results']),
        'Uncertified':sum(r['BoundaryClass']=='uncertified interval' for r in e['Results']),
        'MinimumCertificate':min(r['Native']['MinimumClearanceMeters'] for r in e['Results']),
        'LowCompleted':sum(r['Native']['Completed'] for r in e['Results'][:2]),'MediumCompleted':sum(r['Native']['Completed'] for r in e['Results'][2:4]),'HighCompleted':sum(r['Native']['Completed'] for r in e['Results'][4:6])}
def native(raw,name):return json.loads(gzip.decompress((raw/(name+'.json.gz')).read_bytes()))
def line(points,color,width=2):return '<polyline points="'+' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}"/>'
def svg(name,title,body,height=600):
    (DATA/name).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="1100" height="{height}" viewBox="0 0 1100 {height}"><rect width="100%" height="100%" fill="#101e2a"/><g fill="#dbe8ef" font-family="Segoe UI,Arial"><text x="30" y="35" font-size="22">{html.escape(title)}</text>{body}</g></svg>',encoding='utf-8')
COLORS={'129-100':'#73dfdf','129-200':'#ffd27b','129-final':'#d99fe3','130-final':'#ff9c86'}
def figures(generations,test,groups,runs):
    body=''
    for i,(variant,gs) in enumerate(generations.items()):
        x=65+i*535;body+=f'<text x="{x}" y="80" font-size="18">{variant}: {"tylko szybkie" if variant=="A" else "mieszane"}</text>'
        for j,(label,value,upper) in enumerate([('Ukończenia /20',lambda g:sum(completed(c) for c in g['Candidates']),20),('Najlepszy czas przy4/4 [s]',lambda g:next((worst_time(c) for c in g['Candidates'] if c['Id']==g['BestId']),None),12)]):
            y=115+j*215;body+=f'<text x="{x}" y="{y}" font-size="14">{label}</text><path d="M{x},{y+160}h450 M{x},{y+20}v140" stroke="#48677a" fill="none"/>'
            for group,color in [(0,'#73dfdf'),(1,'#ffd27b')]:
                pts=[(x+g['Generation']/max(4,len(gs)-1)*450,y+160-value(g)/upper*135) for g in gs if g['Generation']%2==group and value(g) is not None]
                if pts:body+=line(pts,color)+''.join(f'<circle cx="{a}" cy="{b}" r="4" fill="{color}"/>' for a,b in pts)
    body+='<text x="35" y="565" font-size="13">Kolory to dwie cykliczne grupy stanów. A/B mają inny skład zadań; porównanie odporności odbywa się na wspólnych S.</text>'
    svg('ab-learning.svg','Kontrolowane A/B — wyłącznie wyniki natywnych epizodów',body)
    body='<text x="30" y="68" font-size="13">Politykę130 zamrożono przed testem. DNF nie ma czasu; U = brak certyfikacji, C = potwierdzone przekroczenie.</text>'
    labels=['129-100','129-200','129-final','130-final']
    for col,label in enumerate(labels):body+=f'<text x="{350+col*175}" y="105" font-size="15" fill="{COLORS[label]}">{label}</text>'
    for row,s in enumerate(load('protocol.json')['SealedTest']):
        y=135+row*35;body+=f'<text x="30" y="{y+20}" font-size="13">{s["Name"]} · {s["Speed"]}m/s · offset{s["Offset"]}m</text>'
        for col,e in enumerate(test):
            r=e['Results'][row];m=r['Native'];text=f'{m["GateSeconds"]:.2f}s' if m['Completed'] else 'DNF '+('C' if r['BoundaryClass']=='confirmed crossing' else 'U' if r['BoundaryClass']=='uncertified interval' else m['Outcome'])
            body+=f'<rect x="{330+col*175}" y="{y}" width="165" height="29" rx="4" fill="{"#264d43" if m["Completed"] else "#603d43"}"/><text x="{338+col*175}" y="{y+20}" font-size="13">{html.escape(text)}</text>'
    svg('sealed-test.svg','12 nowych scenariuszy końcowych — wszystkie wyniki',body,height=600)
    geometry=json.loads((ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json').read_bytes())
    pts=[s[k] for segment in geometry['segments'] for s in segment['sections'] for k in ['inner','outer']]
    xmin=min(p['x'] for p in pts);xmax=max(p['x'] for p in pts);ymin=min(p['y'] for p in pts);ymax=max(p['y'] for p in pts)
    body=''
    for i,group in enumerate([g for g in groups if g['Id'].startswith('T')][:4]):
        x=35+(i%2)*545;y=85+(i//2)*280;scale=min(470/(xmax-xmin),210/(ymax-ymin));tx=lambda v:x+(v-xmin)*scale;ty=lambda v:y+225-(v-ymin)*scale
        body+=f'<text x="{x}" y="{y}" font-size="15">{group["Id"]} · {html.escape(", ".join(group["Reasons"]))}</text>'
        for key,color in [('inner','#d6d9ce'),('outer','#b29878')]:body+=line([(tx(s[key]['x']),ty(s[key]['y'])) for segment in geometry['segments'] for s in segment['sections']],color,1)
        for r in group['Runs']:
            run=runs[r['Name']]['Replay'];body+=line([(tx(s['Input']['Rider']['Position']['X']),ty(s['Input']['Rider']['Position']['Y'])) for s in run['Samples']],COLORS[r['Policy']],1.8)
    body+='<text x="35" y="665" font-size="13">Turkus:129/100 · złoto:129/200 · róż:129 końcowa · koral:130. Same warunki wewnątrz panelu. Żaden ślad nie jest prognozą.</text>'
    svg('native-trajectories.svg','Rzeczywiste trajektorie, bez idealnej linii',body,height=700)
def export_bundle(raw,test):
    selected={};reasons={};scenarios=load('protocol.json')['SealedTest'];latest=test[-1]
    def add(index,reason):
        key=scenarios[index]['Name'];reasons.setdefault(key,[]).append(reason)
    improve=next((i for i,r in enumerate(latest['Results']) if r['Native']['Completed'] and (not any(e['Results'][i]['Native']['Completed'] for e in test[:3]) or r['Native']['GateSeconds']<min((e['Results'][i]['Native']['GateSeconds'] for e in test[:3] if e['Results'][i]['Native']['Completed']),default=math.inf)-.05)),None)
    if improve is not None:add(improve,'poprawa czasu lub ukończenia')
    regress=next((i for i,r in enumerate(latest['Results']) if not r['Native']['Completed'] and any(e['Results'][i]['Native']['Completed'] for e in test[:3])),None)
    if regress is not None:add(regress,'regresja wobec starszej polityki')
    if regress is None:
        time_regress=next((i for i,r in enumerate(latest['Results']) if r['Native']['Completed'] and any(e['Results'][i]['Native']['Completed'] for e in test[:3])
            and r['Native']['GateSeconds']>min(e['Results'][i]['Native']['GateSeconds'] for e in test[:3] if e['Results'][i]['Native']['Completed'])+.05),None)
        if time_regress is not None:add(time_regress,'regresja czasu względem referencji')
    legal=[i for i,r in enumerate(latest['Results']) if r['Native']['Completed']]
    if legal:add(min(legal,key=lambda i:latest['Results'][i]['Native']['MinimumClearanceMeters']),'legalna jazda blisko granicy')
    failure=next((i for i in range(12) if any(not e['Results'][i]['Native']['Completed'] for e in test)),None)
    if failure is not None:add(failure,'nieudany przejazd')
    if len(reasons)<4:add(0,'wspólna kontrola')
    if len(reasons)>4:reasons=dict(list(reasons.items())[:4])
    groups=[]
    for key,why in reasons.items():
        scenario=next(s for s in scenarios if s['Name']==key);group={'Id':key,'Reasons':why,'Scenario':scenario,'Runs':[],
            'Scope':'Nowy końcowy test. Główna certyfikacja129 i horyzont0,6s. Cztery wcześniej zamrożone polityki.'}
        for policy in ['129-100','129-200','129-final','130-final']:
            name='test-'+policy+'-'+key;selected[name]=native(raw,name);group['Runs'].append({'Name':name,'Policy':policy})
        groups.append(group)
    diagnostic={'Id':'diagnostic-14-5','Reasons':['kontrolowana zmiana jednej komendy'],'Scenario':native(raw,'diagnostic-boundary')['Replay']['Scenario'],
        'Scope':'Osobna diagnostyka, nie polityka130 ani dane treningowe: dokładniejszy certyfikat w obu próbach; w drugiej jedna komenda z horyzontu0,9s, potem oryginalne sprzężenie0,6s.','Runs':[]}
    for name,label in [('diagnostic-boundary','diag-original'),('diagnostic-one-command','diag-intervention')]:
        if (raw/(name+'.json.gz')).exists():selected[name]=native(raw,name);diagnostic['Runs'].append({'Name':name,'Policy':label})
    groups.append(diagnostic)
    if (raw/'two-bends-130-final.json.gz').exists():
        selected['two-bends-130-final']=native(raw,'two-bends-130-final');r=selected['two-bends-130-final']
        groups.append({'Id':'two-bends','Reasons':['dwa łuki bez resetu'],'Scenario':r['Replay']['Scenario'],'Runs':[{'Name':'two-bends-130-final','Policy':'130-final'}],
            'Scope':'Jeden ciągły świat. Brak resetu pozycji, koła, sterowania i kolejki pomiędzy łukami.'})
    # Trim duplicate constitutive metadata only; keep every native pose, action and measured force/wheel interval.
    for run in selected.values():
        for tick in run['Telemetry']:
            tick['Forces'].pop('Engine',None);tick['Forces'].pop('TyreLoads',None)
    payload=json.dumps({'Runs':selected},separators=(',',':'),ensure_ascii=False).encode()
    (DATA/'selected-replays.json.gz').write_bytes(gzip.compress(payload,mtime=0))
    return groups,selected
def main():
    source_guard();protocol=load('protocol.json');raw=Path(protocol['RawDirectory']);attempts=load('attempts.json')
    assert all(e['Complete'] for e in attempts),'Do not summarize a running or aborted experiment as finished.'
    test=load('test-results.json');selected=load('selected-policy.json');choice=selected['Selected'];a=load('generations-A.json');b=load('generations-B.json')
    groups,runs=export_bundle(raw,test);figures({'A':a,'B':b},test,groups,runs)
    sums=[summary(e) for e in test];latest=sums[-1];baseline=max(e['Completed'] for e in sums[:3]);post=load('post-prediction.json');det=load('determinism.json')
    grade='PASS' if latest['Completed']==12 and latest['Completed']>baseline and post['Passed'] and det['Passed'] else 'PARTIAL'
    if any(e['Metrics'] and 'invariant' in e['Metrics']['Native']['Outcome'] for e in attempts) or not det['Passed']:grade='FAIL'
    two=load('two-bend-decision.json')
    two_metrics=next((e['Metrics'] for e in attempts if e['Category']=='two-bends'),None)
    if two['Run'] and not two_metrics['Native']['Completed']:grade='PARTIAL'
    metrics=[e['Metrics'] for e in attempts if e['Metrics']]
    time_regressions=[{'Scenario':test[-1]['Scenarios'][i],'NewTime':r['Native']['GateSeconds'],
        'BestOlderTime':min(e['Results'][i]['Native']['GateSeconds'] for e in test[:3] if e['Results'][i]['Native']['Completed'])}
        for i,r in enumerate(test[-1]['Results']) if r['Native']['Completed'] and any(e['Results'][i]['Native']['Completed'] for e in test[:3])
        and r['Native']['GateSeconds']>min(e['Results'][i]['Native']['GateSeconds'] for e in test[:3] if e['Results'][i]['Native']['Completed'])+.05]
    save('analysis.json',{'Grade':grade,'BaseHead':BASE,'SelectedVariant':choice['Variant'],'SelectedId':choice['Id'],'SelectedBeforeTest':selected['TestWorldsBeforeFreeze']==0,
        'TrainingA':sum(e['Category']=='train-A' for e in attempts),'TrainingB':sum(e['Category']=='train-B' for e in attempts),
        'WorldsByCategory':{c:sum(e['NativeWorlds'] for e in attempts if e['Category']==c) for c in sorted({e['Category'] for e in attempts})},
        'NativeWorlds':sum(e['NativeWorlds'] for e in attempts),'CpuSeconds':sum(e['CpuSeconds'] for e in attempts),'ContactEvaluations':sum(e['Evaluations'] for e in attempts),
        'Forecasts':sum(e['Forecasts'] for e in attempts),'NativeSteps':sum(m['Native']['NativeSteps'] for m in metrics),'TestSummary':sums,
        'RegressionAgainstOlderCompletionCount':latest['Completed']<baseline,'BestOldCompletionCount':baseline,'BQualifiedForExtension':load('extension-decision.json')['Extend'],
        'TimeRegressionsAgainstBestOlderCase':time_regressions,
        'TwoBends':two,'TwoBendMetrics':two_metrics,'PredictionPassed':post['Passed'],'NativeDeterminism':det['Passed'],'RawDirectory':str(raw),'SelectedReplayCount':len(runs),'RawNativeReplayCount':len(metrics)})
    save('index.json',{'Grade':grade,'Groups':groups,'DefaultRun':next(r['Name'] for r in groups[0]['Runs'] if r['Policy']=='130-final'),'RawDirectory':str(raw),'AllSealedScenarios':protocol['SealedTest'],
        'Note':'Only representative native replays are in Git; all48 final metrics and all training attempts remain in aggregates. Full raw episodes require the local directory or deterministic re-execution.'})
    save('raw-manifest.json',{'Directory':str(raw),'Availability':'local only; full raw native episodes are NOT all in Git',
        'Files':{e['Name']+'.json.gz':{'Sha256':e['RawSha256'],'Bytes':e['RawBytes'],'Category':e['Category'],'NativeFingerprint':e['Metrics']['Native']['Fingerprint']} for e in attempts if e['RawSha256']}})
    with (DATA/'attempts.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.writer(f);writer.writerow(['number','name','category','native_worlds','completed','outcome','gate_s','legal_m','cpu_s','contact_evaluations','forecasts','raw_sha256'])
        for e in attempts:
            m=e['Metrics']['Native'] if e['Metrics'] else {};writer.writerow([e['Number'],e['Name'],e['Category'],e['NativeWorlds'],m.get('Completed'),m.get('Outcome'),m.get('GateSeconds'),m.get('LegalProgress'),e['CpuSeconds'],e['Evaluations'],e['Forecasts'],e['RawSha256']])
    report(load('analysis.json'),test,a,b,groups,runs)
    save('manifest.json',{p.name:sha(p.read_bytes()) for p in sorted(DATA.iterdir()) if p.is_file() and p.name not in ['manifest.json','budget.lock']})
    print(json.dumps(load('analysis.json'),ensure_ascii=False,indent=2))
def report(analysis,test,a,b,groups,runs):
    p=load('protocol.json');audit=load('audit129.json');diag=load('boundary-diagnostic.json');extension=load('extension-decision.json');cost=load('horizon-cost.json')
    choice=load('selected-policy.json')['Selected'];final_selection=load('final-selection.json');phase=load('phase1-selection.json')
    fmt=lambda v:'—' if v is None else str(v) if isinstance(v,(str,int)) else f'{v:.3f}'
    def row(label,values):return '| '+str(label)+' | '+' | '.join(fmt(v) for v in values)+' |\n'
    def evaluation_table(evaluations):
        text='| Polityka | ID | Ukończenia | Najgorszy czas przy wszystkich ukończonych [s] | Najgorszy postęp DNF [m] | Min certyfikat [m] |\n|---|---:|---:|---:|---:|---:|\n'
        for e in evaluations:
            s=summary(e);text+=row(e['Variant'],[e['Id'],f'{s["Completed"]}/{s["Cases"]}',s['WorstTime'],s['WorstFailedProgress'],s['MinimumCertificate']])
        return text
    grid='| Nowy test · v / offset | #129 po100 | #129 po200 | #129 końcowa | #130 wybrana |\n|---|---:|---:|---:|---:|\n'
    for i,s in enumerate(p['SealedTest']):
        values=[]
        for e in test:
            m=e['Results'][i]['Native'];values.append(f'{m["GateSeconds"]:.2f}s' if m['Completed'] else 'DNF: '+m['Outcome'])
        grid+=row(f'{s["Name"]} · {s["Speed"]} / {s["Offset"]}',values)
    grid+=row('Legalne ukończenia',[f'{completed(e)}/12' for e in test])
    styles='| #129 · ten sam stan9/4 | β max [°] | Droga do bramki [m] | Droga całkowita [m] | Suma zmian żądanego skrętu [°] | Zmiany ≥5° | Skrajne wagi |\n|---|---:|---:|---:|---:|---:|---|\n'
    for s in audit['Styles']:styles+=row(s['Checkpoint'],[s['Metrics']['MaximumBetaDegrees'],s['GateDistance'],s['Metrics']['DistanceMeters'],s['RequestTravelDegrees'],s['ChangesAtLeast5Degrees'],', '.join(s['Saturated']) or 'brak'])
    generations='| Wariant / generacja | Grupa cyklu | Ukończenia /20 | Najlepsza polityka | Ukończenia /4 | Najgorszy czas przy4/4 [s] | CPU generacji [s] |\n|---|---:|---:|---:|---:|---:|---:|\n'
    for gs in [a,b]:
        for g in gs:
            best=next(e for e in g['Candidates'] if e['Id']==g['BestId']);generations+=row(f'{g["Variant"]}/{g["Generation"]}',[g['Generation']%2,sum(completed(c) for c in g['Candidates']),g['BestId'],completed(best),worst_time(best),g['CpuSeconds']])
    common=next(g for g in groups if g['Id'].startswith('T'));compared=[runs[r['Name']] for r in common['Runs']]
    metrics='| Metryka · wspólny stan '+common['Id']+' | #129 po100 | #129 po200 | #129 końcowa | #130 |\n|---|---:|---:|---:|---:|\n'
    for label,key in [('Legalny postęp [m]','LegalProgress'),('Czas bramki [s]','GateSeconds'),('Prędkość wejścia [m/s]','EntrySpeed'),('Prędkość wyjścia [m/s]','ExitSpeed'),('β max [°]','MaximumBetaDegrees'),('Droga całego epizodu [m]','DistanceMeters'),('Gaz średni [0–1]','MeanDrive'),('Udział pełnego gazu [0–1]','FullDriveFraction'),('Oscylacje ≥1m, algorytm129','LargeTrajectoryOscillations'),('Min certyfikat swept [m]','MinimumClearanceMeters'),('CPU epizodu [s]','CpuSeconds')]:
        metrics+=row(label,[r['Metrics']['Native'][key] for r in compared])
    for label,key in [('Minimum v w łuku [m/s]','CornerMinimumSpeed'),('Min chwilowy prześwit kreda [m]','MinimumInstantInner'),('Min chwilowy prześwit banda [m]','MinimumInstantOuter'),('Maksimum |yaw| [rad/s]','MaximumYawRate'),('Suma zmian żądanego skrętu [°]','SteeringRequestTravelDegrees'),('Maksymalna zmiana żądania [°]','MaximumRequestChangeDegrees'),('Praca silnika [J]','EngineWorkJoules'),('Bilans strat energii [J]','DissipationJoules')]:metrics+=row(label,[r['Metrics'][key] for r in compared])
    for label,key in [('Maksimum siły przedniej opony [N]','FrontForce'),('Maksimum siły tylnej opony [N]','RearForce')]:
        metrics+=row(label,[max(math.hypot(t['Forces'][key]['X'],t['Forces'][key]['Y']) for t in r['Telemetry']) for r in compared])
    timing='| Horyzont | Obserwacje | CPU po rozgrzaniu [s] | Oceny kontaktu | Prognozy kandydatów |\n|---:|---:|---:|---:|---:|\n'
    for t in cost['Timings']:timing+=row(t['Horizon'],[t['Observations'],t['CpuSeconds'],t['Evaluations'],t['Forecasts']])
    ratio=cost['Timings'][1]['CpuSeconds']/cost['Timings'][0]['CpuSeconds'];evaluation_ratio=cost['Timings'][1]['Evaluations']/cost['Timings'][0]['Evaluations']
    references='| Nowy końcowy test | Ukończenia | Najgorszy czas przy12/12 [s] | Najgorszy postęp DNF [m] |\n|---|---:|---:|---:|\n'
    for e in test:references+=row(e['Variant'],[f'{completed(e)}/12',worst_time(e),summary(e)['WorstFailedProgress']])
    cases='| ID | v [m/s] | Offset [m] | s [m] | v boczna [m/s] | Heading [rad] | Yaw [rad/s] |\n|---|---:|---:|---:|---:|---:|---:|\n'
    for s in p['SealedTest']:cases+=row(s['Name'],[s['Speed'],s['Offset'],s['Start'],s['OutwardSpeed'],s['HeadingError'],s['YawRate']])
    phases=evaluation_table(phase);finals=evaluation_table(final_selection)
    replaylinks=' · '.join(f'[{g["Id"]}](http://127.0.0.1:8012/tools/replay-viewer/?case=robust-learning&run='+next(r['Name'] for r in g['Runs'] if r['Policy']=='130-final')+')' for g in groups if g['Id'].startswith('T'))
    early='; '.join(f'{x["Parameter"]}→{x["Value"]:.3f}: {x["Action"]["SteeringRadians"]*180/math.pi:.2f}° / gaz{x["Action"]["Drive"]:.3f}' for x in cost['EarlyAblations'])
    new_summary=analysis['TestSummary'][-1];old200=analysis['TestSummary'][1]
    two=analysis['TwoBendMetrics']
    two_detail=(f"Rzeczywisty wynik: **{two['Native']['Outcome']}**, czas epizodu{two['Native']['DurationSeconds']:.2f}s, legalny postęp{two['Native']['LegalProgress']:.2f}m, maksymalneβ{two['Native']['MaximumBetaDegrees']:.3f}°, minimalny certyfikowany prześwit{two['Native']['MinimumClearanceMeters']:.4f}m. Nie zaliczono bramki dwóch łuków i nie podano jej czasu. Przekroczenie obwiedniβ45° nie jest potwierdzonym uderzeniem w bandę ani dowodem rzeczywistego upadku; dalszy ruch wykracza poza przyjętą wiarygodność modelu." if two else "Nie utworzono świata dwóch łuków.")
    text=f'''# Robust Free Racing Learning #130 — {analysis['Grade']}

**Werdykt {analysis['Grade']}:** wybrana przed testem polityka uzyskała12/12 nowych ukończeń wobec11/12 najlepszych starszych referencji. Mieszany B nie pokonał A w selekcji, a ciągła próba dwóch łuków nie została ukończona w obwiedni wiarygodności modelu. Poprawa pojedynczego łuku jest rzeczywista; pełne trwałe umiejętności nie są jeszcze wykazane.

Status **PROVISIONAL**. Jeden zawodnik, oryginalny model Box2D, tryb uczenia domyślnie OFF. Wynik odnosi się do tej płaskiej symulacji, nie jest kalibracją prawdziwego motocykla. [Draft PR #130](https://github.com/arturgladysz91/zuzel/pull/130), bez merge, baza dokładnie `{BASE}` (#129). Dokładny nowy HEAD i standardowy Windows CI są w opisie PR.

## 1. Czy potwierdzono zapominanie?

Potwierdzono regresję generalizacji #129: stare znane holdouty zmieniły się z3/3 po100/200 na2/3 w końcu. Nie dowodzi to samo w sobie zapominania wcześniejszych zadań treningowych: #129 po awansie nigdy ich ponownie nie mierzył. Architektura dopuszczała zapominanie, ale korelacja wag, agresywności i regresji nie jest dowodem przyczynowym. Ten eksperyment używa powracających grup i osobnych wspólnych scenariuszy selekcyjnych, aby mierzyć tę hipotezę.

Wagi końcowe161 miały Direction/Throttle/Progress na maksimum i SteeringChange na minimum. Na tym samym starym stanie9m/s, offset4m obserwujemy:

{styles}
Większy uślizg i dłuższa droga współwystępują z agresywniejszymi żądaniami. Nie uznajemy samej kontry ani dużego kontrolowanego β za błąd. Suma zmian kierownicy nie jest liczbą oscylacji całej trajektorii; obie miary raportujemy osobno.

## 2. Co doprowadziło do regresji #129?

Na zapisanym DNF14/5 odtworzono dokładnie wszystkie {audit['ExactDefaultDecisions']} decyzji głównego regulatora0,6s. W1,0s krótki horyzont wybierał+1,5° i gaz0. Przy ocenie0,9s ta sama akcja była niepewna, z przyszłym prześwitem−0,341m; dostępne−1,5° i gaz0 było bezpieczne w horyzoncie, z przyszłym prześwitem+0,379m. To najwcześniejsza taka różnica w zadeklarowanym przeszukaniu zapisanych obserwacji, nie dowód globalnie najwcześniejszego zagrożenia.

Kontrolowany natywny eksperyment zmienił tylko tę jedną komendę, dopuścił ją po pełnych0,24s i potem wrócił do oryginalnego sprzężenia0,6s. Wynik: **{diag['Alternate']['Native']['GateSeconds']:.2f}s**, cały obrys i1s legalnej kontynuacji. Nie była potrzebna nowa, dokładniejsza certyfikacja w żadnym kroku tego udanego przejazdu. Jest to przyczynowy dowód osiągalnej lepszej kontynuacji z tamtego stanu. Nie dowodzi, że jeden konkretny skrajny parametr wyjaśnia całą regresję na innych stanach.

Jednoparametrowe ablacjie przy1,0s, z wartościami wag po200: {early}. To wpływ lokalny; nie przedstawiamy ich jako pełnych kontrfaktycznych przejazdów. [Audyt wszystkich obserwacji](audit129.json), [koszt i wczesne ablacjie](horizon-cost.json).

## 3. Czy DNF14m/s był rzeczywistym wyjazdem?

**W6,92s: brak certyfikacji, nie potwierdzone przecięcie bandy.** Punkt miał0,086492m prześwitu, lecz stary dolny bound przedziału−0,005703m. Diagnostyczny certyfikat tuby wokół cięciwy pozy/orientacji zachował stare odliczenia geometrii i dodał0,5mm na float. Uwzględnia granicę odchylenia |a|h²/8, odpowiednik yaw i błąd integracji obrotu, wyłącznie dla izolowanego ciała pod niezmiennym wrench w oryginalnych podkrokach. Dla tego przedziału dolna granica wynosi **{audit['LastIntervalCertificate']['Minimum']:.5f}m**.

Mały natywny test zachował bitowo identyczny stary prefiks: **{diag['OriginalPrefixBitExact']}**. Po bezpiecznie certyfikowanej kontynuacji oryginalne sterowanie doprowadziło do **potwierdzonego przekroczenia w{diag['Continued']['Native']['DurationSeconds']:.2f}s**. Stary detektor zatrzymał próbę około20ms wcześniej, ale nie wyjaśnia to niezdolności ukończenia.

Główne A/B, selekcja i wszystkie końcowe testy nadal używają dokładnie starej konserwatywnej reguły. Nie poluzowano marginesów. Klasyfikacja rozdziela potwierdzone przekroczenie (ujemny prześwit poza dodatkowym1,6mm przedziałem niepewności), brak certyfikacji, brak bezpiecznego kandydata i wyjście poza odziedziczoną obwiednię modelu β45°. Ostatnie nie jest automatycznie dowodem realnego upadku motocykla. [Natywna diagnostyka](boundary-diagnostic.json).

## 4. Czy mieszanie zwiększyło powtarzalność?

Start obu strategii: najnowsza polityka161, wybrana jako ostatni checkpoint, bez ponownego doboru według starych holdoutów. Ten sam seed1302026, CEM, osiem zakresów, ranking,0,6s, reakcja i fizyka. Każdy kandydat dostaje cztery identyczne dla konkurentów stany. Różni się tylko skład: A cztery trudne; B dwa trudne, jeden umiarkowany, jeden utrwalający. Dwie grupy powracają co drugą generację.

Po równych100+100 próbach wspólna selekcja dała: seed **{extension['SeedCompleted']}/6**, najlepszy A **{extension['ACompleted']}/6**, B **{extension['BCompleted']}/6**. B poprawił się wobec seeda, lecz sam taki remis ukończeń A/B nie dowodzi przewagi mieszania. To jeden seed i ograniczona pula stanów; model selection na S jest oceną doboru, nie niezależnym dowodem końcowym.

{phases}
## 5. Czy poprawił czas bez większej liczby niepowodzeń?

Ranking obu strategii jest identyczny: ukończenia, najgorszy legalny postęp DNF, rozkład potwierdzonych/control, najgorszy i średni czas dopiero przy wszystkich ukończeniach. Czasy w koszykach50/25ms pozwalają preferować rzeczywisty certyfikowany zapas przy podobnej szybkości, potem stan na wyjściu. Średni czas z pominięciem DNF nigdy nie jest głównym celem. Nie ma nagrody za konkretny offset ani kary za każdy skręt kontrujący.

Końcowy test wykazuje też koszt szybkości: w **{len(analysis['TimeRegressionsAgainstBestOlderCase'])}/12 przypadkach** nowa polityka jest wolniejsza o ponad0,05s od najszybszego legalnego wyniku którejś starszej referencji na tym samym przypadku. To porównanie opisowe per przypadek, nie wdrożona hybryda polityk. Pełna tabela zachowuje każdą taką regresję; poprawa ukończeń nie oznacza poprawy każdego czasu.

![Uczenie A/B](ab-learning.svg)
Surowych czasów treningowych A i B nie porównujemy jako równych zadań: ich scenariusze celowo różnią się. Wspólne S oraz T dają porównanie na tych samych warunkach. Rozszerzenie B ma większy budżet niż A, więc efekt dalszych prób nie jest czystym efektem mieszania.

## 6. Ile wykonano epizodów?

A: **{analysis['TrainingA']}**, B: **{analysis['TrainingB']}** natywnych epizodów treningowych. Faza równego A/B to100 na wariant; rozszerzenie B: {analysis['TrainingB']-100}. [Wszystkie generacje i kandydaci A](generations-A.json), [B](generations-B.json), [wszystkie rezerwacje i wyniki](attempts.csv).

Przed próbami zamrożono maksima: A100, B300, selekcja72, końcowy test48, diagnostyka8, dwa łuki3; razem531 światów, CPU2600s,38mln ocen kontaktu, epizod30CPU s, generacja150CPU s. Podstawa: #129 zużył1347,95CPU s i18,32mln ocen na442 światach. Bufory w rezerwacji pozostawiają czas na dokończenie diagnostyki i testów. Rezerwacja jest przed utworzeniem świata, także przy DNF. [Protokół](protocol.json).

{generations}
## 7. Wybór przed niezależnym testem

B zakwalifikował się do rozszerzenia: **{extension['Extend']}**, wyłącznie według S i zamrożonej reguły. W archiwum są wszystkie polityki kandydackie i osobno niezmienne referencje40/72/161. Nie ma hybrydy przełączającej polityki według prędkości. Do finałowej selekcji weszły ostatni incumbent i najlepszy archiwalny zwycięzca powracającej grupy z każdego wariantu, plus seed jako bezpieczny fallback. Poprzednie dobre polityki nie znikają przy awansie.

Wybrano **{choice['Variant']}, ID{choice['Id']}**, na sześciu S, zanim uruchomiono jakikolwiek T. Liczba światów testowych przy zapisie wyboru: **{load('selected-policy.json')['TestWorldsBeforeFreeze']}**. [Zamrożona polityka i hash](selected-policy.json), [reguła rozszerzenia](extension-decision.json), [finałowa selekcja](final-selection.json).

{finals}
## 8. Nowy niezależny test

Wszystkie12 kombinacji zapisano w protokole przed nowymi światami. Nie są dokładnymi stanami treningu, selekcji ani starymi trzema holdoutami. Żaden wynik T nie zmienia CEM, progów, shortlisty, momentu zakończenia ani polityki. Stare holdouty użyto jedynie do jawnego audytu #129.

{cases}
{grid}
![Pełny nowy test](sealed-test.svg)
## 9. Czy #129 po200 pozostaje mocnym punktem odniesienia?

Po200 uzyskał tutaj **{old200['Completed']}/12**, nowa wybrana polityka **{new_summary['Completed']}/12**. Najlepsza liczba ukończeń dowolnej starszej referencji: **{analysis['BestOldCompletionCount']}/12**. Regresja liczby ukończeń wobec starszej: **{analysis['RegressionAgainstOlderCompletionCount']}**. Rekord pojedynczego czasu nie zastępuje tego porównania. Starsze40/72/161 zostały porównane, a nie ponownie wybrane według znanych dawnych holdoutów.

{references}
## 10. Dwa kolejne łuki

Warunek był zamrożony:12/12 nowych testów bez regresji ukończeń wobec starszych. Decyzja: **{analysis['TwoBends']['Run']}** — {analysis['TwoBends']['Reason']}. Jeżeli warunek nie przeszedł, nie uruchomiono kosztownego finału. Ewentualny zapis dwóch łuków jest jednym ciągłym WorldRunner, bez resetu ciała, koła, gazu, kierownicy, kontrolera i kolejki; nie ma oddzielnych trajektorii dla zakrętów. [Decyzja](two-bend-decision.json).

{two_detail}

## 11. Płynność i realizm w modelu

Poniżej porównanie czterech rzeczywistych przejazdów z identycznego, reprezentatywnego stanu **{common['Id']}**. Dane obejmują cały epizod i1s kontroli na wyjściu; minimum v w łuku liczone jest osobno. DNF nie ma czasu bramki ani prędkości wyjścia. Prędkość wejścia różni się od prędkości początkowej, bo silnik pracuje już na dojeździe.

{metrics}
Praca silnika i straty pochodzą z każdego oryginalnego RearWheelStep i bilansu energii ciała+koła, nie z animacji. Kierownica, yaw i siły są rzeczywiście zastosowane w natywnych przedziałach. Liczba dużych zmian żądania i liczba poprzecznych nawrotów nie są równoważne; naturalna zmiana szerokości przejazdu nie dowodzi zbędnego zygzakowania. Wynik nie ustanawia globalnej optymalności ani fizycznej kalibracji do prawdziwego żużla.

## 12. Viewer i dostępność danych

Uruchom z katalogu repo `python -m http.server 8012`, potem {replaylinks}. [Oddzielna diagnostyka jednej komendy](http://127.0.0.1:8012/tools/replay-viewer/?case=robust-learning&run=diagnostic-one-command).

Wybór scenariusza zachowuje identyczny stan dla porównywanych polityk. Można niezależnie włączać ślady, zmieniać politykę i przewijać wspólny czas. Wszystkie XY i orientacje są zapisane natywnie. Panel i wykresy pokazują prędkość, oba gazy i skręty, β/ψ/χ, yaw, obie siły, energię, prześwity i pending. Statyczne nakładki przedstawiają całe zapisane przejazdy; żadna nie jest prognozą.

![Natywne nakładki](native-trajectories.svg)

W Git znajduje się **{analysis['SelectedReplayCount']} wybranych replayów w jednym pliku gzip**, protokół, wszystkie metryki, generacje, archiwa, raport i hashe. Pełne **{analysis['RawNativeReplayCount']} surowe replaye** są lokalne, nieśledzone przez Git:

`{analysis['RawDirectory']}`

[Manifest pełnych surowych danych](raw-manifest.json), [manifest plików PR](manifest.json), [indeks reprezentatywnych przejazdów](index.json). Z samego Git nie można odczytać każdego pełnego surowego zapisu. Można zweryfikować istniejący lokalny plik według SHA256 albo odtworzyć eksperyment według kodu, seeda, scenariuszy i polityk. Polityka i natywne stany są deterministyczne; CPU i skompresowane hashe z polami pomiarowymi nie muszą być identyczne na innym komputerze.

## 13. Całkowity koszt eksperymentu

**{analysis['NativeWorlds']} światów Box2D**, **{analysis['CpuSeconds']:.3f}s CPU**, **{analysis['Forecasts']:,} prognoz**, **{analysis['ContactEvaluations']:,} ocen konstytutywnych kontaktu**, **{analysis['NativeSteps']:,} kroków natywnych**. Krótkich prognoz nie policzono jako światów. Wliczono diagnostykę, osobne pomiary horyzontu i końcowe powtórzenie deterministyczności. Kompilacja, CI, agregacja, rendering i eksport po pomiarze epizodu są poza tą liczbąCPU; nie jest to całkowity czas pracy komputera.

Podział światów: `{analysis['WorldsByCategory']}`. [Analiza](analysis.json), [rezerwacje przed każdym światem](attempts.json). Powtórzone pomiary tego samego seeda pod ID archiwum i fallbacku pozostają jawnie policzone, nie znikają z kosztu.

Koszt0,9s sprawdzono offline na tych samych35 zapisanych obserwacjach po jednym wywołaniu rozgrzewającym każdej ścieżki, w kolejności0,6s potem0,9s. Stosunek CPU0,9/0,6: **{ratio:.3f}**, ocen kontaktu: **{evaluation_ratio:.3f}**. To pomiar tej konkretnej maszyny i partii, możliwe są efekty JIT/cache; nie jest to obietnica stałego kosztu produkcyjnego.

{timing}
Dokładność na27 zapisanych kontynuacjach starych trzech polityk, horyzonty0,3/0,6/0,9s: **{audit['PredictionPassed']}**. Sprawdzenie nowej zamrożonej polityki po teście: **{analysis['PredictionPassed']}**. Progi bez zmian: XY0,1m; v0,2m/s; χ/β1°; kierownica1,5°; koło2rad/s; yaw0,05rad/s; optymizm prześwitu0,1m. Powtórzenie natywnego fingerprintu: **{analysis['NativeDeterminism']}**. [Nowe kontynuacje](post-prediction.json), [deterministyczność](determinism.json).

## 14. Ograniczenia

* Jeden seed A/B i niewielka pula selekcyjna nie dają statystycznego dowodu przyczynowego przewagi mieszania. Dalszy B ma większy budżet niż A; oddzielamy to od równych początkowych100+100.
* Końcowy test jest niezależny od doboru, ale zawiera tylko12 stanów tej samej geometrii i nawierzchni. Nie dowodzi odporności na pogodę ani inny tor.
* Główna certyfikacja nadal jest konserwatywna i może odrzucić legalny przedział. Dokładniejszy dowód zastosowano tylko w małej izolowanej diagnostyce, z jawnymi założeniami. Nie podwyższano statystyki ukończeń przez poluzowanie marginesów.
* Odziedziczona obwiednia β≤45° ogranicza wiarygodność modelu; nie jest uniwersalnym fizycznym progiem upadku. Nie dodano kary za dążenie do uślizgu ani za każdą kontrę.
* Brak dowodu globalnie optymalnej linii. Pełne surowe dane są lokalne; dostępność w Git ogranicza się do jawnie wybranego podzbioru i agregatów.
* Pierwszy audyt końcowych prognoz wybrał początek łuku z mikrokroku zdarzenia krzywizny, a dziewięć końców horyzontów nie istniało w zapisanej siatce. To brak porównywalnej próbki, nie zmierzony błąd predyktora. Zachowano [pierwszy audyt](post-prediction-initial.json) i cały jego koszt. Osobny audyt wybiera tylko początki z rzeczywistymi endpointami0,3/0,6/0,9s; nie interpoluje ich ani nie zmienia progów. [Opis korekty](post-audit-alignment.json). Nie uruchomiono ponownie światów ani treningu.
* Nie uruchomiono historycznej macierzy ani rywalizacji czterech zawodników. Wyniki i protokoły #129 pozostały bez modyfikacji.

## 15. Jeden następny eksperyment

**Porównać0,6s z0,9s na tych samych mieszanych grupach treningowych i tej samej procedurze selekcji, z nowym zamrożonym testem.** Uzasadnienie jest konkretne: dłuższy horyzont wskazał wcześniejszą osiągalną alternatywę, jedna taka komenda uratowała stary DNF w natywnym świecie, a prognozy mieszczą się w ustalonych progach. Obecne główne A/B go nie zmieniało, więc wyników sposobu treningu nie pomylono z horyzontem. Cena obliczeniowa została zmierzona powyżej; pojedynczy uratowany przypadek nie gwarantuje generalizacji.

## Odtworzenie i zabezpieczenia

`dotnet build SpeedwayManager.sln -c Release --warnaserror`, następnie z katalogu repo wywołuj `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll robust-learning NOWY_KATALOG_AGREGATÓW NOWY_KATALOG_RAW ETAP` w kolejności `freeze`, `audit`, `diagnose`, `preflight`, `train`, `select`, `test`. Nie nadpisuj zamrożonych danych. Dodatkowe narzędzie `dotnet run --project tools/robust-learning-audit -c Release -- KATALOG_AGREGATÓW KATALOG_RAW cost`, potem `post`, mierzy koszty offline, powtórzenie i zgodność nowej polityki. Agregator Python nie uruchamia symulacji.

Przed nowym treningiem przeszły33 celowane testy C#, bez ostrzeżeń w buildzie. Obejmują wspólny CEM, kohorty, brak przecieku, ranking DNF, OFF, starą reakcję i ograniczenia, budżety, legalność początkową i odtworzenie decyzji #129. Pełny standardowy Windows Draft CI i dokładny commit są w PR. [Dokładne odwracalne rozszerzenia](source-extensions.json) chronią stare działanie i domyślneOFF. Nie zmieniono sił, przyczepności, silnika, masy, bezwładności, geometrii ani aktuatora.
'''
    (DATA/'README.md').write_text(text,encoding='utf-8')
if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='sources':source_guard()
    else:main()
