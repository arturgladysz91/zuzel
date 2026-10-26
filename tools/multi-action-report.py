"""Read-only report of frozen #135 native trials. Never chooses or tunes a causal plan."""
import argparse, bisect, csv, gzip, hashlib, importlib.util, json, math, re, statistics, time
from collections import Counter
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
D=ROOT/'docs/experiments/multi-action-planning'
def read(p):
    p=Path(p)
    if p.suffix=='.gz':return json.loads(gzip.decompress(p.read_bytes()))
    return json.loads(p.read_bytes() if p.exists() else gzip.decompress(Path(str(p)+'.gz').read_bytes()))
def write(n,value):
    (D/n).write_text(json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n',encoding='utf-8')
def packed(n,value):
    (D/n).write_bytes(gzip.compress(json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode(),compresslevel=9,mtime=0))
def camel(x):
    if isinstance(x,dict):return {k[0].lower()+k[1:]:camel(v) for k,v in x.items()}
    if isinstance(x,list):return [camel(v) for v in x]
    return x
def table(headers,rows):return '\n'.join(['| '+' | '.join(headers)+' |','| '+' | '.join(['---']*len(headers))+' |']+['| '+' | '.join(str(c) for c in row)+' |' for row in rows])
def fmt(x):return 'DNF' if x is None else f'{x:.3f}'
def gain(row):
    a,b=row['Baseline']['Native'],row['Native']['Native']
    return a['GateSeconds']-b['GateSeconds'] if a['Completed'] and b['Completed'] else None
def main(raw):
    started=time.process_time();raw=Path(raw)
    for geometry in ['progressive-b','progressive-c','egg-asymmetric']:
        # Serialization-only normalization of the read-only C# geometry export.
        write(geometry+'-geometry.json',camel(read(D/(geometry+'-geometry.json'))))
    protocol=read(D/'protocol.json');ledger=read(D/'attempts.json');preflight=read(D/'preflight.json')
    if not (D/'source-line-endings.json').exists():
        sources={}
        for name,digest in protocol['Sources'].items():
            b=(ROOT/'tools/shared-world-speedway'/name).read_bytes();assert hashlib.sha256(b).hexdigest().upper()==digest,name
            sources[name]=dict(OriginalRawSha256=digest,LFContentSha256=hashlib.sha256(b.replace(b'\r\n',b'\n')).hexdigest().upper())
        write('source-line-endings.json',dict(Rule='Postflight CI content audit: CRLF to LF only; original raw hashes and frozen locks retained; raw source verified before deriving normalized hash.',Sources=sources))
    pairs=read(D/'diagnostic-pairs.json')+read(D/'confirm-pairs.json')
    states={s['Name']:s for name in ['diagnostic','confirm'] for s in read(D/(name+'-states.json'))}
    searches={s['State']:s for name in ['diagnostic','confirm'] for s in read(D/(name+'-search.json'))}
    spec=importlib.util.spec_from_file_location('prior_decision',ROOT/'tools/rider-decision-report.py');prior=importlib.util.module_from_spec(spec);spec.loader.exec_module(prior)
    quality={};manifest=[]
    for entry in ledger:
        assert entry['Complete'],entry['Name']
        if not entry['NativeWorlds']:continue
        path=raw/(entry['Name']+'.json.gz');digest=hashlib.sha256(path.read_bytes()).hexdigest().upper();assert digest==entry['RawSha256']
        manifest.append(dict(Name=entry['Name'],Sha256=digest,Bytes=path.stat().st_size))
        run=read(path);prior.GEOMETRY=read(D/(run['GeometryName']+'-geometry.json'))
        q=prior.old.quality(run);first=prior.first_whole_gate(run)
        q['FirstWholeGateSeconds']=first['Input']['Time'] if first else None
        q['Geometry']=run['GeometryName'];quality[entry['Name']]=q
    packed('native-quality.json.gz',quality)
    write('raw-manifest.json',dict(Directory=str(raw.resolve()),Availability='Full raw local only. Git has selected native replays and all hashes/metrics. Raw is not reconstructible from Git without rerunning native trials.',Runs=manifest))
    request_audit={r['RawName']:r for r in read(D/'postflight-request-audit.json')['Checks']}
    for row in pairs:
        state=states[row['State']];search=searches[row['State']]['Search'];base=quality['baseline-'+state['Case']['Name']]
        row['Pool']='confirm' if state['Case']['Name'].startswith('V-') else 'diagnostic'
        row['Geometry']=state['Case']['Geometry'];row['Time']=state['Time'];row['GainTwoSeconds']=gain(row)
        first=quality[row['RawName']]['FirstWholeGateSeconds'];row['FirstGateSeconds']=first
        row['BaselineFirstGateSeconds']=base['FirstWholeGateSeconds'];row['GainFirstSeconds']=base['FirstWholeGateSeconds']-first if first is not None and base['FirstWholeGateSeconds'] is not None else None
        row['DifferentFirstAction']=row['Actions'][0]!=search['Baseline']['Action']
        first_candidate=next(c for c in search['Baseline']['Candidates'] if c['Action']==row['Actions'][0]);row['FirstActionHeldEvaluation']=first_candidate
        # Native feedback starts to change the predicted held tail at this activation.
        cutoff=min(state['Time']+len(row['Actions'])*.2+.24,state['Time']+.9,row['Native']['Native']['DurationSeconds'])
        comparable=[x for x in row['Accuracy']['Errors'] if x['Time']<cutoff-1e-8 and x['Compared']]
        missing=[x for x in row['Accuracy']['Errors'] if x['Time']<cutoff-1e-8 and not x['Compared'] and x['Required']]
        row['RequestsBeforeStopExact']=row['ExactRequests'] or request_audit.get(row['RawName'],{}).get('Passed',False)
        row['MatchedCommandPrefixAccuracy']=dict(Compared=len(comparable),MissingRequired=len(missing),Passed=not missing and all(x['Passed'] for x in comparable),UntilSeconds=cutoff,
            MaximumXY=max(x['XY'] for x in comparable),MaximumSpeed=max(x['Speed'] for x in comparable),MaximumBetaDegrees=max(x['Beta'] for x in comparable))
        del row['Accuracy']
    packed('paired-results.json.gz',pairs)
    fields=['State','Pool','Geometry','Time','Arm','RawName','GainTwoSeconds','GainFirstSeconds','FirstGateSeconds','BaselineFirstGateSeconds','DifferentFirstAction']
    with (D/'paired-times.csv').open('w',newline='',encoding='utf-8') as stream:
        writer=csv.DictWriter(stream,fieldnames=fields+['BaselineTwoSeconds','TrialTwoSeconds','Outcome','MinimumCertificateMeters']);writer.writeheader()
        for r in pairs:writer.writerow(dict({k:r[k] for k in fields},BaselineTwoSeconds=r['Baseline']['Native']['GateSeconds'],TrialTwoSeconds=r['Native']['Native']['GateSeconds'],Outcome=r['Native']['Native']['Outcome'],MinimumCertificateMeters=r['Native']['Native']['MinimumClearanceMeters']))
    causal=[r for r in pairs if r['Arm'] in ['B','C']]
    winners=[r for r in causal if gain(r) is not None and gain(r)>=.15-1e-8]
    unique=lambda rows:{(states[r['State']]['Case']['Name'],round(r['Time'],8)) for r in rows}
    diag=[r for r in winners if r['Pool']=='diagnostic'];confirm=[r for r in winners if r['Pool']=='confirm']
    new_dnf=[r for r in causal if r['Baseline']['Native']['Completed'] and not r['Native']['Native']['Completed']]
    max_cpu=max(s['Search']['CpuSeconds'] for s in searches.values());max_contacts=max(s['Search']['Contacts'] for s in searches.values())
    criteria=dict(DiagnosticThree=len(unique(diag))>=3,IndependentThree=len(unique(confirm))>=3,IndependentTwoGeometries=len({r['Geometry'] for r in confirm})>=2,
        NoNewCausalDNF=not new_dnf,PredictionPreflight=preflight['Passed'],PrefixAndQueue=all(r['ExactPrefix'] and r['RequestsBeforeStopExact'] for r in pairs),MatchedPrefixAccuracy=all(r['MatchedCommandPrefixAccuracy']['Passed'] for r in pairs),SearchCpu=max_cpu<=.25,SearchContacts=max_contacts<=15000)
    verdict='GO' if all(criteria.values()) else 'NO-GO' if not preflight['Passed'] or not any(gain(r) is not None and gain(r)>=.15 for r in pairs) else 'INCONCLUSIVE'
    oracle=[]
    for name,state in states.items():
        rows=[r for r in pairs if r['State']==name];legal=[r for r in rows if r['Native']['Native']['Completed']]
        best=min(legal,key=lambda r:r['Native']['Native']['GateSeconds'],default=None);base=rows[0]['Baseline']['Native']
        best_time=min([base['GateSeconds']] if base['Completed'] else []+[]) if best is None and base['Completed'] else None
        if best:best_time=min(best['Native']['Native']['GateSeconds'],base['GateSeconds']) if base['Completed'] else best['Native']['Native']['GateSeconds']
        oracle.append(dict(State=name,Scope='offline oracle / sampled upper bound: best actual result among A/B/C/one extra sampled triple, no tuning',BestTime=best_time,BestArm='A' if base['Completed'] and best_time==base['GateSeconds'] else best['Arm'] if best else None))
    write('sampled-oracle.json',oracle)
    summary=dict(Verdict=verdict,Criteria=criteria,DiagnosticWinningUniqueStates=len(unique(diag)),IndependentWinningUniqueStates=len(unique(confirm)),IndependentWinningGeometries=sorted({r['Geometry'] for r in confirm}),
        NewCausalDNF=[r['RawName'] for r in new_dnf],MaximumSearchCpuSeconds=max_cpu,MaximumSearchContacts=max_contacts,
        NativeWorlds=sum(e['NativeWorlds'] for e in ledger),CpuSeconds=sum(e['CpuSeconds'] for e in ledger),Forecasts=sum(e['Forecasts'] for e in ledger),ContactEvaluations=sum(e['Evaluations'] for e in ledger),
        NativeSteps=sum(e.get('Metrics',{}).get('Native',{}).get('NativeSteps',0) for e in ledger if e.get('Metrics')),FirstActionChangedWins=sum(r['DifferentFirstAction'] for r in winners),
        PolicyId=16015,RawDirectory=str(raw.resolve()),CpuScope='Native episodes and explicitly reserved offline searches; excludes builds, tests, compression and this read-only report.')
    write('analysis.json',summary)
    # Illustrative selection AFTER all results, never a causal policy/model selection.
    best=max(winners,key=lambda r:gain(r),default=causal[0]);failed=next((r for r in pairs if not r['Native']['Native']['Completed']),pairs[0])
    chosen=list(dict.fromkeys([best['State'],failed['State'],next(r['State'] for r in pairs if r['Geometry']=='progressive-c'),next(r['State'] for r in pairs if r['Geometry']=='egg-asymmetric'),(confirm[0] if confirm else next(r for r in causal if r['Pool']=='confirm'))['State']]))
    bundle={};groups=[];time_gains=[]
    for name in chosen:
        state=states[name];rows=[r for r in pairs if r['State']==name];runs=[dict(Name='baseline-'+state['Case']['Name'],Policy='A')]+[dict(Name=r['RawName'],Policy=r['Arm']) for r in rows]
        for r in runs:
            if r['Name'] not in bundle:bundle[r['Name']]=read(raw/(r['Name']+'.json.gz'))
        plans=[dict(Arm=r['Arm'],Time=state['Time'],ReactionSeconds=.24,Actions=r['Actions'],Basis='Przyczynowy wybór H09 przed natywną próbą' if r['Arm']!='oracle-sample' else 'Dodatkowa próbka wybrana według prognozowanego postępu; wynik tylko do retrospektywnego oracle') for r in rows]
        paired='; '.join(f"{r['Arm']}: {fmt(r['Native']['Native']['GateSeconds'])} s / {r['Native']['Native']['Outcome']}" for r in rows)
        groups.append(dict(Id=name,Scenario=state['Case']['Scenario'],GeometryFile=state['Case']['Geometry']+'-geometry.json',TwoBends=True,Runs=runs,Plans=plans,
            Reasons=[state['Slot'],state['Case']['Geometry'],state['Case']['Name']],Scope='Jednakowy natywny prefiks; sekwencja dwóch/trzech żądań, potem oryginalny autonomiczny134A. Oracle nie jest sterownikiem.',
            PairedSummary=f"Pełny czas dwóch łuków A: {fmt(rows[0]['Baseline']['Native']['GateSeconds'])} s; {paired}. Wszystkie DNF zachowane."))
        a=bundle[runs[0]['Name']]['Replay']['Samples'];refs=[s['Input']['Rider']['TotalReferenceMeters'] for s in a]
        samples=[];trial=bundle[runs[1]['Name']]['Replay']['Samples'];trial_refs=[s['Input']['Rider']['TotalReferenceMeters'] for s in trial]
        for ref in range(30,int(min(refs[-1],trial_refs[-1])),5):
            ai=bisect.bisect_left(refs,ref);bi=bisect.bisect_left(trial_refs,ref)
            if ai>=len(a) or bi>=len(trial):continue
            sa,sb=a[ai],trial[bi]
            samples.append(dict(ReferenceMeters=ref,OldTime=sa['Input']['Time'],NewTime=sb['Input']['Time'],GainSeconds=sa['Input']['Time']-sb['Input']['Time'],OldXY=sa['Input']['Rider']['Position'],NewXY=sb['Input']['Rider']['Position']))
        time_gains.append(dict(Group=name,Policy='B',Samples=samples,Description='Zysk czasu B względem A przy pierwszym natywnym przekroczeniu tego samego postępu co5m; rozdzielczość0,01s, rzeczywiste XY'))
    packed('selected-replays.json.gz',dict(Runs=bundle));write('time-gains.json',time_gains)
    write('index.json',dict(Title='Multi-Action Planning Feasibility #135',Grade=verdict,DefaultRun=groups[0]['Runs'][1]['Name'],PreferredPolicy='B',RawDirectory=str(raw.resolve()),
        Labels={'A':'#134 A · jedna utrzymana decyzja','B':'#135 B · dwie decyzje','C':'#135 C · trzy decyzje','oracle-sample':'Próbka offline oracle · bez selekcji przyczynowej'},
        Colors={'A':'#ffd27b','B':'#73dfdf','C':'#d99fe3','oracle-sample':'#ff9c86'},Horizons={k:.9 for k in ['A','B','C','oracle-sample']},TimeGain='time-gains.json',TimeGainPolicy='B',ComparisonCharts=True,Charts=[['paired-times.svg','Wszystkie sparowane czasy'],['trajectories.svg','Wybrane natywne trajektorie']],Groups=groups))
    controls=read(D/'mechanism-controls.json');mechanism=read(D/'mechanism-audit-plan.json')
    group=next(g for g in groups if g['Id']==mechanism['State'])
    for c in controls:
        bundle[c['Name']]=read(raw/(c['Name']+'.json.gz'));group['Runs'].append(dict(Name=c['Name'],Policy=c['Arm']))
        group['Plans'].append(dict(Arm=c['Arm'],Time=mechanism['Time'],ReactionSeconds=.24,Actions=c['Actions'],Basis='Kontrola mechanizmu po wynikach; poza niezależnym wynikiem i wyborem planera'))
        group['PairedSummary']+=f" Kontrola {c['Arm']}: {fmt(c['Metrics']['Native']['GateSeconds'])} / {c['Metrics']['Native']['Outcome']}."
    packed('selected-replays.json.gz',dict(Runs=bundle))
    index=read(D/'index.json');index['Groups']=groups
    index['Labels'].update({'first-only':'Kontrola: tylko pierwsza akcja','repeat-first':'Kontrola: powtórzona pierwsza akcja'})
    index['Colors'].update({'first-only':'#8bb3ef','repeat-first':'#ed6565'});index['Horizons'].update({'first-only':.9,'repeat-first':.9});write('index.json',index)
    plots(pairs,groups,bundle)
    for filename in ['paired-times.svg','trajectories.svg']:
        path=D/filename;path.write_text('\n'.join(line.rstrip() for line in path.read_text(encoding='utf-8').splitlines())+'\n',encoding='utf-8',newline='\n')
    report(summary,protocol,pairs,searches,oracle,preflight)
    for name in ['diagnostic-search','confirm-search','diagnostic-pairs','confirm-pairs']:
        path=D/(name+'.json');packed(name+'.json.gz',read(path));path.unlink(missing_ok=True) # Derived compact Git packaging; all original raw retained outside Git.
    write('report-cost.json',dict(CpuSeconds=time.process_time()-started,Scope='Read-only aggregation, compression and plotting; separate from experiment budget'))
    print(json.dumps(summary,ensure_ascii=False,indent=2))
def plots(pairs,groups,bundle):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    preview=ROOT/'work/planning-preview';preview.mkdir(parents=True,exist_ok=True)
    names=list(dict.fromkeys(r['State'] for r in pairs));fig,(ax,failax)=plt.subplots(2,1,sharex=True,figsize=(11,8),gridspec_kw={'height_ratios':[4,1]})
    for arm_index,(arm,color) in enumerate([('B','#168c8c'),('C','#a346b5'),('oracle-sample','#d96949')]):
        rows=[r for r in pairs if r['Arm']==arm and gain(r) is not None];x=[names.index(r['State']) for r in rows];y=[gain(r) for r in rows]
        ax.scatter(x,y,label=arm,color=color,marker='o',s=35)
        failed=[r for r in pairs if r['Arm']==arm and gain(r) is None];failax.scatter([names.index(r['State']) for r in failed],[arm_index]*len(failed),color=color,marker='x',s=35)
    failax.set_yticks([0,1,2],['B','C','oracle'],fontsize=8);failax.set_ylim(-.5,2.5);failax.set_ylabel('DNF\nbez czasu');failax.set_xticks(range(len(names)),names,rotation=70,ha='right',fontsize=7)
    ax.axhline(.15,color='green',linestyle='--',label='próg +0,15 s');ax.axhline(0,color='gray')
    ax.set_ylabel('Zysk pełnego czasu dwóch łuków względem A [s]');ax.set_title('Natywne kontrfaktyczne próby; DNF oznaczone osobno');ax.legend();fig.tight_layout();fig.savefig(D/'paired-times.svg');fig.savefig(preview/'paired-times.png');plt.close(fig)
    fig,axes=plt.subplots(1,len(groups),figsize=(5*len(groups),4),squeeze=False)
    for ax,g in zip(axes[0],groups):
        geometry=read(D/g['GeometryFile']);sections=[q for segment in geometry['segments'] for q in segment['sections']]
        for edge in ['inner','outer']:ax.plot([q[edge]['x'] for q in sections],[q[edge]['y'] for q in sections],color='#776a50',linewidth=1)
        for r,color in zip(g['Runs'],['#c29127','#168c8c','#a346b5','#d96949','#5487bb','#c03d3d']):
            samples=bundle[r['Name']]['Replay']['Samples'];ax.plot([s['Input']['Rider']['Position']['X'] for s in samples],[s['Input']['Rider']['Position']['Y'] for s in samples],label=r['Policy'],color=color,linewidth=1)
        ax.set_aspect('equal');ax.set_title(g['Id'],fontsize=8);ax.set_xlabel('x [m]');ax.set_ylabel('y [m]');ax.legend(fontsize=7)
    fig.tight_layout();fig.savefig(D/'trajectories.svg');fig.savefig(preview/'trajectories.png');plt.close(fig)
def report(s,p,rows,searches,oracle,preflight):
    sequence_cost=read(D/'sequence-cost.json');costs=[c['CpuSecondsPerSequence'] for c in sequence_cost['Checks']]
    controls=read(D/'mechanism-controls.json');mechanism_table=table(['V-C-preparation2','Dwa łuki [s]','Wynik'],[['Oryginalny A','15.590','completed'],['Wybrany B: −15°/gaz0 → −5°/gaz1','15.340','completed']]+[[c['Arm'],fmt(c['Metrics']['Native']['GateSeconds']),c['Metrics']['Native']['Outcome']] for c in controls])
    causal=[r for r in rows if r['Arm'] in ['B','C']];winning=[r for r in causal if gain(r) is not None and gain(r)>=.15-1e-8]
    examples=sorted(winning,key=lambda r:gain(r),reverse=True)[:5]
    table_rows=[[r['State'],r['Arm'],r['Pool'],fmt(r['Baseline']['Native']['GateSeconds']),fmt(r['Native']['Native']['GateSeconds']),fmt(gain(r)),r['Native']['Native']['Outcome']] for r in rows]
    ex=table(['Stan','Wariant','A dwa łuki [s]','Próba [s]','Zysk [s]','Pierwsza akcja inna niż A'],[[r['State'],r['Arm'],fmt(r['Baseline']['Native']['GateSeconds']),fmt(r['Native']['Native']['GateSeconds']),fmt(gain(r)),r['DifferentFirstAction']] for r in examples])
    by_arm=table(['Pula','Ramię','Ukończenia','Zyski ≥0,15 s','Najgorsza strata na wspólnych ukończeniach [s]'],[[pool,arm,f"{sum(r['Native']['Native']['Completed'] for r in rr)}/{len(rr)}",sum(gain(r) is not None and gain(r)>=.15-1e-8 for r in rr),fmt(min((gain(r) for r in rr if gain(r) is not None),default=None))] for pool in ['diagnostic','confirm'] for arm in ['B','C','oracle-sample'] for rr in [[r for r in rows if r['Pool']==pool and r['Arm']==arm]]])
    text=f'''# PR #135 — Multi-Action Planning Feasibility

**{s['Verdict']}**. PROVISIONAL, domyślnie OFF. Dokładna baza `{p['BaseHead']}`, polityka **#134 A #16015**, oryginalne osiem wag, H09 i pełny gaz. Bez CEM, Track Awareness ani zmian CoreSim, geometrii, silnika, opon, reakcji i aktuatora. Wynik małego zbioru diagnostycznego nie jest walidacją gotowego kierowcy.

## 1. Jak działa pojedyncza decyzja?
`FreeLearningController.Choose()` prognozuje każdą akcję przez0,9s po pełnej reakcji i utrzymuje ją do końca, z wcześniej oczekującymi komendami. `Predict()` ma callback decyzji z przewidywanych obserwacji, ale #134 A go nie używa. W natywnym świecie A ponownie ocenia sterowanie co0,2s. Off odtworzył dokładny fingerprint #134 A N00; każdy kandydat ma test identyczności skopiowanej funkcji kosztu.

## 2. Czy utrzymana komenda powodowała zły wybór?
Liczba sparowanych wyników B/C z inną pierwszą akcją niż A i zyskiem co najmniej0,15s całych dwóch łuków: **{s['FirstActionChangedWins']}**. Konkretne dowody poniżej. To kontrfaktyczna dostępność lepszego manewru w ograniczonym zbiorze, nie dowód globalnej optymalności. `paired-results.json.gz` zachowuje dawną ocenę utrzymania tej pierwszej akcji i nową ocenę całej sekwencji.

{ex}

W czterech z pięciu powyższych korzystnych przypadków sekwencja tylko powtarza tę samą pierwszą akcję A. To dowód korzyści z chwilowego utrzymania komendy względem późniejszych korekt feedbacku, **nie samodzielny dowód potrzeby różnych kolejnych akcji**. Mocniejszy bezpośredni przypadek jest jeden: V-C-preparation2 B, gdzie A odrzuca pierwszą akcję −15°/gaz0, a planner wybiera ją z dalszą −5°/gaz1.

Po zakończeniu całej puli wykonano dwie dodatkowe kontrole mechanizmu, zamrożone przed ich wynikami w `mechanism-audit-plan.json`. Mają status **post-hoc diagnostic**, bez doliczania do niezależnych sukcesów i bez dostrajania planera:

{mechanism_table}

Sama pierwsza akcja daje0,10s zysku, a dobrana druga kolejne0,15s. Powtórzenie pierwszej kończy się przekroczeniem β45° przy dodatnim prześwicie. W tym jednym stanie istnieje więc natywny dowód, że dalsza komenda ma znaczenie. Nadal nie jest to kilka niezależnych potwierdzeń architektury MPC.

## 3. Czy dwie decyzje pomagają?
Wyniki wszystkich przypadków, bez usuwania DNF:

{by_arm}

## 4. Czy trzy dają dodatkową korzyść?
Porównanie B/C w tym samym stanie jest dostępne w każdej parze poniżej. Więcej gałęzi nie gwarantuje lepszego pełnego czasu; terminalny koszt H09 potrafi preferować krótkotrwałą poprawę, po której dalszy feedback jedzie gorzej. Zbiór trójek jest ograniczony, nie zawiera wszystkich możliwych manewrów.

## 5. Czy potwierdza to natywny Box2D?
Każda próba odtwarza cały świat od pierwotnego początku. Nie przybliżamy snapshotu. Po interwencji zachowany jest ten sam WorldRunner, nawierzchnia, wheel/steering, reakcja i nieodwołalna kolejka. Wszystkie sprawdzenia fizycznego prefiksu przed pierwszą aktywacją oraz żądań i aktywacji wymaganych przed końcem świata: {all(r['ExactPrefix'] and r['RequestsBeforeStopExact'] for r in rows)}. Certyfikat całego obrysu, β45°, reverse/projection i testy energii są dokładnie odziedziczone; nie zaostrzano ani nie luzowano marginesów.

Pierwotny końcowy guard zatrzymał suite na V-E-preparation2 C: wymagał także aktywacji trzeciej komendy przypadającej po natywnym DNF. Zachowano `confirm-pairs-initial.json.gz` i pierwotne `ExactRequests=false`. Osobny `postflight-request-audit.json` sprawdza wyłącznie komendy przed końcem świata; późniejsza pending pozostaje niewykonana. Dopiero potem wykonano jedną pozostałą, już wcześniej wybraną próbkę oracle. Żadnego świata ani wyszukiwania nie powtórzono; zamrożony planner, pule i progi nie uległy zmianie. Hash kodu audytu jest zapisany oddzielnie.

## 6. Czy planner znajduje poprawy sam?
Tak oznaczone B/C wybrano wyłącznie na podstawie stanu przy obserwacji i H09, zapisując wybór przed natywnym wynikiem. Nie wybieramy B albo C po finale jako nowego sterownika. Oddzielny `sampled-oracle.json` to najlepszy rzeczywisty wynik w skończonym zbiorze A/B/C/dodatkowej próbki. Oracle nie jest wdrożoną polityką i nie dostraja planera.

## 7. Reakcja i pending
Reakcja pochodzi z `RiderController.ReactionSeconds(spec)`; dla tego zawodnika {preflight['ReactionSeconds']:.2f}s. Żądania t/t+0,2/t+0,4 aktywują się t+0,24/t+0,44/t+0,64. Starsze pending aktywują się wcześniej i nie są anulowane. Kierownica nadal≤±35° i90°/s. Jedna sekwencja jest wybrana przy t; po jej żądaniach wraca autonomiczny #134 A na rzeczywistych obserwacjach. To przyczynowy prototyp planu lokalnego, nie pełne receding-horizon MPC przez cały bieg.

## 8. Co ogranicza bezpieczne manewry?
Pozostaje mała obwiednia modelu, opóźniony aktuator, nadchodząca zmiana krzywizny/szerokości oraz niezgodność krótkiej preferencji terminalnej z dalszym feedbackiem. Klasa SafeInHorizon nie dowodzi legalności całego łuku. DNF i przyczyny są w tabeli, bez nazywania przekroczenia β potwierdzonym fizycznym upadkiem. Brak nowych DNF dotyczy zamrożonych B/C; dodatkowa kontrola repeat-first ma własny nowy DNF i pozostaje jawnie odrębna.

## 9. Prosta, pierwszy i drugi łuk
Zamrożono16 stanów diagnostycznych i6 potwierdzających: koniec prostej, wejście/środek/wyjście łuku, prosta między łukami, przygotowanie i wejście drugiego, blisko granicy i maksimum yaw. Dokładne stany w plikach `*-states.json`; powtarzające się fazy mogą wskazać tę samą obserwację. Kryteria liczą unikalne pary(case,time), nie aliasy stanu. Pierwsza bramka jest mierzona pełnym obrysem; pełne czasy dotyczą drugiej bramki oraz dodatkowej1s legalnej kontroli po niej.

## 10. Geometria i transfer
Każda próba używa istniejącego progressive-b, progressive-c lub egg-asymmetric i własnego eksportu geometrii. Nie zakładamy idealnych półokręgów. B ma12→16,6→12m, C12→17/16,2→12m. Parametry apexu nie są pomiarami Motoareny. Zgodność komend z lokalnym gridem nie dowodzi przenoszalności jednego planu XY na inny tor: w każdej geometrii planner sam ocenia istniejące akcje.

Test odczytowy potwierdza obecną prostą35+27m, drugą62m i okrążenie≈318,78m. Uzgodnione31+31m pozostaje **osobnym zadaniem przywrócenia położenia start/meta** w `start-finish-follow-up.md`. Origin nadal(0,0), cztery neutralne pola A–D mają środki1,5/4,5/7,5/10,5m; bez bonusów i bez stałych pasów jazdy. #135 nie przesuwa startu, bandy ani kredy.

## 11. Koszt sekwencji
Maksimum jednego całego wyszukiwania: **{s['MaximumSearchCpuSeconds']:.6f}s CPU / {s['MaximumSearchContacts']} ocen kontaktu**; obejmuje ocenę bazowego gridu oraz≤12 par i≤24 trójek. `*-search.json.gz` ma koszt każdego stanu, liczbę prognoz, każdą gałąź i prognozowane punkty (jawnie oddzielone od Native). Koszt jednej liściowej prognozy można sprawdzić w `Prediction.ForceEvaluations`; H09 około90 przedziałów. CPU obejmuje lokalne wyszukiwanie, bez tworzenia świata Box2D. Maksimum obejmuje koszt zimnego procesu/JIT; nie zastępujemy zamrożonego progu późniejszą korzystniejszą medianą.

Osobny postflight `sequence-cost.json`:16 partii B/C na8 różnych obserwacjach, po1 warmup i20 powtórzeń. Zmierzony koszt jednej sekwencji: **{min(costs):.6f}…{max(costs):.6f}s CPU**, mediana **{statistics.median(costs):.6f}s**. Timer CPU jest kwantowany; są to średnie20 prognoz, nie sztuczna dokładność pojedynczego timera. Całe336 prognoz i warmupy wliczono do budżetu;0 dodatkowych światów. Pomiar nie zmienia wyboru ani werdyktu.

## 12. Przyszłych czterech zawodników
Przy5 obserwacjach/s i4 zawodnikach konserwatywna ekstrapolacja maksimum to **{s['MaximumSearchCpuSeconds']*20:.3f} CPU-s na sekundę symulacji**, bez kosztu natywnej fizyki, ruchu rywali i interakcji. To ekstrapolacja, nie zmierzony bieg czterech zawodników. Możliwe są współdzielone prefiksy/pruning; nie obiecujemy przyspieszenia bez pomiaru.

## 13. Czy stara funkcja celu wystarcza?
Nie została nauczona wartości kontynuacji. Zachowaliśmy jej wagi i literalny wzór, odnosząc preferencje gazu/zmiany skrętu do pierwszej akcji; H09 terminalny postęp/kierunek/yaw pochodzi z całej sekwencji. Późniejsze kosztowne akcje mogą więc być za tanie w tej ocenie. To jawna granica prototypu, nie przemilczana nowa kara ani dowód, że wystarczy dodać gałęzie. Native rozstrzyga rzeczywisty czas i bezpieczeństwo.

## 14. Czy H09 wystarcza?
Trzecia komenda ma tylko0,26s skutku w H09. Cztery kontrolowane natywne próby utrzymują ostatnią akcję po sekwencji przez całeH09; dotychczasowe tolerancje XY0,1m, v0,2m/s, χ/ψ/β1°, steering1,5°, wheel2rad/s, yaw0,05rad/s, optimism0,1m pozostały bez zmian. W głównych próbach prognoza i Native zgadzają się tylko do aktywacji pierwszej przywróconej komendy A: porównujemy ten zgodny prefiks osobno. Różnica później jest zmianą sterowania, nie automatycznie błędem fizyki. Nie wydłużono MaximumHorizonSeconds i nie certyfikujemy całej przyszłości z H09.

## 15. Decyzja dotycząca #136
**{s['Verdict']}** według progów zamrożonych w protokole przed próbami. Kryteria: `{json.dumps(s['Criteria'],ensure_ascii=False)}`. Diagnostyka: {s['DiagnosticWinningUniqueStates']} unikalnych stanów zysku≥0,15s; niezależna pula: {s['IndependentWinningUniqueStates']} w geometriach {s['IndependentWinningGeometries']}. Nowe causal DNF: {len(s['NewCausalDNF'])}. Oddzielne światy z jednego początku nie są niezależnymi osobnikami ani statystycznym dowodem gotowości. Nie otwieramy automatycznie #136. Następny pojedynczy eksperyment: zweryfikować wartość późniejszej kontynuacji na tych samych zamrożonych wagach i modelu, zanim zaczniemy duży trening lub domyślne MPC.

Koszt łącznie: **{s['NativeWorlds']} światów, {s['CpuSeconds']:.6f}s CPU epizodów/wyszukiwań, {s['Forecasts']} prognoz, {s['ContactEvaluations']} ocen kontaktu, {s['NativeSteps']} kroków**. Limity96/1400s/250k/25M. Budżet CPU wynika z6 światów preflight i zamrożonej formuły, nie z dostosowania po wynikach. Builds, testy, przygotowanie geometrii przed pomiarem epizodu, serializacja raw i ten raport są poza pomiarem epizodów; koszt raportu jest osobno. Nie zmierzono sumy CPU całych procesów z tym narzutem, więc783s ani późniejsza suma nie są takim pomiarem.

Pełny raw: `{s['RawDirectory']}`. Dostęp lokalny; Git zawiera wszystkie agregaty, prognozy, hashe i wybrany bundle, **nie każdy pełny replay**. Historyczne dane i źródła #129–#134 pozostały bez zmian. `tools/run-multi-action.ps1` odtwarza etapy do świeżych katalogów. Standardowy Windows Draft CI i dokładny HEAD są w opisie PR, bez merge. Testy fizyki/OFF i nowe kontrakty muszą przejść przed publikacją.

## Wszystkie sparowane wyniki

Dodatni zysk oznacza szybszy legalny pełny przejazd; przy DNF nie ma legalnego czasu.

{table(['Stan','Ramię','Pula','A [s]','Próba [s]','Zysk [s]','Wynik'],table_rows)}

[Protokół](protocol.json) · [Preflight](preflight.json) · [Koszt](attempts.json) · [Wszystkie pary](paired-times.csv) · [Oracle](sampled-oracle.json) · [Pełne metryki](native-quality.json.gz) · [Manifest raw](raw-manifest.json)

![Sparowane czasy](paired-times.svg)

![Rzeczywiste trajektorie](trajectories.svg)
'''
    text+='\nDodatkowy audyt źródeł CI: `source-line-endings.json` wiąże oryginalne surowe SHA z hashami po jedynej normalizacji CRLF→LF. Lokalne core.autocrlf=true i CI=false różnią się bajtami końców linii. Wszystkie oryginalne surowe hashe sprawdzono przed utworzeniem tego odwzorowania; żadnych innych znaków kodu nie pomija się. Oryginalny protokół i locki źródeł zachowano. Odtworzenie do nowych katalogów zamraża bieżące bajty; wznowienie starego runu nadal wymaga zgodnych surowych źródeł.\n'
    text=re.sub(r'(?m)^(## .+)\n(?!\n)',r'\1\n\n',text)
    (D/'README.md').write_text(text,encoding='utf-8')
    (D/'start-finish-follow-up.md').write_text('''# Oddzielne zadanie: przywrócenie uzgodnionego start/meta

Odczyt135 potwierdza istniejący podział35/27m, wobec uzgodnionego31/31m, z neutralnym origin(0,0). Nie zmieniono współrzędnych w135. Osobne przyszłe zadanie wymaga zachowania całej62m prostej, dwóch łuków, pólA–D, referencji/metryki zamknięcia i jawnego przeliczenia historycznych wejść. Ten zapis nie uruchamia automatycznie nowego PR ani migracji danych.
''',encoding='utf-8')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('raw');parser.add_argument('--data',type=Path,default=D);args=parser.parse_args();D=args.data;main(args.raw)
