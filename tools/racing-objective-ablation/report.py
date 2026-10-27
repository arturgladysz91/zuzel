"""Compact the existing aggregate and describe observations; never predict or create worlds."""
from pathlib import Path
import hashlib
import json
import re
import time

ROOT=Path(__file__).resolve().parents[2]
P=ROOT/'docs/experiments/racing-objective-ablation/results.json'
started=time.process_time()
r=json.loads(P.read_text(encoding='utf-8'))
self_name=str(Path(__file__).relative_to(ROOT)).replace('\\','/')
self_hash=hashlib.sha256(Path(__file__).read_bytes().replace(b'\r\n',b'\n')).hexdigest().upper()
for name,digest in r['Protocol']['Sources'].items():
    if name==self_name:continue
    assert hashlib.sha256((ROOT/name).read_text(encoding='utf-8').replace('\r\n','\n').encode()).hexdigest().upper()==digest,name
if r['Protocol']['Sources'].get(self_name)!=self_hash:
    revision={'PreviousFrozenReporterSha256':r['Protocol']['Sources'].get(self_name),
        'NewReporterSha256':self_hash,'NativeWorlds':0,'NewForecasts':0,'Meaning':'Reporting-only revision after all24 worlds to include refined-boundary outcomes. Every runtime objective, physics, comparison and dispatcher hash stays exact.'}
    history=r.setdefault('PostRunReportingRevisions',[])
    if r.get('PostRunReportingRevision') and not history:history.append(r['PostRunReportingRevision'])
    history.append(revision)
    r['PostRunReportingRevision']=revision
    r['Protocol']['Sources'][self_name]=self_hash
rows={n['Name']:n for n in r['Native']}
main=[rows[c+'-'+mode] for c in ('D0','D1','H0','H1') for mode in ('A','B','C')]
branches=[]
for name,base in [('branch-D0-clock-corrected','D0-A'),('branch-D1','D1-A')]:
    a,b=rows[base],rows[name]
    assert b['ExactAcceptedBranchPrefix'] and b['BranchStateObserved']
    assert all(x['ExactCachedPredecision'] for x in b['BranchObservations'])
    branches.append({'Name':name,'Original':base,'Removed':b['IsolatedRemovedPenalty'],
        'FullTimeASeconds':a['FullTimeSeconds'],'FullTimeAlternativeSeconds':b['FullTimeSeconds'],
        'TimeGainSeconds':a['FullTimeSeconds']-b['FullTimeSeconds'],
        'FullPathDistanceDifferenceMeters':b['FullPhysicalDistanceMeters']-a['FullPhysicalDistanceMeters'],
        'BothFullScopesLegal':a['Completed'] and b['Completed'],
        'DeltaKphAtStudiedMilestones':{k:b[k]['SpeedKph']-a[k]['SpeedKph'] for k in ['Exit','Plus20','Plus40','Plus62']},
        'Meaning':'One changed request with unchanged full reaction/pending, then fresh original A decisions. This is an isolated diagnostic, not an autonomous globally ablated policy or an additional independent original A world.'})

def at_review_reference(n,reference):
    ds=n['Decisions']
    for a,b in zip(ds,ds[1:]):
        if a['ReferenceMeters']<=reference<=b['ReferenceMeters']:
            q=(reference-a['ReferenceMeters'])/(b['ReferenceMeters']-a['ReferenceMeters'])
            return {'TargetReferenceMeters':reference,'SpeedKph':a['SpeedKph']+q*(b['SpeedKph']-a['SpeedKph']),
                'TimeSeconds':a['Time']+q*(b['Time']-a['Time']),
                'BracketBefore':{k:a[k] for k in ['Time','ReferenceMeters','SpeedKph','BetaDegrees','YawRate']},
                'BracketAfter':{k:b[k] for k in ['Time','ReferenceMeters','SpeedKph','BetaDegrees','YawRate']},
                'Meaning':'Secondary local branch diagnostic: scalar interpolation of actual accepted decision observations at0.2s cadence. Primary full-scope/milestone metrics use adjacent0.01s accepted samples. No interpolated state enters controller.'}
    return None

# The throttle intervention is on the FIRST straight; D1 main scope measures the SECOND.
first_exit=rows['D0-A']['Scope']['StudiedExitReference']
branches[1]['FirstStraightLocalMeasurements']={}
for distance in [20,40,62]:
    branches[1]['FirstStraightLocalMeasurements'][str(distance)]={'Original':at_review_reference(rows['D1-A'],first_exit+distance),
        'Alternative':at_review_reference(rows['branch-D1'],first_exit+distance)}

margin=[]
recover=[]
for case in ['D0','D1','H0','H1']:
    a,b,c=(rows[case+'-'+m] for m in ['A','B','C'])
    first=next(((x,y) for x,y in zip(b['Decisions'],c['Decisions']) if x['Request']!=y['Request']),None)
    if first:
        x,y=first
        margin.append({'Case':case,'TimeSeconds':x['Time'],'B':x,'C':y,
            'Meaning':'Before first divergence, same actual observations and pending; C promotes a nominally hard-admissible Uncertain candidate over Safe. Later controller infeasibility is separate from actual DNF.'})
    if case in ['D0','D1']:
        experiments=[rows[k] for k in rows if k.startswith('recovery-'+case+'-')]
        recover.append({'Case':case,'OriginalAFromZeroCompletes':a['Completed'],
            'TestedSwitches':[{'SwitchAtSeconds':n['ObjectiveSwitchAtSeconds'],'Outcome':n['Outcome'],'EndTimeSeconds':n['NativeEndTimeSeconds'],
                'Method':'Autonomous B until fixed diagnostic time, then autonomous A; one continuous world, no state/pending reset.'} for n in experiments],
            'Conclusion':'Both tested late switches fail. For this A recovery policy, damage is already present by the earlier tested time; no exact global irreversible frontier or impossibility for all controls is claimed. H09 all-invalid refers only to the finite held-action set.'})

native_failures=[n for n in r['Native'] if n['PhysicalDnf']]
for n in r['Native']:
    n['NativeGuardStop']=n['PhysicalDnf']
    n['ConfirmedSampledFootprintCrossing']=min(n['RobustMetrics']['MinimumInstantInner'],n['RobustMetrics']['MinimumInstantOuter'])<0
    if n['Outcome']=='uncertified interval':
        n['BoundaryInterpretation']='Instantaneous accepted footprint clearances remain positive; original conservative swept lower bound is negative. This is a failure to certify the interval, not a confirmed sampled footprint crossing. Native stop retained.'
    for o in n['BranchObservations']:
        if not o['ExactCachedPredecision'] and n['Name'].endswith('-A'):
            proof=next((x for x in r['ClockComparisonCorrection']['Proofs'] if x['Name']==o['Name']),None)
            if proof:o['ClockNormalizedPredecisionExact']=True
boundary=[]
for case in ['D0','H1']:
    a,b=rows[case+'-B'],rows['boundary-refined-'+case]
    def stable_decision(d):return {k:v for k,v in d.items() if k!='CpuSeconds'}
    same=[stable_decision(x) for x in a['Decisions']]==[stable_decision(x) for x in b['Decisions'][:len(a['Decisions'])]]
    assert same and b['Outcome']=='confirmed crossing'
    boundary.append({'Case':case,'OriginalMainStopSeconds':a['NativeEndTimeSeconds'],'OriginalReason':a['Outcome'],
        'RefinedStopSeconds':b['NativeEndTimeSeconds'],'RefinedOutcome':b['Outcome'],
        'RefinedMinimumInstantOuterMeters':b['RobustMetrics']['MinimumInstantOuter'],
        'ExactLoggedBDecisionPrefix':same,'IndependentConfirmation':False,
        'Meaning':'Existing refined swept certificate observes the SAME B/physics, without changing requests or accepted integration. Another0.02s confirms an actual sampled footprint crossing. Diagnostic repetition, not a replacement main result or independent held family.'})
r['BoundaryDiagnosticResults']=boundary
r['BranchComparison']=branches
r['MarginPriorityNativeComparison']=margin
r['RecoverabilityFindings']=recover
r['FieldDefinitions']={
    'PhysicalDnf':'Legacy collector boolean for a NATIVE GUARD STOP, including uncertified swept bounds and experimental beta45. It does NOT assert a real-world crash or a confirmed physical boundary crossing. NativeGuardStop is the explicit alias; FailureClassification and ConfirmedSampledFootprintCrossing distinguish evidence.',
    'ControllerForecastStop':'C has no admissible nominal H09 action. Diagnostic placeholder never activates before stopping. Current native state may remain legal. Not counted as an actual native boundary/beta/reverse DNF.',
    'Uncertain':'Caution margin from predicted clearance/closing, never an actual DNF by itself.',
    'Reverse':'Original guard triggers velocity dot road tangent<=0 OR reference delta<-0.02m. Exported generic reverse label does not distinguish these; no claim of rear-wheel reversal.',
    'Metrics':'Main same-reference scalar crossings interpolate adjacent accepted0.01s samples with brackets retained. Missing targets/times remain null. First-straight branch diagnostics derived from0.2s decision observations are explicitly secondary.',
    'Contacts':'Lower bound: aborted development world contacts were not fully exported. Its CPU is retained and forecasts reconstructed from identical original A decision prefix, including failing chooser call.',
    'Independence':'Four main initial physical families; original branch counterpart reused from main A, prefixes, diagnostic recovery switches and software restart are not independent confirmations.'}
r['FinalSummary']={
    'Verdict':'PARTIAL — individual preferences can harm legal time; blanket Progress Only is not a successful replacement',
    'MainCompletion':{m:sum(rows[c+'-'+m]['Completed'] for c in ['D0','D1','H0','H1']) for m in ['A','B','C']},
    'MainBGuardStops':{'uncertified swept-boundary interval':2,'native reverse/progress guard':1,'experimental beta45':1},
    'RefinedDiagnosticsConfirmedCrossings':2,
    'MainCControllerForecastStops':4,
    'AllNativeGuardStopsIncludingRecoveryDiagnostics':len(native_failures),
    'AllConfirmedSampledFootprintCrossings':sum(n['ConfirmedSampledFootprintCrossing'] for n in r['Native']),
    'DevelopmentAbortedWorlds':sum(a.get('AbortedBeforeIntervention',False) for a in r['Reservations']),
    'CoreFinding':'Identical physics permits a0.580683s legal full-scope improvement from removing the partial-throttle preference for one request. However all-penalty removal with H09 makes aggressive initial choices and fails all four full scopes; no evidence supports replacing the objective with greedy progress alone.',
    'ScopeLimit':'No globally autonomous no-throttle-only policy was tested. Two branch diagnostics do not justify adopting removal of individual terms everywhere. No calibrated real-motorcycle limit or exact global loss-of-recovery frontier is inferred.'}
r['Recommendations']={
    'Retain':'Actual track/collision/energy/actuator/reaction constraints; beta45 only as a frozen experimental comparability envelope. Keep native swept-certification stops distinct from confirmed sampled crossings.',
    'ThrottlePrior':'First candidate for removal: preference for partial gas has no constitutive justification; isolated full pulse saves0.581s legally in D1. Next validate autonomous no-ThrottleWeight on all four families before adoption.',
    'YawAndDirection':'Behavioral proxies, not physical laws. Direction removal saves0.027s for one request, yaw changes26/28 offline actions. Combined ablation does not identify either term as individually necessary. Do not remove all guidance of later legal continuation based only on H09 progress.',
    'SteeringGeometryClearance':'No action change from isolated removals in28 unique observations; no evidence here that these exact weights or0.8 reserve improve native time. Actuator rate is already physical. Treat as candidates for separate removal validation, not established physical necessities.',
    'TrackAwareness':'OFF in A; keep off in this experiment. Only1/18 counterfactual full pools changes on enabled-vs-removed reranking; no native-time necessity demonstrated.',
    'Uncertain':'Not DNF. Relaxing class priority admits more aggressive nominally valid actions but does not improve completion here. Keep safety observation distinct from proof of recovery.',
    'NextStep':'One autonomous no-throttle-prior A/B validation, same actions/H09/physics and full next-corner scope; no new cost terms, training or tyre changes.'}
r['Provenance']={'BaseHead':'266df048536c256ced410a1045a5924899153237','Branch':'experiment/racing-objective-ablation-142',
    'PreWorldProtocolCommit':'a3341b9','ClockComparisonCorrectionCommit':'01ec8ba','PreHeldConfirmationLockCommit':'c533227','PreRefinedBoundaryDiagnosticCommit':'71b2155',
    'ObjectiveOrPhysicsRetuned':False,'PhysicsWorkflowChanged':False,
    'ReporterSourceSha256':self_hash}
r['Cost']['ContactsAreLowerBound']=True
r['Cost']['ContactCountIncompleteWorlds']=1
r['Cost']['AbortedWorldCpuSeconds']=next(a['CpuSeconds'] for a in r['Reservations'] if a.get('AbortedBeforeIntervention'))
r['Cost']['ReporterCpuSecondsExcludedFromEpisodeForecastLimit']=time.process_time()-started

text=f'''# #142 — Racing Objective & Artificial Penalty Ablation

**PARTIAL: pojedyncze preferencje szkodzą, ale sam zachłanny postęp nie wystarcza.**
Baza #141 `266df048536c256ced410a1045a5924899153237`. Oryginalne silnik, opony,
Box2D, masa/I, mu, tor i aktuator bez zmian. Reakcja0,24s, decyzje0,2s,
predyktor H09 i legalny zestaw akcji identyczne. Nowe polityki OFF; bez treningu.

## Co ograniczało zawodnika

Audyt38 obserwacji #136/#137:28 unikalnych stanów dwóch rodzin; zero nowych
prognoz/światów offline. Nie są to28 niezależne przypadki.
Usunięcie yaw zmienia26/28 wyborów (głównie skręt), kierunku16/28, preferencji
gazu4/28 (gaz1 w2 stanach). Usunięcie osobno kosztów skrętu, geometrii,
prześwitu i Unsafe-score nie zmienia wyboru w tej próbce. Nie dowodzi to ich
globalnej zbędności. Oryginalne A wybiera gaz0/częściowy/1:4/24/0; B/C:0/0/28.

Yaw i kierunek są preferencjami wobec przyszłej stycznej/krzywizny, nie prawami
fizyki ani celem β=0. Kara gaz1 wynosi1,307 punktu za odejście od preferowanych
31,82%. Kara zmiany skrętu działa obok rzeczywistego aktuatora. Obwiednia
`sqrt(0.8*capacity/curvature)` ma20% heurystycznej rezerwy; nie jest prawem opony.
Track Awareness jest OFF w A; po włączeniu zastępuje koszt geometrii, jego delta
może być ujemna. Enabled-vs-removed zmienia1/18 pełnych zapisanych pul.
Równania, wagi #16015 i rankingi są w `results.json`.

## Ciągłe natywne A/B/C

A: oryginał. B: maksymalny postęp ze starymi klasami. C: maksymalny postęp,
odrzucający naruszenie obrysu, β45°, nieprawidłowy stan i ruch wstecz względem
toru, bez priorytetu Safe nad Uncertain. Każdy pełny zakres obejmuje badany łuk,
62m, cały kolejny łuk i dalsze62m; D1/H1 również trzeci łuk. Bez resetów.

| Rodzina | A: pełny czas | B: stop natywny | C: brak dopuszczalnego H09 |
|---|---:|---|---:|
|D0|18,869s|2,350s — brak certyfikacji obrysu|1,610s|
|D1|28,771s|5,770s — reverse / cofnięcie postępu|1,610s|
|H0|18,712s|2,040s — β45,408°|1,210s|
|H1|27,555s|2,240s — brak certyfikacji obrysu|1,210s|

Legalne ukończenia: **A4/4, B0/4, C0/4**. B używa znacznie więcej gazu1,
ale nie dociera do badanych wyjść/prostych. Brakujące prędkości i pełne czasy
pozostają null. Dla A prędkość przy+40m wynosi75,95/76,36/78,39/75,81km/h.
Wszystkie prędkości głównego porównania mierzone są w tych samych przekrojach,
z jawną interpolacją skalarnych metryk między zaakceptowanymi próbkami0,01s.

**Rozróżnienie bezpieczeństwa:** Uncertain to margines0,2m/0,1m, nie DNF.
C rzeczywiście wybiera szybciej/mocniej zamiast Safe po0,8–1,2s, gdy prognoza
nie przekracza jeszcze twardych granic; następnie wyczerpuje skończony zbiór
dopuszczalnych prognoz. Jego cztery zatrzymania nie są czterema rzeczywistymi
wypadnięciami. D0/H1 B mają nadal dodatnie chwilowe prześwity; ujemna jest
konserwatywna dolna granica obrysu w ruchu. To brak certyfikacji, nie potwierdzone
przekroczenie w próbce. Dwie dodatkowe diagnostyki z istniejącym dokładniejszym
testem obrysu, przy identycznym prefiksie decyzji B, potwierdzają jednak przekroczenie
po kolejnych 0,02 s: D0 po 2,370 s (−0,060 m), H1 po 2,260 s (−0,030 m).
Nie są niezależnymi rodzinami. β45° jest nieskalibrowaną obwiednią eksperymentalną,
nie dowodem fizycznego upadku. Detektory natywne zachowano.

## Izolowane decyzje i przyczyna niepowodzeń

Po identycznym prefiksie, z identycznym stanem/pending, zmieniono jedną decyzję,
następnie przywrócono autonomiczne A:

- D1, gaz po8s, bez preferencji gazu częściowego: **28,771→28,191s**, poprawa
0,581s, droga krótsza o5,460m; +40m po badanym drugim łuku **76,36→77,87km/h**.
- D0, kierunek po10,8s: **18,869→18,841s**, poprawa0,027s, droga dłuższa o0,732m.

Obie próby legalne; nie dowodzą korzyści z globalnego usunięcia danej kary.
Przy B wszystkie4 pełne próby zawodzą. W D0 brak dopuszczalnego held-H09
występuje od1,6s, w D1 od5,0s. Przełączenia B→A po1,2/1,6s oraz4,6/5,0s nie
odzyskują legalnej kontynuacji; A od początku kończy. To dowód znaczenia wcześniejszej
strategii i ograniczenia H09, nie globalny certyfikat nieodwracalności ani dowód
konieczności konkretnych wag yaw. Źródła i ostatnie decyzje przy stopach zachowano.

## Rekomendacja i koszt

Najpierw sprawdzić globalne usunięcie preferencji częściowego gazu w osobnym
autonomicznym A/B. Zachować ograniczenia i ocenę dalszej legalnej kontynuacji.
Nie traktować yaw/kierunku, rezerwy80% i marginesów jako fizyki: nie wykazano
konieczności wszystkich tych kar. Sam krótkoterminowy postęp nie wystarcza.

**24/24 światy, 152,8125 s CPU epizodów/prognoz +3,25 s odczytów =156,0625/450 s;
26682 prognozy.** Wliczono przerwany świat8,516s: błąd porównania zegara7×10⁻¹⁵s
skorygowano po dowodzie identyczności stanu/pending, bez zmiany sterownika.
Kontakty przerwanej próby są niepełne; suma jest dolną granicą.
Protokół `a3341b9`, korekta `01ec8ba`, lock H0/H1 `c533227`, dodatkowe diagnostyki
`71b2155`; bez strojenia. Łącznie 10 stopów zabezpieczeń
(w tym 4 późne przełączenia i 2 powtórzenia diagnostyczne), 4 zatrzymania C i 1
przerwanie programistyczne; 2 potwierdzone przekroczenia obrysu w dodatkowych
diagnostykach. Bez progu CPU pojedynczej decyzji.

Walidacja lokalna i standardowy Windows CI: `results.json` oraz PR.
Workflow i produkcja bez zmian; ciężkie audyty historyczne pomija polityka Draft.
Tylko te dwa raporty, bez dodatkowych materiałów prezentacyjnych.
'''
# Keep numerical units and prose readable without changing field labels in JSON.
for a,b in {
 'Reakcja0,24s, decyzje0,2s':'Reakcja 0,24 s, decyzje 0,2 s','Audyt38':'Audyt 38','obejmuje28':'obejmuje 28','nie28':'nie 28',
 'zmienia26/28':'zmienia 26/28','kierunku16/28':'kierunku 16/28','gazu4/28':'gazu 4/28','w2 stanach':'w 2 stanach',
 ':4/24/0':': 4/24/0',':0/0/28':': 0/0/28','wynosi1,307':'wynosi 1,307','ma20%':'ma 20%','zmienia1/18':'zmienia 1/18',
 '62m':'62 m','dalsze62':'dalsze 62','A4/4, B0/4, C0/4':'A 4/4, B 0/4, C 0/4','przy+40m':'przy +40 m',
 'wynosi75,95/76,36/78,39/75,81km/h':'wynosi 75,95/76,36/78,39/75,81 km/h','próbkami0,01s':'próbkami 0,01 s',
 'margines0,2m/0,1m':'margines 0,2 m/0,1 m','po0,8–1,2s':'po 0,8–1,2 s','po8s':'po 8 s','poprawa0,027s':'poprawa 0,027 s',
 '0,581s':'0,581 s','o5,460m':'o 5,460 m','77,87km/h':'77,87 km/h','po10,8s':'po 10,8 s','o0,732m':'o 0,732 m',
 'wszystkie4':'wszystkie 4','od1,6s':'od 1,6 s','od5,0s':'od 5,0 s','po1,2/1,6s oraz4,6/5,0s':'po 1,2/1,6 s oraz 4,6/5,0 s',
 'rezerwy80%':'rezerwy 80%','świata,146,8125s':'świata, 146,8125 s','+3,25s':'+3,25 s','=150,0625/450s':'=150,0625/450 s',
 'świat8,516s':'świat 8,516 s','o7×':'o 7×','⁻¹⁵s':'⁻¹⁵ s','Łącznie8':'Łącznie 8','tym4':'tym 4','4 zatrzymania C i1':'4 zatrzymania C i 1'
}.items():text=text.replace(a,b)
text=text.replace('to28','to 28').replace('zegara7','zegara 7')
text=re.sub(r'(?<=\d)s\b',' s',text)
text=re.sub(r'(?<=\d)m\b',' m',text)
(P.parent/'README.md').write_text(text,encoding='utf-8',newline='\n')

def compact(value,level=0,key=''):
    indent='  '*level
    if isinstance(value,dict):
        return '{\n'+',\n'.join('  '*(level+1)+json.dumps(k,ensure_ascii=False)+': '+compact(v,level+1,k) for k,v in value.items())+'\n'+indent+'}' if value else '{}'
    if isinstance(value,list):
        if not value:return '[]'
        if key in {'Candidates','Decisions','LastDecisions'}:
            return '[\n'+',\n'.join('  '*(level+1)+json.dumps(v,ensure_ascii=False,separators=(',',':'),allow_nan=False) for v in value)+'\n'+indent+']'
        return '[\n'+',\n'.join('  '*(level+1)+compact(v,level+1,key) for v in value)+'\n'+indent+']'
    return json.dumps(value,ensure_ascii=False,allow_nan=False)
P.write_text(compact(r)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'MainCompletion':r['FinalSummary']['MainCompletion'],'Branches':[(b['Removed'],b['TimeGainSeconds']) for b in branches],
    'NativeGuardStops':len(native_failures),'Worlds':r['Cost']['NewNativeWorlds'],'CpuSeconds':r['Cost']['CpuSeconds'],
    'ReadmeWords':len(text.split()),'JsonBytes':P.stat().st_size}))
