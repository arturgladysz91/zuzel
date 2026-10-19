"""Read-only #94 evidence checks and tables. No simulation or parameter search."""
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

root = Path(__file__).resolve().parents[2]
directory = Path(sys.argv[1]) if len(sys.argv) > 1 else root / 'docs/experiments/close-racing-following-gap'
def read(name): return json.loads((directory / (name + '.json')).read_text(encoding='utf-8-sig'))
def f(x, n=3): return '—' if x is None else f'{x:.{n}f}'
def table(headers, rows):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |', '|' + '---|' * len(headers)] +
                     ['| ' + ' | '.join(map(str, row)) + ' |' for row in rows])
def commands(run): return [d for d in run['Result']['Decisions'] if d['ActualActivation'] is not None]
def attacks(run): return [d for d in commands(run) if d['Channels'] == 1 and d['Action'] in (4, 5)]
def defenses(run): return [d for d in commands(run) if d['Channels'] == 1 and d['Intent'] == 3]
def sides(run): return [d for d in commands(run) if d['Channels'] == 1 and d['Intent'] in (5, 6, 7)]
def gas_times(run, rider):
    end = run['Result']['FinishTimes'].get(str(rider), run['Result']['TimeSeconds'])
    times = [0., 0., 0.]  # zero / partial / full; exact activation boundaries
    at, drive = 0., 1.
    for d in sorted((d for d in commands(run) if d['RiderId'] == rider and d['Channels'] == 2), key=lambda d: d['ActualActivation']):
        t = min(end, d['ActualActivation'])
        times[0 if drive == 0 else 2 if drive == 1 else 1] += max(0, t-at)
        at, drive = t, d['Drive']
        if at >= end: break
    times[0 if drive == 0 else 2 if drive == 1 else 1] += end-at
    return times
def label(run, suffix): return f"[{run['Name']}/{suffix}](close-racing-following-gap/{run['Name']}-{suffix}.json)"

names = ['A1-rotation-0', 'A2-rotation-1', 'close-rotation-0', 'close-rotation-1', 'rolling-straight', 'rolling-bend']
short = ['equal-close', 'catch-slower', 'leader-bend-slowdown', 'closing-sign-change', 'follow-attack', 'parallel', 'three-shared-space']
heats = [(read(n+'-'+mode), mode) for n in names for mode in ('baseline','candidate')]
variants = [(read(n+'-'+suffix), v) for n in short for v, suffix in [(1,'baseline'),(2,'variant-2'),(3,'variant-3')]]
seal = read('baseline-seal')
for entry in seal['Files']:
    assert hashlib.sha256((directory/entry['File']).read_bytes()).hexdigest().upper() == entry['Sha256'], entry['File']
checks = {'BaselineSealExact': True, 'FullTrials': len(heats), 'ShortTrials': len(variants), 'Runs': []}
for run, mode in heats:
    r = run['Result']
    assert run['Status']=='Completed' and len(r['FinishTimes'])==4 and r['Work']['Episodes']==0
    assert len(run['Inputs'])==4 and all(i['TrafficEnabled'] for i in run['Inputs'])
    assert not run['Anomalies'] and not run['Consequences']
    if mode=='baseline' and run['Name'] in names[:2]:
        old=json.loads((directory.parent/'proportional-throttle-control'/(run['Name']+'-ON.json')).read_text(encoding='utf-8-sig'))
        assert r['Fingerprint']==old['Result']['Fingerprint'] and r['Decisions']==old['Result']['Decisions']
    for rider in run['Riders']:
        rid=rider['Measurement']['RiderId']; end=r['FinishTimes'][str(rid)]
        assert len(rider['Measurement']['LapSeconds'])==4
        assert math.isclose(sum(rider['Measurement']['LapSeconds']),end,abs_tol=1e-8)
        assert math.isclose(sum(gas_times(run,rid)),end,abs_tol=1e-8)
        assert not any(d['RiderId']==rid and d['ActualActivation']>end+1e-9 for d in commands(run))
    checks['Runs'].append({'Name':run['Name'],'Mode':mode,'Fingerprint':r['Fingerprint'],
                          'ExactGasSeconds':{str(i):gas_times(run,i) for i in range(1,5)}})
for run, _ in heats+variants:
    assert all(0<=d['Drive']<=1 for d in run['Result']['Decisions'])
    assert all(d['ActualActivation']+1e-9>=d['RequestedActivation'] for d in commands(run))
repeats=read('repeats'); assert all(all(r[k] for k in ('ExactHash','ExactDecisions','ExactContacts')) for r in repeats)
perf=read('performance')
for name in {x['Name'] for x in perf}:
    for mode in (0,1,3): assert len({x['Fingerprint'] for x in perf if x['Name']==name and x['Mode']==mode})==1
# The unchanged eight-second #93 and OFF benchmark windows also match their historical accepted states.
for mode, suffix in [(0,'OFF'),(1,'ON')]:
    old=json.loads((directory.parent/'proportional-throttle-control'/('A-blocked-follow-'+suffix+'.json')).read_text(encoding='utf-8-sig'))
    assert all(x['Fingerprint']==old['Result']['Fingerprint'] for x in perf if x['Name']=='A-blocked-follow' and x['Mode']==mode)
checks['HistoricalEightSecondHashesExact']=True
(directory/'derived-checks.json').write_text(json.dumps(checks,indent=2)+'\n',encoding='utf-8')

parts=['''# Close Racing Validation and Adaptive Following Gap (#94)

**PROVISIONAL — B: CZĘŚCIOWO.** Cztery motocykle w Rolling Pack naprawdę podążają wspólnym korytarzem, używają częściowego gazu i później fizycznie zmieniają linię. Wariant zależny od zbliżania poprawia wybrane krótkie przypadki, lecz **nie zmniejsza rzeczywistej mediany prześwitu w pełnym Rolling Pack**: 3.778 → 3.804 m. Żaden pełny przejazd nie ma podążania z prześwitem wzdłużnym ≤3 m. Nie ma podstaw do oceny A ani do włączenia flagi domyślnie.

Baza: Draft #93, `experiment/proportional-traffic-throttle`, `cb980e0e59b5ad6a1c3b28ac35db778d1a1fae94`. Jeden nowy Draft, bez merge. `EnableClosureAdaptiveFollowingGap=false` domyślnie, wymaga `EnableProportionalTrafficThrottle=true`. Przy nowej fladze OFF zachowane są dokładne wyniki #93 ON; przy obu OFF wcześniejsze wyniki #92. Produkcyjny `HeatSimulator`, `src/`, `RiderController.Forces`, ContactAdapter/Box2D, napęd, geometria, grip, reakcja, umiejętności i scoring #91 nie zmieniają się.

## Etap A: najpierw pomiar #93

[Zamrożona konfiguracja](close-racing-following-gap/frozen-configuration.json), [pieczęć SHA-256 bazowych plików](close-racing-following-gap/baseline-seal.json), [diagnoza przed zmianą wzoru](close-racing-following-gap/stage-A.md), [wybór przed pełnymi próbami kandydata](close-racing-following-gap/selection.md). Najpierw wykonano poprawny rolling-straight, potem resztę bazowych prób. Wzór regulatora był wtedy identyczny z #93. Dodane liczniki nie zmieniają granic kroków. Sprawdzenie odtwarza dokładne hashe i komendy A1/0 i A2/1 oraz historyczne hashe 8 s OFF/ON.

A1 rozdziela aktualne korytarze: brak podążania, początkowe 2.2 m bocznego prześwitu fizycznych części. A2/1 ma 26.611 s podążania R3, ale p10/mediana/p90 prześwitu 6.203/7.549/9.551 m. To daleki pościg, przy którym regulator może prawidłowo pozostawiać pełny gaz. Nowe profile o identycznych legacy Speed/SlideControl nadal głównie jadą odrębnymi liniami; sama równość osiągów nie tworzy ciasnej grupy po starcie.

Rolling-straight bazowy pokazuje 49.560 s podążania R1, 12.300 s częściowego gazu i późniejszy rzeczywisty atak. Trzy aktywowane pełne zamknięcia (9.936, 17.336, 24.736 s), łącznie 1.200 s, mają zwykły powód `Following`, nie Emergency. Przy danej fazie łuku istniejąca funkcja sił może nie dawać potrzebnego spowolnienia dla żadnego dodatniego gazu (neutral carry); Drive=0 odsłania dotychczasowy opór. Nie dodano hamulca. Kandydat nie ma w tym przejeździe żadnego pełnego zamknięcia. Mała para równych prędkości zaczyna z około 1.1 m prześwitu, a baza rozsuwa ją do mediany 2.110 m — stąd ograniczone porównanie preferencji.

## A. Dwanaście pełnych prób

Sześć zamrożonych konfiguracji × baza/kandydat = **12 głównych przejazdów czterech motocykli przez cztery obiegi**. Dodatkowe uruchomienia regresji i krótkie pomiary wydajności nie są nowymi konfiguracjami. Wszystkie normalne AI, bez komend autorskich. Motoarena, referencyjny obieg 318.77874994277954 m, krok .01 s, 4 substeps, AI .1 s, Tactical=true, SideBySide=true, Quality=false, ProportionalThrottle=true. Nieruchoma jednolita nawierzchnia: Grip=1, Ruts=0, Moisture=.5; EffectiveGrip=.9175.

Standing Start używa istniejących pól A/B/C/D (offset .5/3.5/6.5/9.5 m). Rotacja 1: R1=B, R2=C, R3=D, R4=A. A1/A2 bez zmian z #92/#93. Nowa grupa close ma legacy Start/Speed/SlideControl=55/55/65, masę 70 kg i neutralny setup. Kanoniczne umiejętności różnią się, zamrożone przed pierwszym uruchomieniem:
''']
profiles=read('frozen-configuration')['Profiles']
parts.append(table(['Zawodnik','Reaction','Start','Technique','TrackReading','Attack','Defense','PairRiding','Strength','Combativeness'],
    [[p['Id']]+[p['Canonical']['Abilities'][k] for k in ('Reaction','Start','Technique','TrackReading','Attack','Defense','PairRiding','Strength')]+[p['Canonical']['InteractionStyle']['Combativeness']] for p in profiles]))
parts.append('''
Rolling Pack jest osobnym fragmentem trwającego wyścigu. Na prostej referencje R1/R2/R3/R4 to 133.389/141.389/140.389/140.389 m; w łuku 45/53/52/52 m. Offsety 4/4/2.6/5.4 m; prędkości 19/18/19.5/18.5 m/s. Kierunki prędkości i początkowe orientacje są lokalnie styczne, bez ręcznych zmian po starcie. Weryfikacja wszystkich sześciu par pełnych kapsuł i granicy toru następuje przed krokiem. Akceptowana różnica odczytanej orientacji Box2D od stycznej ≤.005 rad uwzględnia istniejącą aproksymację obrotu (~.0014 rad w tej krzywiźnie).

Runner ma opcjonalne `lapsFromInitialPosition=false`: tylko Rolling włącza metę `initialReference + 4*lapMeters` dla każdego zawodnika; pomiar okrążeń stosuje ten sam początek. Nie zaliczamy skróconego pierwszego obiegu jako pełnego. Czasy/order Rolling porównują cztery pełne obiegi od indywidualnych pozycji, nie wspólną linię mety regulaminowego biegu od taśmy. Ciała ukończonych nadal fizycznie jadą do ostatniej mety; ich własna telemetria i komendy kończą się na ich mecie. Limit 90 s, przerwanie na rzeczywisty Crash.

Tabela: czasy w kolejności R1/R2/R3/R4; częściowy gaz i podążanie to suma rider-sekund. A/D/S = aktywowane ataki/obrony/manewry SideBySide, wyliczone wyłącznie z kolejki **Line**. Atak o Action=AttackInside/Outside może równocześnie być manewrem SideBySide. Gaz z zachowaną intencją nie jest kolejnym atakiem.
''')
rows=[]
for run,mode in heats:
    r=run['Result'];rows.append([label(run,mode), 'Rolling' if run['Rolling'] else 'Standing',
        '>'.join('R'+str(x['RiderId']) for x in r['Classification']),
        '/'.join(f(r['FinishTimes'][str(i)]) for i in range(1,5)),
        f(sum(gas_times(run,i)[1] for i in range(1,5))),f(sum(x['FollowingSeconds'] for x in run['Riders'])),
        f'{len(attacks(run))}/{len(defenses(run))}/{len(sides(run))}',len(run['Passes']),
        f"{r['Work']['Episodes']}/0",r['Work']['SolverCalls']])
parts.append(table(['Próba','Typ','Kolejność','4 obiegi [s], R1–R4','Częściowy [s]','Podążanie [s]','A/D/S','Minięcia','Kontakt/Crash','Solver'],rows))
parts.append('''
Wszystkie 12 ukończone; zero kontaktów, konsekwencji i Crash, zero nowych anomalii granicy toru, zero zbliżeń <.3 m z dodatnim ruchem ku rywalowi. To wynik tych pozycji i prędkości, nie gwarancja bezpieczeństwa małego dystansu. Historyczny nieunikniony Crash pozostaje testem regresyjnym. Potwierdzenie minięcia wymaga ustanowionego lidera pełnych motocykli przez 1 s/10 m, realnego stanu obok siebie, potem całego motocykla z .15 m prześwitu utrzymanego 1 s/10 m; początkowe rozstawienie nie tworzy minięcia. Wszystkie potwierdzenia są po pierwszej sekundzie; pełne zdarzenia w JSON.

Szczegóły wszystkich zawodników. Pełny/częściowy/zero to czas rzeczywiście aktywowanego kanału gazu do własnej mety, wyliczony z jego granic aktywacji. Pomiar podążania i kwantyli waży odstępy między zaakceptowanymi stanami; lewy stan może poprzedzać potwierdzenie aktuatora o jeden mikro-krok (≤.01 s). Starsze pola `Measurement.Attacks/Defenses/SideManeuvers` w surowym kolektorze #92 zliczają również odziedziczoną intencję komend gazu — raport używa zamiast nich odrębnych aktywacji Line.
''')
rows=[]
for run,mode in heats:
    for x in run['Riders']:
        m=x['Measurement'];i=m['RiderId'];zero,partial,full=gas_times(run,i)
        rows.append([run['Name']+'/'+mode,'R'+str(i),'/'.join(f(t) for t in m['LapSeconds']),
            '/'.join(f(t) for t in (full,partial,zero)),f(x['FollowingSeconds']),f(x['PartialFollowingSeconds']),
            '/'.join(str(sum(d['RiderId']==i for d in ds)) for ds in (attacks(run),defenses(run),sides(run))),
            sum(p['RiderId']==i for p in run['Passes']),f(x['MinimumSeparationMeters']),f(x['NearCollisionSeconds'])])
parts.append(table(['Próba','R','Okrążenia 1/2/3/4 [s]','Pełny/częściowy/zero [s]','Podążanie [s]','Podążanie częściowo [s]','A/D/S','Minięcia','Min fiz. [m]','Zagrożenie [s]'],rows))
parts.append('''
## B. Cztery różne odległości i trzy warianty

Wzdłużny prześwit = tył pełnego motocykla lidera minus przód goniącego na wspólnej lokalnej osi. Boczny prześwit = odstęp projekcji pełnych kapsuł na normalnej do osi; ujemny oznacza wspólne pasmo boczne, nie przenikanie. Odległość mechaniczna to minimum odległości rzeczywistych kapsuł chassis/kierownicy przez `MechanicalSeparation`. Różnica postępu to odległość na wewnętrznej referencji toru, a nie odległość zderzeniowa. W łuku różnią się wszystkie cztery miary. Sama bliskość postępu jadących równolegle nie wymaga zamknięcia gazu.

**Podążanie**: najbliższy rywal z `TrafficThrottle.Front` (przed nami, ≤30 m, zgodne styczne, korytarz bieżący/przewidywany), ponadto aktualny prześwit boczny ≤.15 m i wzdłużny 0..10 m. Raportuje rzeczywisty wspólny korytarz, nie sam plan przyszłego przecięcia. **Ciasno**: ≤3 m wzdłużnego prześwitu; **stabilnie**: |closing|≤.5 m/s. Kwantyle p10/p50/p90 i zakres min/max ważone są faktycznym czasem zaakceptowanych interwałów, nie liczbą kroków solvera. Ryzyko: odległość mechaniczna <.3 m i dodatni ruch ku innemu motocyklowi; to diagnostyczna flaga bliskości, nie klasyfikacja kontaktu.

1. #93: `0.6 + 0.1*ownSpeed`, czyli 2.6 m przy 20 m/s.
2. Diagnostyczny: `0.4 + 0.04*ownSpeed`, czyli 1.2 m przy 20 m/s. Wyłącznie wewnętrzny parametr audytu; brak drugiego publicznego modelu AI.
3. Kandydat: `0.6 + c*reaction + c²/(2*max(0.1,-aZero))`, gdzie `c=max(0,closing)` i `aZero` jest już obliczonym przyspieszeniem z niezmienionych Forces dla Drive=0. Przy równym tempie preferencja .6 m; dla c=1, reaction=.24 i aZero=-2 daje 1.09 m, dla c=3 daje 3.57 m. .6 m jest preferencją, nie uniwersalnym bezpiecznym minimum.

Awaryjne kryterium pozostaje dokładnie #93: dodatnie closing>.25 i `gapAfterReaction <= .15 + c²/(2*availableCoastDeceleration)`. Osobno wzór preferencji steruje zwykłym regulatorem. `gapAfterReaction` już odejmuje `c*reaction`; wariant 3 dodaje reakcję również do preferencji, więc pozostawia konserwatywną dodatkową rezerwę, nie jest ścisłym minimum zatrzymania. Nie ma prognozy tajnej komendy rywala, skrócenia reakcji ani dodatkowego hamowania. Ten zapas nie zapewnia reakcji na nagłe nieobserwowane zwolnienie lidera.

Te same 7 krótkich sytuacji, po 3 wzory (21 uruchomień). Profilów ani współczynników nie dostrajano po wynikach. Tabela R1: rzeczywisty rozkład podczas podążania, nie wartości docelowe. Fizyczne minimum jest z całego przypadku, osobno od kwantyli podążania.
''')
rows=[]
for run,v in variants:
    x=run['Riders'][0];g=x['Longitudinal'];r=run['Result'];rows.append([run['Name'],v,f(x['FollowingSeconds']),
        '/'.join(f(g[k]) for k in ('Minimum','P10','Median','P90','Maximum')),f(x['CloseSeconds']),
        f(min(x['MinimumSeparationMeters'] for x in run['Riders'])),
        f(sum(gas_times(run,i['Id'])[1] for i in run['Inputs'])),r['Work']['SolverCalls'],r['Work']['ActivatedThrottleCommands'],len(attacks(run))])
parts.append(table(['Przypadek','W','Podążanie [s]','Wzdłużny min/p10/p50/p90/max [m]','≤3 m [s]','Min fiz. [m]','Częściowy [s]','Solver','Gaz komendy','Ataki'],rows))
parts.append('''
Wszystkie 21 bez kontaktów i zarejestrowanego zagrożenia. Wariant 3 wybrano po skróceniu dystansu w equal-close i three-shared-space, przy zachowanym ataku i bez wzrostu solvera w tym drugim. Wariant 2 w trzech motocyklach daje 603→734 wywołania i min fizyczne 1.685→.574 m; wariant 3: 603 i 1.330 m. Nie wszędzie 3 zbliża: follow-attack ma medianę 5.709 wobec 5.208 m bazy. Sytuacja closing-sign-change rzeczywiście zmienia znak zbliżania; parallel nie ma ograniczeń napędu; leader-bend-slowdown zachowuje opóźnienie i odpowiedź na geometrię. Żaden wynik nie został odrzucony jako „nieładny”.

Rozkłady pełnych przejazdów z podążaniem (pozostałe nie mają takiej populacji). Osobne mediany boczne/fizyczne/postępu są kwantylami swoich zmiennych, nie czterema jednoczesnymi wartościami w tej samej chwili.
''')
rows=[]
for run,mode in heats:
    for x in run['Riders']:
        if not x['FollowingSeconds']: continue
        g=x['Longitudinal'];rows.append([run['Name']+'/'+mode,'R'+str(x['Measurement']['RiderId']),f(x['FollowingSeconds']),
            '/'.join(f(g[k]) for k in ('Minimum','P10','Median','P90','Maximum')),
            f(x['Lateral']['Median']),f(x['Mechanical']['Median']),f(x['ReferenceProgress']['Median']),f(x['StableFollowingSeconds']),f(x['CloseSeconds'])])
parts.append(table(['Próba','R','Podążanie [s]','Wzdłużny min/p10/p50/p90/max [m]','Boczny p50','Fizyczny p50','Postęp p50','Stabilnie [s]','≤3 m [s]'],rows))
parts.append('''
**Wynik negatywny pełnych prób:** mniejsza preferencja nie zmusza AI do jechania w docelowym odstępie. Obecna geometria łuków, neutral carry, pełny gaz przy równych możliwościach, wyjścia do SideBySide i zmiany linii dominują nad preferencją. W rolling-straight mediana nie maleje, zero sekund poniżej 3 m; w pozostałych pięciu pełnych konfiguracjach kandydat zachowuje dokładny hash bazy. Nie rozbudowujemy AI ani nie zmieniamy profili, żeby uzyskać ocenę A.

## C. Rzeczywiste cztery motocykle: gaz → atak → fizyczna zmiana linii

Przykład **rolling-straight/baseline**, normalna AI wszystkich czterech. R1 podąża za R2 przez wspólny korytarz; ma wcześniej i w tym fragmencie częściowy napęd. W 47.000 s obserwuje możliwość AttackInside, żądanie 47.236 s, aktywacja 47.236 s — pełne .236 s reakcji. Target 2.612 m; fizyczny offset po aktywacji zmienia się stopniowo z około 3.999 do 2.917 m, bez teleportacji. To wykonany ruch ofensywny; nie tylko intencja.

Tabela pokazuje zaakceptowane stany; próbka dokładnie na granicy aktywacji przedstawia stan przed kolejnym krokiem, polecenie jest osobno potwierdzone w Decisions. Prędkości m/s, odstępy/offsety m. „Zamiar” wynika z aktywowanej kolejki Line.
''')
run=read('rolling-straight-baseline');samples=[s for s in run['Samples'] if s['RiderId']==1]
targets=[46.5,47.,47.236,48.005,49.,49.5]
rows=[]
for t in targets:
    s=min(samples,key=lambda s:abs(s['TimeSeconds']-t));g=s['Gap'];rows.append([f(s['TimeSeconds']),f(s['Drive']),f(s['OwnSpeed']),f(s['OpponentSpeed']),
        f(g['GapMeters']),f(g['LateralGapMeters']),f(s['MechanicalMeters']),f(s['OffsetMeters']),f(s['TargetMeters']),
        'podążanie' if s['TimeSeconds']<47 else 'AttackInside obserwacja' if s['TimeSeconds']<47.236 else 'AttackInside granica' if s['TimeSeconds']<47.24 else 'AttackInside ruch'])
parts.append(table(['Czas','Gaz','Prędkość R1','Prędkość R2','Wzdłużny','Boczny','Fizyczny','Offset R1','Target','Zamiar'],rows))
parts.append('''
**Wynik tej próby:** R1 nie potwierdza minięcia R2; R1 kończy 59.448 s, R2 59.555 s, lecz sama kolejność czasów czterech obiegów od różnych początków nie jest dowodem minięcia. Jedyne potwierdzone minięcie tego przejazdu to R1/R4 w 45.020 s, przed powyższym atakiem, więc nie przypisujemy go atakowi na R2.

Wariant 3 również ma częściowe podążanie (1.200 s), potem ataki `ContestInside` na R4: obserwacje 25.600/33.300, aktywacje 25.836/33.536 s. Fizyczny offset R1 3.994→3.100 m po pierwszym i 2.987→2.058 m po drugim. Potwierdzone R1/R4 następuje w 44.330 s; nie mylimy jego przeciwnika z R2, którego nadal wyznacza regulator Front. To zachowana możliwość walki, lecz prześwit podążania pozostaje około 3.8 m. Brakuje dowodu pełnej czteroosobowej jazdy przy mniejszych prześwitach z krótkich prób.

## D. Wydajność i pochodzenie podziałów

Poniżej dwie identyczne długości okna 8 s; OFF = stara reaktywna taktyka bez proporcjonalnego gazu, #93 = proporcjonalny stary odstęp, nowy = wariant 3. Bare WorldRunner bez obserwatorów; jedna rozgrzewka, trzy mierzone powtórzenia każdej konfiguracji, .NET 8 Release, `DOTNET_TieredCompilation=0`, ta sama maszyna. Czas/AI to mediany; alokacje na głównym wątku obejmują konstrukcję/Run/Dispose. Hashe trzech powtórzeń każdej konfiguracji identyczne. Nie są to trzy różne zestawy profili.
''')
rows=[]
for name in sorted({x['Name'] for x in perf}):
    for mode in (0,1,3):
        xs=[x for x in perf if x['Name']==name and x['Mode']==mode];x=xs[0];w=x['Work']
        rows.append([name,{0:'OFF',1:'#93',3:'nowy'}[mode],f(x['TimeSeconds']),w['SolverCalls'],f(w['SolverCalls']/x['TimeSeconds']),
            w['SolverSubsteps'],f(statistics.median(x['ElapsedMilliseconds'] for x in xs)),
            f(statistics.median(x['Work']['TacticalElapsedMilliseconds'] for x in xs)),f(statistics.median(x['AllocatedBytes'] for x in xs)/1048576),
            f(w['ProximitySeconds']),w['ActivatedThrottleCommands']])
parts.append(table(['Okno','Tryb','Czas sim [s]','Solver','Solver/s','Substeps','Wall [ms]','AI [ms]','MiB','Proximity [s]','Gaz aktywacje'],rows))
rows=[]
for name in sorted({x['Name'] for x in perf}):
    for mode in (0,1,3):
        x=next(x for x in perf if x['Name']==name and x['Mode']==mode);w=x['Work'];nom=w['PhysicsUpdates']+w['ProximityRequestedExtraCalls']
        rows.append([name,{0:'OFF',1:'#93',3:'nowy'}[mode],w['PhysicsUpdates'],w['ProximityRequestedExtraCalls'],
            w['SolverCalls']-nom,w['CurvatureLimitedCalls'],w['ControlDeadlineLimitedCalls'],w['ThrottleDeadlineLimitedCalls'],w['ThrottleForceEvaluations']])
parts.append(table(['Okno','Tryb','Bazowe ticki','Proximity extra nominal','Reszta calls','Curvature clip','Deadline clip','w tym gaz','Force próby gazu'],rows))
parts.append('''
`ProximitySeconds` to czas z istniejącym `NearbySubdivisions>1`, zależnym od odległości, prędkości względnej i obrotu kapsuł; nie jest czasem dowolnej jazdy w pobliżu. Nominalny budżet = ticki + suma(subdivisions−1). Reszta to dodatkowe wywołania po istniejących granicach krzywizny/reakcji. Liczniki clip opisują wywołania ograniczone na końcu przez dany warunek, nie addytywną liczbę nowych kroków: krzywizna może wygrać z wcześniejszym deadline, a ułamki zmiennoprzecinkowe przy końcu ticka mogą ograniczyć czas bez nowego kroku. Gaz to podzbiór deadline; jego aktywacja nie zawsze tworzy nową granicę. Cztery substeps każdego wywołania pozostają stałe.

W historycznym A wzrost OFF→#93 o 239 wywołań (899→1138) odpowiada przede wszystkim +240 nominalnym podziałom proximity (91→331); krzywizna 8→7, tylko 2 clip deadline gazu. Sam regulator robi więcej pracy AI, ale nie nakazuje +26.6% kroków fizyki. Kandydat ma 943 wywołania i tylko 136 extra proximity. Trajektorie oraz czas pozostawania blisko różnią się, więc spadek wall nie jest optymalizacją solvera. W rolling 8 s geometria jest podobna (#93/nowy po 157 extra proximity), liczba deadline gazu maleje 4→2, solver 973→971. Brak materialnego narzutu nowej formuły w tym oknie; nie gwarantuje to kosztu w innych tłokach.

Pełne przebiegi poniżej są pojedynczymi **obserwowanymi** pomiarami (z kosztowną diagnostyką geometrii/sił i JIT), a nie porównaniem szybkości algorytmu. Różnią się długością świata i trajektorią. Bare pomiar powyżej oddziela koszt zbierania raportu.
''')
rows=[]
for run,mode in heats:
    r=run['Result'];w=r['Work'];rows.append([run['Name']+'/'+mode,f(r['TimeSeconds']),w['SolverCalls'],f(w['SolverCalls']/r['TimeSeconds']),w['SolverSubsteps'],
        f(run['ElapsedMilliseconds']),f(w['TacticalElapsedMilliseconds']),f(run['AllocatedBytes']/1048576),f(w['ProximitySeconds']),w['ActivatedThrottleCommands']])
parts.append(table(['Próba','Sim [s]','Solver','Solver/s','Substeps','Wall obserw. ms','AI ms','MiB obserw.','Proximity s','Gaz aktywacje'],rows))
parts.append('''
## E. Ocena, regresje i odtwarzanie

**B — CZĘŚCIOWO.** Bazowy #93 współpracuje z normalną walką czterech motocykli. Nowy model ma mierzalną korzyść w kontrolowanej parze i trzech wspólnych korytarzach, lecz pełny zestaw nie potwierdza utrzymania mniejszych odstępów. Zostaje najwyżej jeden nowy eksperymentalny wzór, domyślnie OFF; brak dodatkowej AI, strojenia osiągów, zmian reakcji czy sił. Ocena nie zależy od liczby minięć ani szybszej mety.

Nowe regresje sprawdzają flagę/zależność, wzór, dokładne osiem #93 ON, pełne obiegi Rolling i brak komend po własnej mecie, poprawne kapsuły i brak sztucznego początkowego minięcia, siedem sytuacji, zachowanie nieuniknionego Crash i pełnej reakcji, samotną jazdę i odwróconą kolekcję. Dotychczasowe testy #93 zachowują oddzielne kolejki, anulowanie niebezpiecznej linii, nieprzesuwanie reakcji i rzeczywisty kontakt. Trzy powtórzenia nowego modelu z odwróconą kolekcją w osobnym procesie zachowują hashe, komendy i kontakty. [Powtórzenia](close-racing-following-gap/repeats.json), [kontrole pochodne](close-racing-following-gap/derived-checks.json), [wydajność](close-racing-following-gap/performance.json).

Polecenia (z katalogu repozytorium, .NET 8/Windows):

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
$audit = 'tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
$out = 'work/close-racing-reproduction' # nowy katalog, nie nadpisuje dowodów
dotnet $audit close-racing $out freeze
dotnet $audit close-racing $out rolling-first
dotnet $audit close-racing $out baseline
dotnet $audit close-racing $out variants
# po ocenie pomiarów zapisz selection.md; zamrożony wybór tutaj to wariant 3
Copy-Item docs/experiments/close-racing-following-gap/selection.md $out
dotnet $audit close-racing $out candidate
dotnet $audit close-racing $out repeat
$env:DOTNET_TieredCompilation='0'
dotnet $audit close-racing $out benchmark
python tools/shared-world-speedway/close-racing-report.py $out
```

Generator tylko czyta wyniki; weryfikuje bazową pieczęć, czasy czterech obiegów, wszystkie aktywacje, ukończenia, historyczne hashe okien i powtórzenia, następnie zapisuje raport i manifest. Nie wykonuje symulacji ani optymalizacji współczynników. `.gitattributes` zachowuje bajty dowodów na Windows. Ograniczenie: krótka prosta nie daje dowodu dowolnie długiego podążania .6 m; rzeczywisty lider może zwolnić szybciej niż dopuszcza reakcja. Nie usunięto tego ryzyka.
''')
validation_path=directory/'validation.json'
if validation_path.exists():
    v=read('validation');parts.append('\nWalidacja lokalna: '+v['Summary']+'\n')
output=directory.parent/(directory.name+'.md')
output.write_text('\n\n'.join(parts).rstrip()+'\n',encoding='utf-8')
manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(directory.iterdir()) if p.is_file() and p.name!='manifest.json'}
(directory/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'{output}: {len(heats)} full trials, {len(variants)} short trials; seal/hashes/deadlines/finishes verified; {len(manifest)} files')
