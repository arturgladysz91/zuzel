"""Compact read-only report/packaging for143. No physics, forecasts, fitting or policy selection."""
import csv,gzip,hashlib,io,json,math,shutil,statistics,sys,time
from collections import Counter,defaultdict
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
RESULT=Path(sys.argv[1]).resolve();D=RESULT.parent;RAW=Path(sys.argv[2]).resolve();OUT=Path(sys.argv[3]).resolve() if len(sys.argv)>3 else D
OUT.mkdir(parents=True,exist_ok=True)
def read(p):return json.loads(gzip.decompress(p.read_bytes()) if str(p).endswith('.gz') else p.read_bytes())
def save(name,value):(D/name).write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
def zipdata(p,value):
    with open(p,'wb') as f,gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as z:z.write(json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode())
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def fmt(x,n=2):return 'N/D' if x is None else f'{x:.{n}f}'
def mean(x):return statistics.mean(x) if x else None
def family(row):
    s=row['Case']['Case']['Scenario'];return tuple(s[k] for k in ['Speed','Offset','Start','OutwardSpeed','HeadingError','YawRate'])
def group_name(row):return row['Case']['Case']['Scenario']['Name']
def paired(rows):
    bases={q['Family']:q for q in rows if q['Pool']=='held' and q['Variant']['Id']=='A'};pairs=[]
    for q in rows:
        if q['Pool']!='held' or q['Variant']['Id']=='A':continue
        a=bases[q['Family']];both=a['Completed'] and q['Completed']
        pairs.append({'Case':q['Family'],'PhysicalFamily':group_name(q),'PhysicalIdentity':list(family(q)),'Geometry':q['Case']['Case']['Geometry'],'Variant':q['Variant']['Id'],
            'AComplete':a['Completed'],'Complete':q['Completed'],'NewDnf':a['Completed'] and not q['Completed'],'ASeconds':a['FullTimeSeconds'],'Seconds':q['FullTimeSeconds'],
            'GainSeconds':a['FullTimeSeconds']-q['FullTimeSeconds'] if both else None,'GainFraction':1-q['FullTimeSeconds']/a['FullTimeSeconds'] if both else None,
            'DistanceDifferenceMeters':q['FullPhysicalDistanceMeters']-a['FullPhysicalDistanceMeters'] if both else None,
            'MeanPhysicalSpeedGainKph':(q['FullPhysicalDistanceMeters']/q['FullTimeSeconds']-a['FullPhysicalDistanceMeters']/a['FullTimeSeconds'])*3.6 if both else None,
            'StudiedExitGainKph':None if q['Exit'] is None or a['Exit'] is None else q['Exit']['SpeedKph']-a['Exit']['SpeedKph'],
            'Plus40GainKph':None if q['Plus40'] is None or a['Plus40'] is None else q['Plus40']['SpeedKph']-a['Plus40']['SpeedKph'],
            'Outcome':q['Outcome'],'StopCategory':q['StopCategory']})
    save('held-pairs.json',pairs)
    with open(D/'held-pairs.csv','w',encoding='utf-8',newline='') as f:
        fields=[k for k in pairs[0] if k!='PhysicalIdentity'];w=csv.DictWriter(f,fieldnames=fields,extrasaction='ignore',lineterminator='\n');w.writeheader();w.writerows(pairs)
    aggregates=[]
    for variant in sorted({q['Variant'] for q in pairs}):
        p=[q for q in pairs if q['Variant']==variant];families=defaultdict(list)
        for q in p:
            if q['GainFraction'] is not None:families[tuple(q['PhysicalIdentity'])].append(q['GainFraction'])
        average=mean([statistics.mean(x) for x in families.values()]);success=all(q['Complete'] or not q['AComplete'] for q in p) and average is not None and average>=.05
        aggregates.append({'Variant':variant,'Cases':len(p),'PhysicalFamilies':len({tuple(q['PhysicalIdentity']) for q in p}),'Complete':sum(q['Complete'] for q in p),'AComplete':sum(q['AComplete'] for q in p),
            'NewDnf':sum(q['NewDnf'] for q in p),'JointPairs':sum(q['GainFraction'] is not None for q in p),'PairedFamilies':len(families),'FamilyMeanTimeGainFraction':average,
            'MinimumPairedTimeGainFraction':min((q['GainFraction'] for q in p if q['GainFraction'] is not None),default=None),
            'MaximumPairedTimeGainFraction':max((q['GainFraction'] for q in p if q['GainFraction'] is not None),default=None),'OrientationalFivePercentWithNoNewDnf':success})
    save('held-summary.json',aggregates);return pairs,aggregates
def selection(rows):
    table=[]
    baseline={q['Family']:q for q in rows if q['Pool']=='selection' and q['Variant']['Id']=='A'}
    for variant in sorted({q['Variant']['Id'] for q in rows if q['Pool']=='selection'}):
        group=[q for q in rows if q['Pool']=='selection' and q['Variant']['Id']==variant]
        p=[1-q['FullTimeSeconds']/baseline[q['Family']]['FullTimeSeconds'] for q in group if q['Completed'] and baseline[q['Family']]['Completed']]
        table.append({'Variant':variant,'Configuration':group[0]['Variant'],'Complete':sum(q['Completed'] for q in group),'Cases':len(group),'MeanPairedGainFraction':mean(p),
            'ChangedDecisions':sum(q['ChangedDecisions'] for q in group),'Outcomes':dict(Counter(q['Outcome'] for q in group)),
            'MeanDecisionCpu':sum(q['DecisionCpu']['Total'] for q in group)/sum(q['DecisionCpu']['Count'] for q in group)})
    save('selection-summary.json',table);return table
def files_and_logs(r,rows):
    old=read(D/'decision-logs.json.gz') if (D/'decision-logs.json.gz').exists() else {};manifest=[]
    for q in rows:
        if 'Decisions' in q:old[q['Name']]=q['Decisions']
        file=D/'telemetry'/(q['Name']+'.csv.gz');assert sha(file)==q['CsvSha256'];manifest.append({'Name':file.name,'Bytes':file.stat().st_size,'Sha256':sha(file),'NativeRawSha256':q['NativeRawSha256']})
    zipdata(D/'decision-logs.json.gz',old);save('telemetry-manifest.json',manifest)
    return old,manifest
def failure_data(rows,logs):
    data=[]
    for q in rows:
        if q['Completed']:continue
        last=q['LastAccepted'];trace=[]
        with gzip.open(D/'telemetry'/(q['Name']+'.csv.gz'),'rt',encoding='utf-8') as f:
            for x in csv.DictReader(f):
                if float(x['time'])>=q['NativeEndTimeSeconds']-.1-1e-8:trace.append({k:None if v=='' else float(v) for k,v in x.items()})
        data.append({'Name':q['Name'],'Pool':q['Pool'],'Variant':q['Variant'],'Outcome':q['Outcome'],'Category':q['StopCategory'],'NativeBetaDetectorDegrees':q['NativeBetaDetectorDegrees'],
            'NumericalPassed':q['NumericalClockGatesPassed'],'LastAccepted':last,'MinimumPhysicalInner':q['Telemetry']['MinimumPhysicalInner'],'MinimumPhysicalOuter':q['Telemetry']['MinimumPhysicalOuter'],
            'MinimumCertificate':q['Telemetry']['MinimumAcceptedCertificate'],'MaximumBetaDegrees':q['Telemetry']['Beta']['MaximumAbsDegrees'],
            'InstantFootprintBeyondTolerance':min(q['Telemetry']['MinimumPhysicalInner'],q['Telemetry']['MinimumPhysicalOuter'])<-.0016,
            'ActualRiderContactBegins':q['ActualRiderContactBegins'],'LastFiveDecisions':logs[q['Name']][-5:],'Last100ms':trace})
    save('all-stops.json',data);return data
def recovery_agreement(rows,logs):
    output=[]
    for q in rows:
        if not q['Variant']['Recovery']:continue
        decisions=logs[q['Name']];matched=changed=ended=0;fallback=0;ranking=0;new_empty=0
        for d in decisions:
            if d['ViableRoots']==0:fallback+=1
            if d.get('ForecastRecovery') is None:continue
            actual=next((a for a in decisions if abs(a['Time']-d['RecoveryObservedAt'])<1e-8),None)
            if actual is None:ended+=1;continue
            if actual['Request']==d['ForecastRecovery']:matched+=1
            else:
                changed+=1
                if actual['ViableRoots']==0:new_empty+=1
                else:ranking+=1
        output.append({'Name':q['Name'],'Pool':q['Pool'],'Decisions':len(decisions),'OriginalAFallbacks':fallback,'RecoveryRequestMatchedAtFutureReview':matched,
            'DifferentFreshChoice':changed,'DifferentWithNoCheckedViableRoot':new_empty,'DifferentWithViableFreshRanking':ranking,'NativeEndedBeforeReview':ended,
            'Meaning':'Observed plan/review agreement only. A different ranking is not proof that a physical hazard appeared or vanished; no future native state selected the earlier action.'})
    save('recovery-agreement.json',output);return output
def auxiliary(r,rows):
    index={q['Name']:q for q in rows};out=[]
    fields=['time','reference_m','distance_m','x_m','y_m','vx_mps','vy_mps','psi_rad','beta_rad','yaw_rads','omega_rads','steering_rad']
    def trajectory(name):
        with gzip.open(D/'telemetry'/(name+'.csv.gz'),'rt',encoding='utf-8') as f:return [tuple(q[k] for k in fields) for q in csv.DictReader(f)]
    for q in rows:
        if q['Pool']!='auxiliary' or q['Name'].startswith('warm-'):continue
        if q['Name'].startswith('beta-'):main='-'.join(q['Name'].split('-')[2:])
        elif q['Name'].startswith('refined-'):main=q['Name'][8:]
        else:continue
        original=index[main];a=trajectory(main);b=trajectory(q['Name']);prefix=b[:len(a)]==a
        out.append({'Name':q['Name'],'Main':main,'SameNativePhysicalPrefix':prefix,'OriginalOutcome':original['Outcome'],'Outcome':q['Outcome'],
            'Completed':q['Completed'],'MaximumBetaDegrees':q['Telemetry']['Beta']['MaximumAbsDegrees'],'InstantFootprintBeyondTolerance':min(q['Telemetry']['MinimumPhysicalInner'],q['Telemetry']['MinimumPhysicalOuter'])<-.0016,
            'PhysicalInner':q['Telemetry']['MinimumPhysicalInner'],'PhysicalOuter':q['Telemetry']['MinimumPhysicalOuter'],'MinimumCertificate':q['Telemetry']['MinimumAcceptedCertificate'],
            'Interpretation':'Detector-only diagnostic with unchanged controller/physics. Removing beta does not establish real-motorcycle correctness. Refined certificate is observation-only and does not promote a main result.'})
    save('detector-diagnostics.json',out);return out
def viewer(r,rows,logs):
    winner=r['ConfirmationLock']['Winners'][0]['Id'];names={q['Name']:q for q in rows};bundle={};groups=[]
    specs=[('H0',['A',winner,'B']),('H1',['A',winner,'K1']),('H4-b',['A','F','K2']),('D0',['A','D'])]
    colors={'A':'#ffd27b','F':'#73dfdf','K1':'#d99fe3','K2':'#a9d77d','B':'#f28f71','D':'#ff7777'}
    label={'A':'A · oryginalne #142','F':'F · bez preferencji geometrycznej','K1':'F+B · bez geometrii i preferencji gazu','K2':'F+E · bez geometrii i kary skrętu','B':'B · bez preferencji gazu','D':'D · bez kary yaw'}
    for case,ids in specs:
        ids=list(dict.fromkeys(ids));selected=[names[case+'-'+v] for v in ids if case+'-'+v in names]
        if len(selected)<2:continue
        for q in selected:
            file=RAW/(q['Name']+'.json.gz');assert sha(file)==q['NativeRawSha256'];bundle[q['Name']]=read(file)
        decisionfile='viewer-'+case+'.json.gz';zipdata(D/decisionfile,{q['Variant']['Id']:logs[q['Name']] for q in selected})
        groups.append({'Id':case,'Reasons':['ciągła jazda autonomiczna','odłożony stan' if selected[0]['Pool']=='held' else 'diagnostyka yaw'],
            'Scenario':selected[0]['Case']['Case']['Scenario'],'Runs':[{'Name':q['Name'],'Policy':q['Variant']['Id']} for q in selected],
            'GeometryFile':'../causal-yaw-throttle/'+selected[0]['Case']['Case']['Geometry']+'-viewer-geometry.json','TwoBends':True,'DecisionFile':decisionfile,
            'Scope':'Pełny zakres #142/#143, bez resetów; dwa łuki i dalsze62m, dla studiedCorner2 także trzeci łuk. Czasy poniżej obejmują cały wspólny dystans, a historyczna bramka pozostaje osobna.',
            'Summary':{q['Variant']['Id']:{k:q[k] for k in ['Name','Completed','Outcome','FullTimeSeconds','NativeEndTimeSeconds','StopCategory','DecisionCpu','ChangedDecisions','HistoricalGateSeconds']} for q in selected}})
    zipdata(D/'selected-replays.json.gz',{'Runs':bundle});save('index.json',{'Title':'#143 · Audyt ograniczeń · autonomiczne A/B','Grade':r['Analysis143']['Verdict'],
        'DefaultRun':'H0-'+winner,'PreferredPolicy':winner,'Labels':label,'Colors':colors,'Horizons':dict.fromkeys(label,.9),'Groups':groups,'ComparisonCharts':True,
        'Charts':[['native-comparison.svg','Prędkość, gaz, yaw i β względem drogi'],['held-effects.svg','Pełne odłożone wyniki']],
        'RawAvailability':'W Git: pełne natywne CSV wszystkich światów, wszystkie logi decyzji i reprezentatywne pełne replaye. Pozostałe pełne JSON są lokalnym cache; źródła i konfiguracje odtwarzają każdy wynik.'})
def plots(rows,pairs):
    import matplotlib;matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    import numpy as np
    plt.rcParams.update({'font.size':8,'svg.fonttype':'none','axes.grid':True,'grid.alpha':.2})
    variants=sorted({q['Variant'] for q in pairs});cases=list(dict.fromkeys(q['Case'] for q in pairs))
    fig,ax=plt.subplots(figsize=(13,4.7));values=np.full((len(variants),len(cases)),np.nan)
    for i,v in enumerate(variants):
        for j,c in enumerate(cases):
            q=next(q for q in pairs if q['Case']==c and q['Variant']==v)
            if q['GainFraction'] is not None:values[i,j]=q['GainFraction']*100
    image=ax.imshow(np.ma.masked_invalid(values),cmap='RdYlGn',vmin=-6,vmax=6,aspect='auto')
    for i in range(len(variants)):
        for j in range(len(cases)):ax.text(j,i,'DNF' if np.isnan(values[i,j]) else f'{values[i,j]:+.1f}%',ha='center',va='center',fontsize=8,color='black')
    ax.set_xticks(range(len(cases)),cases,rotation=35,ha='right');ax.set_yticks(range(len(variants)),variants);ax.grid(False)
    ax.set_title('Odłożone pełne przejazdy: zysk czasu względem A · DNF nie jest zyskiem\nB/C tej samej rodziny nie są niezależnymi potwierdzeniami')
    fig.colorbar(image,ax=ax,label='Zysk czasu %');fig.tight_layout();fig.savefig(D/'held-effects.svg');fig.savefig(OUT/'held-143.png',dpi=150);plt.close(fig)
    fig,axes=plt.subplots(5,2,figsize=(15,15),sharex='col');color={'A':'#a86b06','F':'#00828b','K2':'#529635','K1':'#a64fba','B':'#cb643f'}
    for col,case in enumerate(['H0','H1']):
        for variant in ['A','F','K2','K1','B']:
            name=case+'-'+variant
            if not any(q['Name']==name for q in rows):continue
            with gzip.open(D/'telemetry'/(name+'.csv.gz'),'rt',encoding='utf-8') as f:data=[{k:None if v=='' else float(v) for k,v in q.items()} for q in csv.DictReader(f)][::3]
            x=[q['reference_m'] for q in data]
            for ax,key,scale in zip(axes[:,col],['speed_kph','active_gas','yaw_rads','beta_rad','steering_rad'],[1,100,1,180/math.pi,180/math.pi]):
                ax.plot(x,[q[key]*scale for q in data],color=color[variant],label=variant,lw=1)
        axes[0,col].set_title(case+' · urwanie krzywej oznacza koniec natywnego zapisu');axes[0,col].legend(ncol=5)
        for ax,label in zip(axes[:,col],['km/h','Aktywny gaz %','Yaw rad/s','β stopnie','Kierownica stopnie']):ax.set_ylabel(label)
        axes[-1,col].set_xlabel('Referencyjne metry · ciągły świat Box2D')
    fig.tight_layout();fig.savefig(D/'native-comparison.svg');fig.savefig(OUT/'native-143.png',dpi=140);plt.close(fig)
    for name in ['held-effects.svg','native-comparison.svg']:
        p=D/name;p.write_text('\n'.join(line.rstrip() for line in p.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
def report(r,rows,pairs,held,selected,stops,detectors,logs):
    candidates=[q for q in held if q['NewDnf']==0 and q['FamilyMeanTimeGainFraction'] is not None];best=max(candidates,key=lambda q:q['FamilyMeanTimeGainFraction']) if candidates else max(held,key=lambda q:q['Complete'])
    success=any(q['OrientationalFivePercentWithNoNewDnf'] for q in held);verdict='PASS' if success else 'PARTIAL'
    r['Analysis143']={'Verdict':verdict,'BestReliableHeld':best,'Held':held,'AllMainWorldsNumericallyValid':all(q['NumericalClockGatesPassed'] for q in rows),
        'Limits':'Deterministic finite cases,8physical held families in12 case/geometry pairs.5percent is an orientational target; smaller or mixed effects are explicit. No population or empirical motorcycle claim.'}
    save('analysis.json',r['Analysis143'])
    mode={q['Variant']:q for q in selected};byname={q['Name']:q for q in rows};baseline=[q for q in rows if q['Pool']=='held' and q['Variant']['Id']=='A']
    main=[q for q in rows if q['Pool']!='auxiliary'];fullmax=max(q['Telemetry']['Speed']['MaximumKph'] for q in main if q['Completed'])
    lines=['# #143 — Comprehensive Racing Constraints Audit & Autonomous Speed Optimization','',f'**{verdict}.** Baza dokładnie `0da21bc92ee541b6611e19f1d4bd2e3866b283ca`. Domyślnie OFF, bez CEM, bez merge. Oryginalne fizyka, silnik, opony, mu, masa/I, Box2D, tor i polityka A #16015 bez zmian.','',
        f'Najlepszy odłożony wariant bez dodatkowego DNF: **{best["Variant"]}**, {best["Complete"]}/{best["Cases"]} ukończeń przy A {best["AComplete"]}/{best["Cases"]}; średni zysk pełnego czasu ważony równo rodzinami **{fmt(100*best["FamilyMeanTimeGainFraction"])}%**. Zakres pojedynczych sparowanych efektów {fmt(100*best["MinimumPairedTimeGainFraction"])}…{fmt(100*best["MaximumPairedTimeGainFraction"])}%. Nie ukrywamy strat ani niższych efektów od celu5%.','',
        '## Co faktycznie zbadano','',
        '40 pełnych izolowanych A–H/I/R na D0/D1 i dwóch nowych początkach S0/S1. A–I mają identyczny H09, przestrzeń gazu i kierownicy, reakcję.24s i review.2s. R oddzielnie dodaje skończony sprawdzian odzyskiwania do1.7s, bez zmiany pierwszych dostępnych akcji. Wszystkie warianty samodzielnie wybierają sterowanie przez CAŁY zakres; nie są podmianą jednej decyzji.','',
        'Po tych wynikach zamknięto kombinacje F+B, F+E, B+E i F+B+E oraz dwa połączenia z R. Ponieważ C/D bez kierunku/yaw miały0/4, przed finałem dodano ich osobne kombinacje z R; poprzednie konfiguracje i wyniki zachowano. To test zastępowania części stabilizującej roli prognozą konsekwencji, nie argument za świętością starych wag. R może jednak jawnie wrócić do A — nie dowodzi to całkowitego usunięcia danej preferencji z każdego fallbacku.','',
        'Przed jakimkolwiek światem finałowym zamknięto F/K2/K1 oraz obowiązkowy globalny B. Dwanaście par stan/geometria zawiera OSIEM rodzin fizycznych: znane historycznie H0/H1, cztery nowe rodziny na B/C oraz dwie nowe na egg-asymmetric. B/C tej samej rodziny są ważone łącznie, nie podwójnie. Nie strojono na finale. Zakres jest taki sam jak#142: badany łuk,62m, cały kolejny łuk i dalsze62m; rodziny rozpoczynające badanie od drugiego wyjścia mają również trzeci łuk.','',
        '## Ograniczenia i rekomendacje','',
        '|Ograniczenie|Uzasadnienie fizyczne?|Efekt usunięcia offline|Pełna jazda izolowana|Rekomendacja|','|---|---|---|---|---|']
    names={'B':('Preferencja częściowego gazu','Nie, koszt odejścia od31,82%','Ocena globalna i finał B; większy gaz sam nie jest korzyścią.'),
        'C':('Kierunek χ vs przyszła styczna','Preferencja, nie warunek β=0','Nie usuwać samodzielnie w obecnej strukturze; wynik nie dowodzi konieczności wagi4 dla każdego sterownika.'),
        'D':('Yaw vs prędkość×krzywizna','Preferencja, nie prawo opony','Nie usuwać samodzielnie z held-H09; R częściowo ratuje, lecz nie daje uniwersalnej alternatywy.'),
        'E':('Zmiana kierownicy','Preferencja obok rzeczywistego aktuatora','Wpływ zależy od kontekstu; K2 vs F izoluje usunięcie E przy już usuniętym F.'),
        'F':('GeometrySpeedWeight i rezerwa.8','Heurystyka, nie granica sił','Ocenić według odłożonego F, zachowując natywne guardy; brak automatycznej promocji na bazie D0.'),
        'G':('Clearance/Uncertain margins','Rezerwy ostrożności','Sprawdzone zmniejszenie marginesów nie jest bezpiecznym zamiennikiem oceny kontynuacji.'),
        'H':('Przygotowanie geometrii','Niecertyfikowane oszacowanie','Znany tor i fizycznie osiągnięty offset pomagają w części przypadków; nie są zadaną linią.'),
        'I':('Pierwszeństwo Safe nad Uncertain','Hierarchia implementacji','Ważony score bez tej hierarchii wymaga lepszego sprawdzianu kontynuacji; Uncertain samo nie jest DNF.')}
    counts=r['Audit']['Counts']
    for id,(title,physical,recommendation) in names.items():
        q=mode[id];offline='H: '+str(sum(q['Changed'] for q in r['Audit']['GeometryH']))+'/18 zmienionych' if id=='H' else str(counts[id]['Changed'])+'/28 zmienionych'
        lines.append(f'|{title}|{physical}|{offline}|{q["Complete"]}/{q["Cases"]}, średni sparowany zysk {fmt(None if q["MeanPairedGainFraction"] is None else 100*q["MeanPairedGainFraction"])}%|{recommendation}|')
    lines += ['|β45°|Nieskalibrowana obwiednia eksperymentalna|Oddzielono od wyniku yaw|Diagnostyki60°/bez progu poniżej|Nie uznawać przejazdu za realnie poprawny tylko po usunięciu detektora.|',
        '|Obrys, siły, aktuator, energia|Ograniczenia zaimplementowanego modelu|Niezmienione w głównym porównaniu|Weryfikowane native|Zachować; realny obrys i mechanika nie są arbitralną karą.|','',
        'Dokładne równania, źródła kodu, ograniczona siatka±15° wobec mechanicznych±35°, kolejka, regularyzacja opony i guardy: [pełna mapa AUDIT.md](AUDIT.md).28 obserwacji offline to tylko dwie rodziny. Brak zmiany offline nie dowodzi globalnej zbędności: F zmieniało0/28 lokalnych wyborów, a mimo to zmieniało pełne autonomiczne przejazdy.','',
        '## Kombinacje i konsekwencje — pula selekcji','',
        '|Wariant|Usunięte preferencje / dodatkowa kontynuacja|Ukończenia|Średni zysk czasu tylko wspólnie ukończonych par|','|---|---|---:|---:|']
    for q in selected:
        if not (q['Variant'].startswith('K') or q['Variant'] in ['R','H','I']):continue
        description='+'.join(q['Configuration']['Changes']) or 'oryginalny score, bez pierwszeństwa Safe'
        if q['Configuration']['Recovery']:description+=' + R (możliwy fallback A)'
        lines.append(f'|{q["Variant"]}|{description}|{q["Complete"]}/{q["Cases"]}|{fmt(None if q["MeanPairedGainFraction"] is None else q["MeanPairedGainFraction"]*100)}%|')
    lines += ['','Kombinacje nie są sumą efektów pojedynczych: B+E kończy2/4, a dodanie F daje3/4; F+E kończy4/4 w selekcji, lecz11/12 w finale. R poprawia ukończenia bez yaw z0/4 do3/4 i bez kierunku z0/4 do1/4, z zastrzeżeniem fallbacku. H nie wszedł do zamrożonego top3 i nie ma niezależnego finału; jego4/4 nie uzasadnia promocji.','',
        '## Odłożone pełne przejazdy','',
        '|Wariant|Ukończone / A|Nowe DNF|Średni zysk czasu na rodzinę|Min…max zysku w parach|','|---|---:|---:|---:|---:|']
    for q in held:lines.append(f'|{q["Variant"]}|{q["Complete"]}/{q["Cases"]} / {q["AComplete"]}/{q["Cases"]}|{q["NewDnf"]}|{fmt(100*q["FamilyMeanTimeGainFraction"]) if q["FamilyMeanTimeGainFraction"] is not None else "N/D"}%|{fmt(None if q["MinimumPairedTimeGainFraction"] is None else 100*q["MinimumPairedTimeGainFraction"])}…{fmt(None if q["MaximumPairedTimeGainFraction"] is None else 100*q["MaximumPairedTimeGainFraction"])}%|')
    lines += ['','|Przypadek|A s|F s|K2=F+E s|K1=F+B s|B s|','|---|---:|---:|---:|---:|---:|']
    for a in baseline:
        values=[byname[a['Family']+'-'+v]['FullTimeSeconds'] if byname[a['Family']+'-'+v]['Completed'] else None for v in ['A','F','K2','K1','B']]
        lines.append('|'+a['Family']+'|'+'|'.join(fmt(v,3) for v in values)+'|')
    lines += ['','N/D to brak legalnego pełnego czasu, nie0. Wszystkie pary, przyczyny stopów i różnice rzeczywistej drogi: [held-pairs.csv](held-pairs.csv). Wyszukiwanie i wybór wariantów oparto wyłącznie na wcześniejszej puli; powyższa tabela nie służy do wyboru nowej polityki po finale.','',
        '|Wariant — tylko odłożone ukończone|Śr. badane wyjście km/h|Śr.+40m km/h|Śr.+62m km/h|Aktywny gaz1 %|Max β°|Min fizyczny gap m|Kontakty motocykli / naruszenia obrysu|Śr. różnica drogi vs A m|','|---|---:|---:|---:|---:|---:|---:|---:|---:|']
    for id in ['A']+[q['Variant'] for q in held]:
        group=[q for q in rows if q['Pool']=='held' and q['Variant']['Id']==id and q['Completed']]
        if not group:continue
        speed=lambda key:mean([q[key]['SpeedKph'] for q in group if q[key] is not None]);diff=[q['DistanceDifferenceMeters'] for q in pairs if q['Variant']==id and q['DistanceDifferenceMeters'] is not None]
        contacts=sum(q['ActualRiderContactBegins'] for q in group);crossings=sum(min(q['Telemetry']['MinimumPhysicalInner'],q['Telemetry']['MinimumPhysicalOuter'])<-.0016 for q in group)
        lines.append(f'|{id}|{fmt(speed("Exit"))}|{fmt(speed("Plus40"))}|{fmt(speed("Plus62"))}|{fmt(100*statistics.mean(q["Telemetry"]["ActiveGasFractions"][2] for q in group))}|{fmt(max(q["Telemetry"]["Beta"]["MaximumAbsDegrees"] for q in group))}|{fmt(min(min(q["Telemetry"]["MinimumPhysicalInner"],q["Telemetry"]["MinimumPhysicalOuter"]) for q in group),4)}|{contacts} / {crossings}|{fmt(mean(diff))}|')
    lines += ['','Prędkości pochodzą z tych samych przekrojów każdego przypadku; średnie opisowe różnych zakresów nie zastępują par. Gaz1 obejmuje wspólne odziedziczone0,24s początkowego Drive=1; osobne żądania i aktywacje są w logach. Zysk czasu może pochodzić ze skrócenia drogi — w CSV podano też zmianę średniej fizycznej prędkości. Pełne wejścia/wyjścia każdego łuku, środek prostej31m, +20/+40/+62m, gaz żądany/aktywny, percentyle β/yaw/wheelspin, Fx/Fy i utilization są w results.json i natywnych CSV.','',
        f'Największa prędkość w legalnie ukończonych głównych próbach wynosi {fullmax:.2f}km/h. Wyższe wartości przed DNF pozostają oddzielne i nie są sukcesem sportowym. Wartości100km/h nie narzucano ani nie premiowano.','',
        '## Zatrzymania, predyktor i CPU','',
        f'Zachowano {len(stops)} nieukończonych światów, w tym osobne diagnostyki detektorów. W głównej puli było25 zatrzymań na niecertyfikowanym interwale i3 na β45; bez potwierdzonego przekroczenia obrysu. [all-stops.json](all-stops.json) zawiera kategorię, ostatnie100ms sił/stanu, chwilowy gap, certyfikat, β i ostatnie decyzje. Odziedziczona nazwa pola `PhysicalDnf` obejmuje także niepewny certyfikat i β — nie jest dowodem fizycznego wypadku. Wiążące rozróżnienie zawierają `StopCategory` i `FailureClassification`. Adapter tej próby solo nie zawiera fizycznej ściany; licznik rzeczywistych kontaktów dotyczy motocykli, a naruszenie geometrii raportujemy osobno.','',
        '|Diagnostyka|Ten sam fizyczny prefiks|Wynik dalszej jazdy|Max β°|Chwilowe naruszenie obrysu|','|---|---|---|---:|---|']
    for q in detectors:lines.append(f'|{q["Name"]}|{q["SameNativePhysicalPrefix"]}|{q["Outcome"]}|{fmt(q["MaximumBetaDegrees"])}|{q["InstantFootprintBeyondTolerance"]}|')
    lines += ['','Sterownik w diagnostykach β zachowuje oryginalną ocenę prognoz, w tym dawną klasyfikację β45. Zmieniono wyłącznie moment zatrzymania natywnego detektora, nie opony, akcje ani punktację. Refined jest oddzielnym wyjaśnieniem niepewnego sweepu; nie podmienia wyniku głównego.','',
        'Weryfikacja predyktora: [predictor-check.json](predictor-check.json) sprawdza wspólne pierwsze0,4s przed aktywacją kolejnej rzeczywistej komendy+.44. Nie porównuje fałszywie końca held-H09 z natywnym replanowaniem. Dobre dopasowanie tego prefiksu nie certyfikuje dalszego manewru ani realnej opony. Uzgodnienie prognozowanej akcji odzyskania z późniejszym wyborem i fallbacki A: [recovery-agreement.json](recovery-agreement.json).','',
        f'Główny koszt: **{r["Cost"]["Worlds"]} światów, {r["Cost"]["CpuSeconds"]:.3f} CPU-s, {r["Cost"]["Forecasts"]} prognoz, {r["Cost"]["Contacts"]} ocen kontaktu**. Budżet160/1800 nie został zwiększony. To rezerwacje całych epizodów, prognozy, podsumowania i serializacja raw/CSV oraz offline; zapis zbiorczego indeksu, build/testy, końcowe formatowanie i Git mają osobny zakres. Nie ma limitu.2s na decyzję. Dodatkowy koszt read-only prognoz walidacyjnych i pakowania zapisano osobno.','',
        '|Wariant — held|Średnie CPU/dec|Pierwsza decyzja świata — zakres|Późniejsze CPU/dec|Średnie prognozy/dec|','|---|---:|---:|---:|---:|']
    for id in ['A']+[q['Variant'] for q in held]:
        group=[q for q in rows if q['Pool']=='held' and q['Variant']['Id']==id];count=sum(q['DecisionCpu']['Count'] for q in group)
        lines.append(f'|{id}|{fmt(sum(q["DecisionCpu"]["Total"] for q in group)/count,4)}|{fmt(min(q["DecisionCpu"]["ColdFirst"] for q in group),4)}…{fmt(max(q["DecisionCpu"]["ColdFirst"] for q in group),4)}|{fmt(mean([q["DecisionCpu"]["WarmMean"] for q in group]),4)}|{fmt(sum(sum(d["Forecasts"] for d in logs[q["Name"]]) for q in group)/count)}|')
    lines += ['','|Powtórzenia D0|Pierwsza decyzja procesu|Śr. CPU/dec w trzech światach|Fingerprint każdej trajektorii zgodny z pierwotną|','|---|---:|---|---|']
    for v in ['A','F']:
        warm=[q for q in rows if q['Name'].startswith('warm-') and q['Variant']['Id']==v];original=byname['D0-'+v]
        lines.append(f'|{v}|{fmt(warm[0]["DecisionCpu"]["ColdFirst"],4)}|'+', '.join(fmt(q['DecisionCpu']['Mean'],4) for q in warm)+f'|{all(q["NativeMetrics"]["Fingerprint"]==original["NativeMetrics"]["Fingerprint"] for q in warm)}|')
    lines += ['','Pierwsza decyzja kolejnego świata nie zawsze jest zimnym JIT procesu; w osobnym procesie benchmarku zimna jest pierwsza decyzja A, F następuje po A. Sześć dodatkowych powtórzeń kontroluje ustaloną pracę i determinizm. Nie stwierdzono poważnej regresji kosztu wybranych wariantów; ok.0,08–0,10 CPU-s/dec nadal oznacza istotny koszt. Procesowy czas Windows jest skwantowany. Nie jest to pomiar biegu czterech rywali;20 takich decyzji/s to jedynie ekstrapolacja kosztu wyszukiwań.','',
        '## Odpowiedzi końcowe','',
        f'1. **Co można usunąć całkowicie?** Globalne usunięcie preferencji gazu B zachowało12/12 ukończeń, ale średni efekt wyniósł tylko {fmt(100*best["FamilyMeanTimeGainFraction"])}%, z zyskami i stratami zależnymi od stanu. To uzasadnia dalsze porównywanie B, nie twierdzenie o istotnej ogólnej poprawie. Dla pozostałych kar nie mamy równie niezawodnego potwierdzenia pełnej jazdy. Domyślny sterownik pozostaje bez zmian.','',
        '2. **Co osłabić/zastąpić?** Geometryczny speed hint, yaw/kierunek i marginesy są heurystykami. Lepsza ocena konsekwencji może zastąpić część ich roli; obecny skończony R jest tylko częściowo skuteczny i korzysta z fallback A. Nie wykazano, że każdą dawną wagę należy zachować ani że wystarczy ją wyzerować.','',
        '3. **Co jest konieczne?** Spójność sił, stanów, energii, rzeczywistego obrysu i fizyczna realizacja komend. β45,20% reserve i enum Uncertain nie są prawami fizyki; zmiana ich detektorów lub rankingów nie wystarcza do uznania nowej jazdy za empirycznie poprawną.','',
        '4. **Największy problem?** W badanym modelu koszty faktycznie wpływają na pełną jazdę, a zysk jednego przypadku nie przenosi się automatycznie. H09 zakłada utrzymanie komendy i nie obejmuje całej prostej/kolejnego łuku; R sprawdza tylko kilka odzyskiwań. Siatka działań nie wykorzystuje całych±35°. Oryginalna opona nadal nie jest empirycznie skalibrowana; nie maskujemy tego zmianą nagrody.','',
        f'5. **Czy może szybciej i legalnie?** Tak w części sparowanych pełnych prób; końcowy zakres i wielkość efektu to {best["Variant"]} / {fmt(100*best["FamilyMeanTimeGainFraction"])}% przy {best["Complete"]}/{best["Cases"]} ukończeń. Straty, nowe DNF innych wariantów i zakres generalizacji pozostają jawne.','',
        f'6. **Co dalej?** Rozwijać najlepszy niezawodny wariant {best["Variant"]} jako odizolowany eksperyment, porównując nadal z A. Dalszą stabilizację badać przez krótsze przyczynowe korekty i ocenę konsekwencji, a siatkę sterowania i kalibrację opon w oddzielnych testach. Bez CEM i bez automatycznego włączania do produkcji.','',
        '## Odtwarzanie i dane','',
        'Z głównego katalogu repozytorium: `powershell -File tools/racing-constraints/run.ps1 -Evidence work/repro143/results.json -Raw work/repro143/raw -Python python`. Wymaga świeżej ścieżki, .NET 8 oraz matplotlib/numpy do wykresów. Etapy i rezerwacje są jawne; przerwana rezerwacja nie jest automatycznie powtarzana. CI wyłącznie sprawdza zachowane dane i kod, bez ponownego uruchamiania całego badania.','',
        f'W Git jest {len(rows)} natywnych CSV gzip ({sum((D/"telemetry"/(q["Name"]+".csv.gz")).stat().st_size for q in rows)/1e6:.1f} MB), logi wszystkich decyzji i wybrane pełne replaye. Pozostałe pełne JSON pozostały lokalnym cache; ich SHA są w wynikach. Komenda odtwarzająca tworzy je ponownie, nie dopasowuje wag. Pakowanie przeniosło logi bezstratnie, nie zmieniło wyników. Osobne koszty analizy: [predyktor](predictor-check.json), [raport](report-cost.json).','',
        '[Audyt równań i historii](AUDIT.md) · [Wyniki/protokół/hashe](results.json) · [Odłożone pary](held-pairs.csv) · [Wszystkie CSV i SHA](telemetry-manifest.json) · [Logi decyzji](decision-logs.json.gz) · [Walidacja](validation.json) · [Replay lokalny](http://127.0.0.1:8017/tools/replay-viewer/?case=constraints143)','',
        '![Wszystkie odłożone efekty](held-effects.svg)','','![Natywne prędkości i sterowanie](native-comparison.svg)','',f'**Werdykt: {verdict}.**','']
    (D/'README.md').write_text('\n'.join(lines),encoding='utf-8',newline='\n')
    if OUT!=D:shutil.copyfile(D/'README.md',OUT/'raport-143.md')
    return verdict
if __name__=='__main__':
    started=time.process_time();original_sha=sha(RESULT);r=read(RESULT);assert r.get('ConfirmationComplete');rows=r['Native'];logs,manifest=files_and_logs(r,rows)
    pairs,held=paired(rows);selected=selection(rows);stops=failure_data(rows,logs);agreement=recovery_agreement(rows,logs);detectors=auxiliary(r,rows)
    verdict=report(r,rows,pairs,held,selected,stops,detectors,logs);viewer(r,rows,logs);plots(rows,pairs)
    if 'Packaging' not in r:r['Packaging']={'OriginalCompleteResultsSha256':original_sha,'Meaning':'All decision arrays moved losslessly to decision-logs.json.gz. Every native row, result, configuration, source hash and lock retained; no simulation or selection rerun.'}
    for row in rows:row.pop('Decisions',None)
    r['Packaging']['DecisionLogsSha256']=sha(D/'decision-logs.json.gz');RESULT.write_text(json.dumps(r,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8',newline='\n')
    history=read(D/'report-cost-history.json') if (D/'report-cost-history.json').exists() else []
    history.append({'CpuSeconds':time.process_time()-started,'NewWorlds':0,'NewForecasts':0,'Purpose':'Read-only summarization, plots, lossless decision-log extraction, selected replay packaging and hashing.'});save('report-cost-history.json',history)
    save('report-cost.json',{'CpuSeconds':sum(q['CpuSeconds'] for q in history),'Passes':len(history),'NewWorlds':0,'NewForecasts':0})
    print(json.dumps({'Verdict':verdict,'Worlds':r['Cost']['Worlds'],'Held':held,'ReportCpu':history[-1]['CpuSeconds']},indent=2))
