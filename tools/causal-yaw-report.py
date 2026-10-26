"""Read-only reduction of #137 native captures and frozen predictions. No simulation or tuning."""
import csv,gzip,hashlib,importlib.util,io,json,math,shutil,statistics,sys,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
D=Path(sys.argv[1]).resolve();RAW=Path(sys.argv[2]).resolve()
OUT=Path(sys.argv[3]).resolve() if len(sys.argv)>3 else D
OUT.mkdir(parents=True,exist_ok=True)
def read(p):
    with gzip.open(p,'rt',encoding='utf-8') if str(p).endswith('.gz') else open(p,encoding='utf-8') as f:return json.load(f)
def save(name,data):
    (D/name).write_text(json.dumps(data,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def zipped(p,data):
    with open(p,'wb') as f,gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as z:z.write(json.dumps(data,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def fmt(v,n=2):return 'N/D' if v is None else f'{v:.{n}f}'
def at(rows,t):return min(rows,key=lambda r:abs(r['Time']-t))
saved_argv=sys.argv;sys.argv=['legacy',str(RAW),str(OUT)]
spec=importlib.util.spec_from_file_location('metrics136',ROOT/'tools/speed-throttle-report.py');old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
sys.argv=saved_argv;old.D=D

def geometry():
    for name in ['progressive-b','progressive-c']:
        for suffix in ['-geometry.json','-viewer-geometry.json']:
            shutil.copyfile(ROOT/'docs/experiments/speed-throttle-root-cause'/(name+suffix),D/(name+suffix))
    egg=read(ROOT/'docs/experiments/multi-action-planning/egg-asymmetric-geometry.json')
    save('egg-asymmetric-viewer-geometry.json',egg)
    def pascal(v):
        if isinstance(v,dict):return {k[:1].upper()+k[1:]:pascal(x) for k,x in v.items()}
        if isinstance(v,list):return [pascal(x) for x in v]
        return v
    save('egg-asymmetric-geometry.json',pascal(egg))

def offline_audit():
    rows=[];checkpoints=[]
    for p in sorted(D.glob('full-root-traces-*.json.gz')):
        doc=read(p);name=doc['State']['Name'];t=doc['State']['Time']
        for r in doc['Rows']:
            h,c=r['Held'],r['Corrected'];rows.append({'State':name,'First':r['First'],'Second':c['Actions'][1],
                'HeldSafety':h['Evaluation']['Safety'],'CorrectedSafety':c['Evaluation']['Safety'],'HeldScore':h['Evaluation']['Score'],'CorrectedScore':c['Evaluation']['Score'],
                'HeldYaw':h['TerminalYawCost'],'CorrectedYaw':c['TerminalYawCost'],'HeldMeanYaw':h['MeanYawCost'],'CorrectedMeanYaw':c['MeanYawCost'],
                'HeldSpeedKph':h['Evaluation']['ExitSpeed']*3.6,'CorrectedSpeedKph':c['Evaluation']['ExitSpeed']*3.6,
                'HeldProgress':h['Evaluation']['Progress'],'CorrectedProgress':c['Evaluation']['Progress'],'Basis':'Same first full-throttle action and steering. Finite predicted suffix, not a native full completion.'})
            for role,key in [('held-full','HeldTrace'),('corrected-full','CorrectedTrace')]:
                for dt in [0,.2,.24,.4,.44,.6,.9]:
                    q=at(r[key],t+dt);checkpoints.append({'State':name,'Role':role,**q})
    for p in sorted(D.glob('traces-*.json.gz')):
        doc=read(p);t=doc['State']['Time']
        for trace in doc['Traces']:
            for dt in [0,.2,.24,.4,.44,.6,.9]:checkpoints.append({'State':doc['State']['Name'],'Role':trace['Role'],**at(trace['Points'],t+dt)})
    save('same-first-full-comparison.json',rows);save('yaw-checkpoints.json',checkpoints)
    with open(D/'yaw-checkpoints.csv','w',encoding='utf-8',newline='') as f:
        fields=['State','Role','Elapsed','Time','YawRate','DesiredYaw','YawError','BetaDegrees','ChiDegrees','PsiDegrees','TravelCurvature','FrontFx','FrontFy','RearFx','RearFy','Inner','Outer','FrontUtilization','RearUtilization']
        w=csv.DictWriter(f,fieldnames=fields,extrasaction='ignore',lineterminator='\n');w.writeheader();w.writerows(checkpoints)
    return rows

def export(run,name,metrics):
    old.export_csv(name,run)
    path=D/'telemetry'/(name+'.csv.gz');bytime={round(q['Time'],8):q for q in metrics['Rows']}
    text=io.StringIO(gzip.decompress(path.read_bytes()).decode());r=csv.DictReader(text);extra=['DesiredYaw','YawError','ChiDegrees','PsiDegrees','TravelCurvature','TangentAcceleration','FrontFx','PreviewReference','PreviewSegment','PreviewRadius']
    buffer=io.StringIO(newline='');w=csv.DictWriter(buffer,fieldnames=r.fieldnames+extra,lineterminator='\n');w.writeheader()
    for row in r:
        q=bytime[round(float(row['time']),8)];w.writerow({**row,**{k:q[k] for k in extra}})
    with open(path,'wb') as f,gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as z:z.write(buffer.getvalue().encode())

def native_summaries():
    summaries=[];checkpoints={};manifest=[];native=D/'native';native.mkdir(exist_ok=True)
    for p in sorted(RAW.glob('*.json.gz')):
        name=p.name[:-8];run=read(p);work=read(D/('work-'+name+'.json'));scope=read(D/('scope-'+name+'.json'))
        metrics=read(D/'native-metrics'/p.name);assert metrics['NativeRawSha256']==sha(p)
        export(run,name,metrics);shutil.copyfile(p,native/p.name);manifest.append({'Name':p.name,'Bytes':p.stat().st_size,'Sha256':sha(p)})
        marks=[old.landmarks(run,c) for c in [1,2]];points={}
        g=old.geom(run);starts=[s['StartReferenceMeters'] for s in g['Segments']]
        for corner in [1,2]:
            end=starts[4 if corner==1 else 8]
            points[str(corner)]=[{'CrossSectionReference':end+delta,'RelativeReferenceMeters':delta,'Native':old.info(old.at(run,end+delta))} for delta in [-20,-10,-5,0,10,20,40,62]]
        checkpoints[name]=points
        plans=read(D/('plans-'+name+'.json.gz'));cpus=[x['CpuSeconds'] for x in plans]
        fullrequests=sum(x['Action']['Drive']>=.99 for x in run['Replay']['Decisions'])
        rows=run['Replay']['Samples'];maximum=max(s['Input']['Rider']['Speed']*3.6 for s in rows)
        bends=[]
        for corner in [1,2]:
            begin=old.at(run,starts[1 if corner==1 else 5]);end=marks[corner-1]['Exit'];bends.append(None if begin is None or end is None else end['Time']-begin['Input']['Time'])
        summaries.append({**work,'Scope':scope,'Landmarks':marks,'BendSeconds':bends,'FullRequests':fullrequests,'MaximumObservedKph':maximum,
            'MeanDecisionCpu':work['SearchCpu']/max(1,work['Searches']),'MaximumDecisionCpu':max(cpus,default=None),
            'P95DecisionCpu':sorted(cpus)[min(len(cpus)-1,math.ceil(.95*len(cpus))-1)] if cpus else None,
            'PredictionContactsPerReview':work['PredictionContacts']/max(1,work['Searches']),
            'NonSearchCpu':work['Metrics']['Native']['CpuSeconds']-work['SearchCpu']})
    save('native-summaries.json',summaries);save('native-checkpoints.json',checkpoints);save('raw-manifest.json',manifest)
    return summaries

def comparisons(summary):
    lock=read(D/'selection-lock.json');winner=lock['Winner'];result=[]
    cases=read(D/'protocol.json')['Confirmation']
    for c in cases:
        a=next(r for r in summary if r['Name']=='final-'+c['Name']+'-A');b=next(r for r in summary if r['Name']=='final-'+c['Name']+'-'+winner)
        completed=a['Metrics']['Native']['Completed'] and b['Metrics']['Native']['Completed']
        time_gain=a['Metrics']['Native']['GateSeconds']-b['Metrics']['Native']['GateSeconds'] if completed else None
        exits=lambda r:[x['Exit']['SpeedKph'] for x in r['Landmarks'] if x['Exit'] is not None]
        exit_gain=statistics.mean(exits(b))-statistics.mean(exits(a)) if len(exits(a))==len(exits(b))==2 else None
        speed=lambda r:(r['Landmarks'][0].get('After40m') or {}).get('SpeedKph')
        straight_gain=speed(b)-speed(a) if speed(a) is not None and speed(b) is not None else None
        result.append({'Case':c['Name'],'Geometry':c['Geometry'],'A':a['Name'],'Variant':b['Name'],'Arm':winner,'TimeGainSeconds':time_gain,'MeanExitGainKph':exit_gain,'After40mGainKph':straight_gain,
                       'NewDnf':a['Metrics']['Native']['Completed'] and not b['Metrics']['Native']['Completed'],
                       'JointImprovement':completed and time_gain>=.15 and exit_gain is not None and exit_gain>=1 and straight_gain is not None and straight_gain>=2})
    save('held-comparisons.json',result);return winner,result

def drive_facts(winner):
    result=[]
    for case in ['V0-b','V1-b']:
        for arm in ['A',winner]:
            name='extended-'+case+'-'+arm;run=read(RAW/(name+'.json.gz'));samples=run['Replay']['Samples'];g=old.geom(run)
            decisions=[(d,min(samples,key=lambda s:abs(s['Input']['Time']-d['Time']))) for d in run['Replay']['Decisions']]
            for corner in [1,2]:
                end=g['Segments'][4 if corner==1 else 8]['StartReferenceMeters'];seconds=[0.,0.,0.];util=0.;slip=0.;rear_fx=0.
                for j,(a,b) in enumerate(zip(samples,samples[1:])):
                    if not end<=a['Input']['Rider']['TotalReferenceMeters']<end+62:continue
                    dt=b['Input']['Time']-a['Input']['Time'];gas=a['Input']['Active']['Drive'];seconds[0 if gas==0 else 2 if gas>=.99 else 1]+=dt
                    t=run['Telemetry'][j];util+=t['Wheel']['RearUtilization']*dt;slip+=abs(t['Wheel']['InstantSlipRatio'])*dt;rear_fx+=t['Forces']['RearLongitudinal']*dt
                duration=sum(seconds);coast=next(((d,s) for d,s in decisions if end+32<=s['Input']['Rider']['TotalReferenceMeters']<=end+62 and d['Action']['Drive']==0),None)
                full=[{'Time':d['Time'],'RelativeExitMeters':s['Input']['Rider']['TotalReferenceMeters']-end,'ActivationAt':d['Time']+.24} for d,s in decisions if end-20<=s['Input']['Rider']['TotalReferenceMeters']<end+62 and d['Action']['Drive']>=.99]
                result.append({'Name':name,'Corner':corner,'Seconds':duration,'GasZeroPartialFullSeconds':seconds,'GasZeroPartialFullFractions':[x/duration for x in seconds],
                               'MeanRearUtilization':util/duration,'MeanAbsSlipRatio':slip/duration,'MeanRearFxNewtons':rear_fx/duration,
                               'FirstCoastNearNextTurn':None if coast is None else {'Time':coast[0]['Time'],'MetersBeforeNextTurn':end+62-coast[1]['Input']['Rider']['TotalReferenceMeters']},'FullRequestsAroundExitAndStraight':full})
    save('drive-utilization.json',result);return result

def bundle(summary,winner):
    selected={};groups=[]
    names=[c['Name'] for c in read(D/'protocol.json')['Confirmation']]+['T-E','S-B-C-DNF']
    for case in names:
        failed=case=='S-B-C-DNF';variant_arm='C' if failed else winner
        names_run=[('A','select-S-B-A' if failed else 'transfer-A' if case=='T-E' else 'extended-'+case+'-A'),(variant_arm,'select-S-B-C' if failed else 'transfer-'+winner if case=='T-E' else 'extended-'+case+'-'+winner)]
        runs=[]
        for arm,name in names_run:
            selected[name]=read(RAW/(name+'.json.gz'));runs.append({'Name':name,'Policy':arm})
        doc=selected[names_run[1][1]];row=next(x for x in summary if x['Name']==names_run[1][1]);plans=read(D/('plans-'+row['Name']+'.json.gz'))
        q=next((x for x in plans if x['First']['Drive']>=.99 and (x.get('BaselineFirst') or {}).get('Drive',1)<.99),None)
        events=[]
        if q:
            events.append({'Time':q['Time'],'Label':'Nowe żądanie gaz1 odrzucone przez A w tym samym stanie','Kind':'request'})
            activation=next((x for x in doc['Replay']['Activations'] if abs(x['ObservedAt']-q['Time'])<1e-8),None)
            if activation:events.append({'Time':activation['ActivationAt'],'Label':'Rzeczywista aktywacja gaz1','Kind':'activation'})
        changes=[(a,b) for a,b in zip(doc['Replay']['Decisions'],doc['Replay']['Decisions'][1:]) if abs(a['Action']['SteeringRadians']-b['Action']['SteeringRadians'])>1e-5]
        if changes:
            correction=next(((a,b) for a,b in changes if q is None or b['Time']>q['Time']),changes[0]);events.append({'Time':correction[1]['Time'],'Label':'Nowa rzeczywista komenda kierownicy','Kind':'correction'})
        events += [{'Time':m['Exit']['Time'],'Label':'Geometryczny koniec łuku '+str(i+1),'Kind':'exit'} for i,m in enumerate(row['Landmarks']) if m['Exit']]
        if failed:events.append({'Time':doc['Replay']['Samples'][-1]['Input']['Time'],'Label':'Brak certyfikacji obrysu — DNF C','Kind':'failure'})
        groups.append({'Id':case,'Reasons':['autonomiczne A / '+variant_arm,'DNF C w selekcji' if failed else 'transfer, standardowy zakres' if case=='T-E' else 'pełna następna prosta'],'Scenario':doc['Replay']['Scenario'],
                       'Scope':('Selekcyjny C: DNF, bez ekstrapolacji.' if failed else 'Transfer egg-asymmetric: standardowe dwa łuki +1s kontroli.' if case=='T-E' else 'Historyczne dwa łuki i osobny pomiar pełnej następnej prostej.')+' Prognozowana druga akcja nie jest gwarancją jej wykonania przy kolejnym replanowaniu.',
                       'GeometryFile':doc['GeometryName']+'-viewer-geometry.json','TwoBends':True,'Runs':runs,'Events':events,'PlansFile':'plans-'+row['Name']+'.json.gz',
                       'PairedSummary':'A / '+variant_arm+'; zakres: '+('selekcja — DNF' if failed else 'transfer standardowy' if case=='T-E' else 'dwa łuki i osobny pełny pomiar prostej')+'. Wyniki: '+selected[names_run[0][1]]['Metrics']['Native']['Outcome']+' / '+doc['Metrics']['Native']['Outcome']})
    zipped(D/'selected-replays.json.gz',{'Runs':selected})
    save('index.json',{'Title':'#137 Causal Yaw & Throttle · autonomiczne wybory Native Box2D','Grade':'see report','PreferredPolicy':winner,'DefaultRun':groups[0]['Runs'][1]['Name'],
                       'Labels':{'A':'A · oryginalne #134 A #16015','B':'B · dwie akcje, yaw terminalny','C':'C · jedna akcja, yaw w czasie','D':'D · dwie akcje, yaw w czasie'},
                       'Colors':{'A':'#ffd27b','B':'#73dfdf','C':'#d99fe3','D':'#ff9c86'},'Horizons':{a:.9 for a in 'ABCD'},'Groups':groups,'ComparisonCharts':True,
                       'RawAvailability':'W Git są pełne raw wszystkich nowych światów w native/, CSV w telemetry/ i oddzielne prognozy offline. Bez wymuszonego gazu w tych przejazdach.',
                       'Charts':[['native-telemetry.svg','Prędkość, gaz, yaw, beta, opony i prześwity względem drogi'],['yaw-diagnostic.svg','Oddzielne prognozy yaw — stała komenda i przyczynowa korekta']]})

def plots(winner):
    import matplotlib
    matplotlib.use('Agg');import matplotlib.pyplot as plt
    plt.rcParams['svg.hashsalt']='causal-yaw-137'
    fig,axes=plt.subplots(10,2,figsize=(14,25),sharex='col')
    titles=['km/h','Gaz aktywny','Gaz żądany','Yaw rad/s','Błąd yaw rad/s','Beta deg','Kierownica deg','Prześwit m','Siły opon N','Wykorzystanie kontaktu']
    for col,case in enumerate(['V0-b','V0-c']):
        for arm,color in [('A','#9a6400'),(winner,'#007c91')]:
            name='extended-'+case+'-'+arm;run=read(RAW/(name+'.json.gz'));derived=read(D/'native-metrics'/(name+'.json.gz'));ss=run['Replay']['Samples'];xx=[s['Input']['Rider']['TotalReferenceMeters'] for s in ss]
            tt=run['Telemetry'];values=[[s['Input']['Rider']['Speed']*3.6 for s in ss],[s['Input']['Active']['Drive'] for s in ss],[s['RequestedDrive'] for s in ss],
                [s['Input']['Rider']['AngularVelocity'] for s in ss],[x['YawError'] for x in derived['Rows']],[s['Input']['Rider']['SlideRadians']*180/math.pi for s in ss],
                [s['Input']['Steering']*180/math.pi for s in ss],[min(s['Inner'],s['Outer']) for s in ss]]
            for i,y in enumerate(values):axes[i,col].plot(xx,y,color=color,label=arm,lw=.8,drawstyle='steps-post' if i in [1,2] else 'default')
            axes[8,col].plot(xx[1:],[t['Forces']['RearLongitudinal'] for t in tt],color=color,lw=.8,label=arm+' rear Fx')
            axes[8,col].plot(xx[1:],[t['Forces']['RearLateral'] for t in tt],color=color,lw=.7,ls='--',label=arm+' rear Fy')
            axes[8,col].plot(xx[1:],[t['Forces']['FrontLateral'] for t in tt],color=color,lw=.6,ls=':',label=arm+' front Fy')
            axes[9,col].plot(xx[1:],[t['Wheel']['RearUtilization'] for t in tt],color=color,lw=.8,label=arm+' rear')
            axes[9,col].plot(xx[1:],[old.length(t['Forces']['FrontForce'])/t['Forces']['TyreLoads']['FrontCapacityNewtons'] for t in tt],color=color,lw=.7,ls='--',label=arm+' front')
        for i,ax in enumerate(axes[:,col]):ax.set_ylabel(titles[i]);ax.grid(alpha=.2);ax.legend(fontsize=6,loc='best')
        axes[0,col].set_title('progressive-'+case[-1]);axes[-1,col].set_xlabel('Natywny postęp referencyjny toru [m]')
    fig.tight_layout();fig.savefig(D/'native-telemetry.svg',metadata={'Date':None});fig.savefig(OUT/'native-137.png',dpi=120);plt.close(fig)
    fig,axes=plt.subplots(3,2,figsize=(13,8),sharex=True)
    for col,case in enumerate(['D-B-exit1-10','D-C-exit1-10']):
        doc=read(D/('full-root-traces-'+case+'.json.gz'));r=doc['Rows'][0]
        for key,label,color in [('HeldTrace','held full','#a56600'),('CorrectedTrace','same first + correction','#007c91')]:
            q=r[key];x=[p['Elapsed'] for p in q]
            axes[0,col].plot(x,[p['YawRate'] for p in q],label=label,color=color);axes[0,col].plot(x,[p['DesiredYaw'] for p in q],color=color,ls=':',lw=.8)
            axes[1,col].plot(x,[p['BetaDegrees'] for p in q],color=color);axes[2,col].plot(x,[p['Drive'] for p in q],color=color,drawstyle='steps-post')
        axes[0,col].set_title(case+' · PROGNOZA');axes[0,col].legend(fontsize=7)
        for i,label in enumerate(['Yaw / desiredYaw rad/s','Beta deg','Gaz aktywny']):axes[i,col].set_ylabel(label);axes[i,col].grid(alpha=.2)
        axes[2,col].set_xlabel('Czas od przewidywanej obserwacji [s]')
    fig.tight_layout();fig.savefig(D/'yaw-diagnostic.svg',metadata={'Date':None});plt.close(fig)
    for name in ['native-telemetry.svg','yaw-diagnostic.svg']:
        p=D/name;p.write_text('\n'.join(x.rstrip() for x in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')

def report(summary,corrections,winner,pairs):
    complete=read(D/'complete.json');threshold=read(D/'thresholds.json');offline=read(D/'offline-complete.json');controls=read(D/'controlled.json');selection=read(D/'selection.json')
    reduced=[r for r in corrections if r['CorrectedYaw']<r['HeldYaw']-1e-6 and r['CorrectedSafety']<=r['HeldSafety']]
    final=[r for r in summary if r['Name'].startswith('final-')];chosen=[r for r in final if r['Arm']==winner]
    mean_cpu=sum(r['SearchCpu'] for r in chosen)/sum(r['Searches'] for r in chosen)
    fullgeometries={r['Case']['Geometry'] for r in chosen if r['FullRequests']>0 and any(m['Exit'] and m['Exit']['Active']>=.99 for m in r['Landmarks'])}
    gates={'JointThreeHeldCases':sum(r['JointImprovement'] for r in pairs)>=3,'TwoHeldGeometries':len({r['Geometry'] for r in pairs if r['JointImprovement']})>=2,
           'NoNewHeldDnf':not any(r['NewDnf'] for r in pairs),'AutonomousFullExitBothProfiles':len(fullgeometries)>=2,
           'MeanSearchCpu':mean_cpu<=threshold['MaximumMeanDecisionCpu'],'FourRiderCpuProjection':20*mean_cpu<=4,
           'PredictionPreflight':read(D/'preflight.json')['Passed']}
    some_gain=any(r['TimeGainSeconds'] is not None and r['TimeGainSeconds']>=.02 for r in pairs)
    verdict='GO' if all(gates.values()) else 'PARTIAL' if some_gain and gates['NoNewHeldDnf'] and gates['MeanSearchCpu'] else 'FAIL'
    save('analysis.json',{'Verdict':verdict,'Gates':gates,'Winner':winner,'SameFirstFullYawReductions':len(reduced),'FullRootComparisons':len(corrections),'MeanHeldDecisionCpu':mean_cpu,'FourRiderSearchCpuPerSimSecond':20*mean_cpu})
    drive=read(D/'drive-utilization.json')
    rows=['# #137 — Causal Yaw & Throttle Continuation','',f'**{verdict} · PROVISIONAL, domyślnie OFF.** Baza `{BaseHead}`. Oryginalne fizyczne źródła, polityka #16015, tor i zabezpieczenia bez zmian. Bez CEM i bez merge.','',
          f'Zamrożona selekcja wybrała **{winner}**. Porównanie finałowe obejmuje4 wcześniej zamknięte pary stan/geometria, a ich przedłużenia nie są kolejnymi niezależnymi przypadkami. Wyniki poniżej rozróżniają prognozę, kontrolowaną sekwencję diagnostyczną i samodzielne replanowanie.','',
          'Cztery held przypadki to tylko DWIE rodziny fizycznych początków, każda na B/C. Nie traktujemy ich jako czterech niezależnych zawodników. W badanych śladach wewnętrznych oba profile dały identyczne czasy; szerszy zewnętrzny brzeg nie musiał zostać wykorzystany.','',
          '## Odpowiedzi 1–6: co robi koszt yaw?','',
          '1. Duży koszt wynika z `YawWeight*(r−desiredYaw)^2*10` na końcu utrzymanej komendy H09. Gaz wpływa na oryginalny rozkład Fx/Fy i moment tylnej opony; nie jest wyłącznie wejściem przyspieszenia. Ten koszt nie jest osobnym twardym ograniczeniem yaw.','',
          '2. Dotychczasowy predyktor prawidłowo prognozuje fizykę UTRZYMANIA komendy. Nie przewiduje sam z siebie kolejnej korekty. #137 porównuje inną fizyczną sekwencję, a nie naprawia równania Box2D. W czterech preflightach wspólny prefiks i monolityczny causalPolicy mają identyczne XY; rozbieżność Native jest poniżej0,00013m.','',
          f'3. Przy tej samej pierwszej akcji gaz1 i tej samej kierownicy korekta zmniejsza terminalny koszt yaw w **{len(reduced)}/{len(corrections)}** skończonych porównań, bez pogorszenia klasy H09. Nie jest to globalny dowód osiągalności. Pełny r/desiredYaw/beta/chi/psi/krzywizna/Fx/Fy/utilization przy0/.2/.24/.4/.44/.6/.9s: [yaw-checkpoints.csv](yaw-checkpoints.csv), z jawnym rozdzieleniem prognoz i native.','',
          'Cztery natywne sondy z pełnym gazem i korektą potwierdzają tylko H09. W sześciu innych stanach wybranych do pełnych24 kontynuacji A/B/C/D wygrały w praktyce te same komendy częściowego gazu; nie liczymy tych ukończeń jako24 dowodów korzyści z różnych korekt. [sampled-oracle.json](sampled-oracle.json) jest wyłącznie retrospektywnym minimum tej skończonej próbki, nie sterownikiem.','',
          '4. Dwie komendy wystarczają do sprawdzenia części lokalnych korekt. Ich aktywacje to+.24/+.44s; w+.2s pierwsza nadal oczekuje. Wszystkie stare pending pozostają. Wspólny fizyczny prefiks liczymy raz przez oryginalny Predict, następnie≤5 korzeni ×≤7 korekt. To ograniczony zbiór, nie przeszukiwanie całej przestrzeni.','',
          '5. C/D używają trapezoidalnej średniej błędu yaw w całym H09, w tych samych jednostkach i z tą samą wagą. Stały błąd ma dokładnie dawny koszt. To porównanie wariantów kosztu, bez założenia, że średnia jest lepszą miarą bezpieczeństwa; późny groźny stan może dostać mniejszy koszt średni. W selekcji C ma dwa DNF (brak certyfikacji), a D przejeżdża wolniej od A i B. Nie potwierdzono przewagi uśredniania yaw.','',
          f'6. Wyniki autonomicznej selekcji wybrały {winner} regułą niezawodność→średni legalny czas→koszt CPU→nazwa. C i D nie miały zapewnionej wygranej. Dane wszystkich A/B/C/D pozostają w raporcie.','',
          '|Diagnoza offline|A: held/terminal|B:2/terminal|C:held/średnia|D:2/średnia|','|---|---:|---:|---:|---:|',
          '|Pierwsze żądanie gaz1 w18stanach|'+'|'.join(str(sum(x['Best'][a]['Actions'][0]['Drive']>=.99 for x in offline)) for a in 'ABCD')+'|','',
          'Koszt throttle i zmiany skrętu uwzględnia obie planowane komendy, ważone czasem aktywnego działania w[.24,.9]. Identyczne żądania kanału są scalane: hold→hold ma dokładnie stary koszt. Druga komenda nie jest darmowa. Pozostałe koszty, normalizacja postępu0,6/0,9 i kolejność klas bezpieczeństwa pozostają oryginalne. Nie ma premii za gaz1, małego beta jako celu ani nowego twardego progu yaw.','',
          '## Samodzielna selekcja i finał','',
          '|Przejazd|Ramię|Dwa łuki s|Łuk1 s|Łuk2 s|Gaz1 żądania|Gaz wyjście1/2|Vmax obserwowane km/h|Wynik|','|---|---|---:|---:|---:|---:|---:|---:|---|']
    for r in summary:
        if r['Name'].startswith(('select-','final-','transfer-')):
            gas='/'.join(fmt(m['Exit']['Active'] if m['Exit'] else None) for m in r['Landmarks'])
            rows.append(f"|{r['Name']}|{r['Arm']}|{fmt(r['Metrics']['Native']['GateSeconds'])}|{fmt(r['BendSeconds'][0])}|{fmt(r['BendSeconds'][1])}|{r['FullRequests']}|{gas}|{r['MaximumObservedKph']:.2f}|{r['Metrics']['Native']['Outcome']}|")
    rows+=['','## Odpowiedzi 7–16: prędkość i legalna kontynuacja','',
           '7. B samodzielnie żąda gaz1 częściej: w normalnych held V0 są3 żądania wobec0 dla A; w V1 są3 wobec2. W przedłużeniu V1 B dochodzi do4 żądań. To nie jest wymuszony gaz.','',
           '8–9. Częstsze żądania NIE dają pełnego gazu na geometrycznym wyjściu. Na obu wyjściach wszystkich czterech held A i B mają aktywne około31,82%. Planowana druga komenda jest jedynie prognozą: następny rzeczywisty review ponownie obserwuje i wybiera. Nie twierdzimy, że każda zapowiedziana korekta została wykonana. Dokładne czasy i miejsca otwarcia: [drive-utilization.json](drive-utilization.json), [native-checkpoints.json](native-checkpoints.json).','',
           '10–12. B nie przyspiesza pełnym gazem przez całą dostępną prostą. Maksimum w normalnym dwubendowym held wynosi76,56–76,85km/h, a po przedłużeniu maksimum B to83,43km/h. D osiągnął89,38km/h w selekcji, lecz był wolniejszy całościowo. Żaden nowy autonomiczny wariant nie osiągnął100km/h. Osiągi napędu z izolowanej próby #136 nie zostały wykorzystane: na wyjściu pozostaje częściowy gaz, potem występuje odpuszczenie przed następnym łukiem. Poniżej zmierzony podział czasu i wykorzystanie tylnego kontaktu na obu prostych.','',
           '|Przejazd / prosta po łuku|Czas gaz0 s|Czas częściowy s|Czas pełny s|Średnie rear utilization|Średni abs slip ratio|Pierwszy coast przed następnym łukiem m|',
           '|---|---:|---:|---:|---:|---:|---:|']
    for x in drive:
        rows.append(f"|{x['Name']} / {x['Corner']}|"+'|'.join(fmt(v) for v in x['GasZeroPartialFullSeconds'])+f"|{x['MeanRearUtilization']:.3f}|{x['MeanAbsSlipRatio']:.3f}|{fmt((x['FirstCoastNearNextTurn'] or {}).get('MetersBeforeNextTurn'))}|")
    rows += ['',
           'W tej próbie nie wykazano, że stale nasycona opona uniemożliwia100km/h: średnie wykorzystanie kontaktu na prostej trzeba czytać razem z częściowym gazem. Nie mamy globalnego dowodu bezpiecznego wejścia w następny łuk przy100km/h. Rozdzielamy niewykorzystany napęd i konserwatywne/lokalne sterowanie od niepotwierdzonej osiągalności szybkiej całej trajektorii.','',
           '13. Każde przekroczenie beta45, granicy obrysu, utrata certyfikacji, reverse/projection i fizycznych zakresów aktuatora zatrzymuje świat. Duże yaw samo nie jest DNF. Pełny gaz przy DNF nie stanowi sukcesu. [dnf.json](dnf.json) zawiera każdy stan końcowy i przyczynę, także po historycznym ukończeniu dwóch łuków.','',
           '14–15. Czasy obu łuków są oddzielone powyżej. Poniższe porównanie jest niezależną pulą, nie najlepszym przypadkiem wybranym po wyniku.','',
           '|Held przypadek|A−nowy czas s|Średni zysk na2wyjściach km/h|Zysk po40m km/h|Wspólne kryteria poprawy|Nowy DNF|','|---|---:|---:|---:|---|---|']
    for p in pairs:rows.append(f"|{p['Case']}|{fmt(p['TimeGainSeconds'])}|{fmt(p['MeanExitGainKph'])}|{fmt(p['After40mGainKph'])}|{p['JointImprovement']}|{p['NewDnf']}|")
    rows+=['','16. Oddzielny transfer na istniejący egg-asymmetric ma ten sam początek V1 i wybór zamrożonego wariantu; brak strojenia na tym torze. B kończy legalnie, lecz jest o 0,87 s wolniejszy od A. Transfer nie potwierdza poprawy; [transfer.json](transfer.json) pozostaje poza czterema held przypadkami.','',
           '## Pełna następna prosta — osobny zakres','',
           '|Przejazd|Historyczne2łuki PASS|Prędkość wyjścia2|+10m|+20m|+40m|+62m|Zaobserwowany zakres m|Wynik całego przedłużenia|','|---|---|---:|---:|---:|---:|---:|---:|---|']
    for r in summary:
        if not r['Name'].startswith('extended-'):continue
        m=r['Landmarks'][1];points=read(D/'native-checkpoints.json')[r['Name']]['2'];speeds={p['RelativeReferenceMeters']:(p['Native'] or {}).get('SpeedKph') for p in points}
        rows.append(f"|{r['Name']}|{r['Scope']['HistoricalTwoBendsCompleted']}|{fmt(speeds[0])}|{fmt(speeds[10])}|{fmt(speeds[20])}|{fmt(speeds[40])}|{fmt(speeds[62])}|{m['ObservedStraightMeters']:.2f}|{r['Metrics']['Native']['Outcome']}|")
    rows+=['','Definicja historyczna pozostaje: pełny obrys za drugą bramką +1s legalnej kontroli. Przedłużenie kończy się na faktycznych62m następnej prostej albo wcześniejszym DNF. Odrębny scope zachowuje ukończenie historyczne nawet przy późniejszej porażce. Żadne N/D nie jest zerem. Dokładne zgodne prefiksy zostały sprawdzone.','',
           '## Odpowiedzi 17–20 i werdykt','',
           f'17. Wybrany wariant na held: **{mean_cpu:.6f}s CPU/dec**, budżet GO z preflight **{threshold["MaximumMeanDecisionCpu"]:.6f}s**. Poszczególne mean/p95/max, prognozy, oceny kontaktów oraz fizyka Native są rozdzielone w [native-summaries.json](native-summaries.json) i planach. Nie chowamy wyszukiwania w czasie epizodu.','',
           f'18. Ekstrapolacja czterech zawodników przy5decyzjach/s to **{20*mean_cpu:.3f} CPU-s/s** samych wyszukiwań, przed fizyką i rywalami. To nie jest zmierzony bieg czterech zawodników i nie dowodzi gotowości czasu rzeczywistego.','',
           '19. Największą granicą jest zgodność lokalnie korzystnej, skończonej kontynuacji z dalszym replanowaniem i długim przejazdem. Prognozowana korekta może zostać zmieniona przy następnym review. Średni yaw może też osłabić znaczenie końcowego złego stanu. Nie rozwiązujemy tego obniżeniem YawWeight, zwiększeniem gripu ani sztywnym gaz1 na wyjściu.','',
           f'20. **{verdict}.** Bramki: `{gates}`. GO wymaga≥3/4 jednoczesnych popraw czasu/wyjścia/prostej, obu profili, braku nowych DNF i spełnienia kosztu. Niespełnione kryteria pozostają jawne; mechanizm nie trafia do domyślnego sterownika.','',
           '## Metodologia, dowody i koszt','',
           f'**{complete["Worlds"]} nowych światów; {complete["CpuSeconds"]:.6f}s CPU epizodów i wyszukiwań; {complete["Contacts"]} ocen kontaktu; {complete["Forecasts"]} prognoz.** Twarde limity60/900s/30M/300k. Read-only analiza i raportowanie mają oddzielne pomiary CPU i dodane koszty zapytań; brak nowych światów podczas analizy. Budżet nie był podnoszony po wynikach. Pomiar epizodów/search obejmuje 520,453125 s CPU; serializacja raw po timerze, build i testy są poza tym pomiarem. Odczyt i metryki to osobne 8,859375 s CPU, 0 nowych światów i 3292 niezatwierdzane zapytania o siły stanów prognozy; główny raport ma osobny report-cost.json. Nie jest to czas całego procesu wraz z I/O.','',
           'Pule:18 diagnostycznych obserwacji z dwóch zapisów #136,24 kontrolowane sekwencje,2 selekcyjne początki/geometrie ×4ramiona,4 wcześniej zamknięte pary held A/wybrany wariant, oddzielne przedłużenia i transfer. Wszystkie ramiona używają identycznych początków i nawierzchni w danym przypadku. Root/correction set, źródła, thresholds i evaluation-definition zamknięto przed finałem.','',
           'Geometria to oryginalne zmienne łuki, progressive-b12→16,6→12, progressive-c12→17/16,2→12, 318,78m, szerokość prostych12m i historyczne35+27. Cztery neutralne pola3m bez bonusów. Brak zmiany band, kredy i obrysu. Kierunek przeciwny do wskazówek zegara.','',
           'DesiredYaw w CSV jest dokładnym, oryginalnym odczytem geometrii z preview polityki przy bieżącym stanie Native: ahead=max(0,1m, ReferenceSpeed*PreviewSeconds), preview=RoadAt(s+ahead,offset), desiredYaw=Speed/preview.RadiusMeters na łuku i 0 na prostej. To nie jest krzywizna wektora prędkości: tę obliczamy osobno z rzeczywistego przyspieszenia. Fx/Fy/energia w wierszu odnoszą się do ostatniego ukończonego przedziału, a gaz do bieżącego accepted stanu. Prognozowe siły w traces są jawnym, niezatwierdzanym zapytaniem dt0 do oryginalnego kontaktu.','',
           'Pełne raw wszystkich światów: [raw-manifest.json](raw-manifest.json), native/, CSV telemetry/, oddzielne plany i prognozy. Testy źródeł i fingerprintów zachowują wszystkie dowody #129–#136. Uruchom tools/run-causal-yaw.ps1 do świeżych katalogów; nie nadpisuj rezerwacji. Release, testy i końcowy standardowy Windows CI opisane w validation.json oraz Draft PR.','',
           '[Protokół](protocol.json) · [Próg GO](thresholds.json) · [Selekcja](selection-lock.json) · [Wyniki held](held-comparisons.json) · [Replay lokalny](../../../tools/replay-viewer/?case=causal-yaw)','',
           '![Natywna telemetria](native-telemetry.svg)','','![Oddzielne prognozy yaw](yaw-diagnostic.svg)','']
    (D/'README.md').write_text('\n'.join(rows),encoding='utf-8',newline='\n');index=read(D/'index.json');index['Grade']=verdict;save('index.json',index)
    return verdict

BaseHead='060d6819cbae1ab701f3439d74a6e6266230419a'
if __name__=='__main__':
    started=time.process_time();geometry();corrections=offline_audit();summary=native_summaries();winner,pairs=comparisons(summary);drive_facts(winner);bundle(summary,winner);plots(winner)
    controlled=read(D/'controlled.json');oracle=[]
    for state in sorted({x['Name'] for x in controlled}):
        rows=[x for x in controlled if x['Name']==state and x['Native']['Metrics']['Native']['Completed']]
        best=min(rows,key=lambda x:(x['Native']['Metrics']['Native']['GateSeconds'],x['Arm'])) if rows else None
        oracle.append({'State':state,'BestObservedArm':None if best is None else best['Arm'],'GateSeconds':None if best is None else best['Native']['Metrics']['Native']['GateSeconds'],'Basis':'Posthoc sampled minimum only. Never used by the causal planner or the autonomous arm selection.'})
    save('sampled-oracle.json',oracle);verdict=report(summary,corrections,winner,pairs)
    dnfs=[]
    for r in summary:
        if r['Metrics']['Native']['Completed'] or r['Metrics']['Native']['Outcome']=='probe':continue
        run=read(RAW/(r['Name']+'.json.gz'));dnfs.append({'Name':r['Name'],'Arm':r['Arm'],'Mode':r['Mode'],'Scope':r['Scope'],'Metrics':r['Metrics'],'LastNativeState':old.info(run['Replay']['Samples'][-1])})
    save('dnf.json',dnfs);save('report-cost.json',{'CpuSeconds':time.process_time()-started,'NativeWorlds':0,'Scope':'Read-only raw reduction, CSV, plots and viewer packaging; separate from simulation/search CPU.'})
    if OUT!=D:shutil.copyfile(D/'README.md',OUT/'raport-137.md')
    print(json.dumps({'Verdict':verdict,'Winner':winner,'Worlds':len(summary),'Dnf':len(dnfs),'HeldPairs':pairs},indent=2))
