"""Build #95 tables from frozen accepted-world evidence; never simulates or tunes."""
from collections import Counter
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

root=Path(__file__).resolve().parents[2]
directory=Path(sys.argv[1]) if len(sys.argv)>1 else root/'docs/experiments/autonomous-racing-line'
def read(name):return json.loads((directory/(name+'.json')).read_text(encoding='utf-8-sig'))
def f(x,n=3):return '—' if x is None else f'{x:.{n}f}'
def table(head,rows):return '\n'.join(['| '+' | '.join(head)+' |','|'+'---|'*len(head)]+['| '+' | '.join(map(str,row))+' |' for row in rows])
def active(d):return [c for c in d['Run']['Result']['Decisions'] if c['ActualActivation'] is not None]
def auto(d):return [c for c in active(d) if c['Reason'].startswith('Autonomous')]
def counts(d):
    c=[x for x in active(d) if x['Channels']==1]
    return [sum(x['Action'] in (4,5) for x in c),sum(x['Intent']==3 for x in c),sum(x['Intent'] in (5,6,7) for x in c)]
def link(name,mode):return f'[{name}/{mode}](autonomous-racing-line/{name}-{mode}.json)'
solos=['S1-inner','S2-outer','rolling-S3-entry','rolling-S4-exit','S5-low','S5-high']
heats=['A1-rotation-0','A2-rotation-1','close-rotation-0','rolling-straight','rolling-bend']
runs={(n,m):read(n+'-'+m) for n in solos+heats for m in ['OFF','ON']}
for x in read('baseline-seal')['Files']:
    assert hashlib.sha256((directory/x['File']).read_bytes()).hexdigest().upper()==x['Sha256'],x['File']
for (name,mode),d in runs.items():
    r=d['Run']['Result'];assert r['Completed'] and d['Run']['Status']=='Completed'
    assert not r['Contacts'] and not d['Run']['Anomalies']
    for x in d['Run']['Riders']:
        rid=str(x['Measurement']['RiderId']);assert len(x['Measurement']['LapSeconds'])==4
        assert math.isclose(sum(x['Measurement']['LapSeconds']),r['FinishTimes'][rid],abs_tol=1e-8)
    for c in r['Decisions']:
        assert .5<=c['TargetOffsetMeters']<=9.5 and 0<=c['Drive']<=1
        if c['ActualActivation'] is not None:
            assert c['ActualActivation']+1e-9>=c['RequestedActivation']
            assert c['ActualActivation']<=r['FinishTimes'][str(c['RiderId'])]+1e-9
    for c in auto(d):assert c['Channels']==1 and c['Intent']==0 and not c['SafetyReaction']
    if mode=='OFF' and name in heats:
        old=json.loads((root/'docs/experiments/close-racing-following-gap'/(name+'-baseline.json')).read_text())
        assert old['Result']['Fingerprint']==r['Fingerprint'] and old['Result']['Decisions']==r['Decisions']
perf=read('performance')
for p in perf:assert p['Fingerprint']==runs[p['Name'],'ON' if p['Enabled'] else 'OFF']['Run']['Result']['Fingerprint']
replays=read('replays')
for r in replays:
    assert r['ExactHash'] and r['ExactDecisions'] and r['ExactContacts']
    with (directory/r['File']).open() as stream:
        rows=list(csv.DictReader(stream));assert rows and all(math.isfinite(float(x['x_m'])) and math.isfinite(float(x['heading_rad'])) for x in rows)
conflicts=read('conflicts');assert conflicts['ActiveAttackSnapshot']['PreservedActiveAttack']
blocked=conflicts['BlockedNeighbor'];assert any(not e['Available'] and e['TargetMeters']==5.5 and e['TimeSeconds']==.1 for e in blocked['Evaluations'])
assert not auto(blocked)
parts=['''# Autonomous Racing Lines: Entry, Apex and Exit (#95)

**PROVISIONAL — B: CZĘŚCIOWO.** Samodzielny wybór linii działa i poprawia rzeczywiste przejazdy: zewnętrzny start 63.295→61.443 s, przygotowanie przed łukiem 61.051→59.654 s; wewnętrzny start pozostaje identyczny. Wszystkie 10 głównych prób czterech motocykli ukończone bez kontaktu i Crash. Współpraca wymaga jednak dalszej oceny: Rolling Pack na prostej bardziej się rozciąga, a A2 ma około +19.5% wall i +16.5% wywołań solvera wskutek dłuższej bliskości. To nie jest potwierdzenie pełnej optymalności ani podstawa do domyślnego włączenia.

Baza Draft #94 `f996cd1e2c1424659dd9d106f63c007e5aa1ea0f`, branch `experiment/close-racing-following-gap`. Jeden nowy Draft, bez merge. `EnableAutonomousRacingLine=false` domyślnie, wymaga wyłącznie Tactical. Główne próby: Tactical=true, SideBySide=true, ProportionalThrottle=true, Quality=false, ClosureAdaptiveFollowingGap=false. Motoarena, jednolita zamrożona nawierzchnia Grip1/Ruts0/Moisture.5 (EffectiveGrip .9175), .01 s, 4 substeps, AI .1 s. Siły, Box2D, kontakty, grip, umiejętności, produkcyjny HeatSimulator i algorytm gazu #93 nie zmieniają się.

## A. Diagnoza i zamrożenie

[Konfiguracja/profily](autonomous-racing-line/configuration.json), [pieczęć bazowych plików](autonomous-racing-line/baseline-seal.json), [odpowiedzi przed implementacją](autonomous-racing-line/diagnosis.md). Najpierw 8 solo OFF (6 przypadków i 2 zadeklarowane kontrole wewnętrznej) oraz 5 pełnych OFF; ich hashe i komendy odpowiadają #94. Następnie jeden celowany S3 ON, pozostałe solo i pięć pełnych ON. Bez zmiany profili ani strojenia parametrów po wynikach. Korekta jednostek horyzontu w łuku po przeglądzie została sprawdzona przez ponowienie ON: wszystkie 11 hashy zaakceptowanych stanów pozostały identyczne. Baseline nie został nadpisany.

Bez rywali `TrafficDecision.Tactics` kończyło się brakiem komendy: poszukiwało ataku/obrony/SideBySide, a nie korzyści swobodnej jazdy. `HomeOffsetMeters` inicjalizowało się z celu startowego. Powroty po minięciu i obronie odtwarzały ten offset. #93 bez Front tylko otwiera gaz; #94 zmienia preferencję podążania, nie wybiera szybszej linii. #91 oceniał lokalne alternatywy walki i nie stanowi dowodu skutecznego wolnego przejazdu.

Istniejące Forces już płaci za zmianę: PD błędu i prędkości bocznej dokłada przyspieszenie do krzywizny; potrzebny scrub i skalowanie całego wektora w kręgu przyczepności ograniczają napęd. Żądany promień wpływa na przygotowanie prędkości. Zewnętrzna linia ma większą prędkość styczną, ale dłuższą drogę. Rzeczywiste stałe kontrole poniżej pokazują, że na tej nawierzchni wewnętrzna wygrywa również na pełnym lotnym okrążeniu, dla obu różnych możliwości motocykla.
''']
rows=[]
for name,inner in [('S2-outer','S1-inner'),('S5-low','S5-low-inner-control'),('S5-high','S5-high-inner-control')]:
    a=read(inner+'-OFF');b=runs[name,'OFF']
    rows.append([name,f(a['Run']['Riders'][0]['Measurement']['LapSeconds'][-1]),f(b['Run']['Riders'][0]['Measurement']['LapSeconds'][-1]),
        f(a['Force'][0]['MinimumCornerSpeed']),f(b['Force'][0]['MinimumCornerSpeed'])])
parts.append(table(['Profil','Lotne wewn. .5m [s]','Lotne zewn. 9.5m [s]','Min łuk wewn. [m/s]','Min łuk zewn. [m/s]'],rows))
parts.append('''
## B. Mała polityka, oddzielna ocena i wykonanie

Trzy możliwości: utrzymaj obecny target, target−1.5 m, target+1.5 m. Kandydaci poza .5..MinimumUsableWidth−.5 są odrzucani, nie teleportowani/clampowani na nową linię. Korzyść musi przekraczać .15 m/s średniego postępu na całym horyzoncie. Brak bonusu prędkości, losowania i nakazu jazdy wewnątrz. Ta dyskretna próbka nie gwarantuje optimum: np. S3 kończy na target 1 m, bo następny pełny krok wykracza poza tor.

Fazy Approach/Entry/Apex/Exit wynikają z geometrii i trzecich części postępu łuku. Horyzont obejmuje pozostały lub najbliższy łuk oraz 20 referencyjnych metrów kolejnej prostej, uwzględnia reakcję, minimum 2 s i maksimum 12 s. Jeśli pełny fragment jeszcze się nie mieści, ocena czeka; start nie jest natychmiastowym ruchem w bok. Długość fizycznego łuku z TrackFrame jest przeliczona na wewnętrzne metry przed podzieleniem przez tempo referencyjne.

Ocena ponownie używa **ContestScoring.Progress**, niezależnie od flagi scoringu #91: istniejący analityczny PD po pełnej reakcji, czas potrzebnego scrub, sześć trapezów ograniczeń prędkości, promień i długość drogi przez ReferenceSpeed, grip, boczne żądanie, pozostały napęd/opor i odzyskiwanie tempa. Horyzont przechodzi przez Entry/Apex/Exit i początek prostej; nie optymalizuje tylko aktualnego offsetu. **Nie wykonuje Forces ani Box2D dla kandydatów, nie integruje nowego ciała i nie ma równoległego świata.** Geometria jest próbkowana przy bieżącym tempie referencyjnym, więc to przybliżenie kosztu, nie drugi symulator. Rzeczywisty wynik pochodzi wyłącznie z WorldRunner.

Po utrzymaniu celu przegląd co .5 s, po zmianie pełne reaction+3*LateralResponseSeconds (około 2.19 s). Pamięć przechowuje historyczny Home, ostatni aktywowany cel autonomiczny, fazę i czas ponownej oceny; bieżący cel taktyczny nadal jest TargetOffsetMeters/intencją. Home pozostaje historyczny. W ON powroty używają celu autonomicznego, aktualizowanego dopiero przy aktywacji; OFF zachowuje dawny Home dokładnie.

Priorytet: istniejące Emergency i OccupiedAlongsideCut → aktywna/pending taktyka → kontrolowane podążanie → autonomia. Wspólny aktualny korytarz z Front do 10 m blokuje autonomię. Intencje Attack/Defense/Contest/Return nie są zastępowane nowym optimum. Kandydat musi przejść dotychczasowe Corridor, CrossesOccupied z pełnymi kapsułami, granice toru i test aktualnej separacji .3 m. Przewidywanie korytarza jest ograniczone do reakcji+3 tau ruchu; nie zakłada tajnych przyszłych komend rywali. Nowa linia idzie przez tę samą opóźnioną kolejkę **Line**, z Drive=own.Drive. Nie ma prawa otworzyć gazu wbrew niezależnej kolejce #93. Oczekującą linię może anulować istniejące bezpieczeństwo.

## C. Solo: rzeczywiście wykonane cztery obiegi

S1 pole A, S2 pole D; S3 zaczyna 15 m przed łukiem (ref20, offset7, 22 m/s); S4 w wyjściu łuku (ref110, offset7, 22 m/s). S5 to istniejące pary legacy Speed/SlideControl 20/20 i 80/80, pole D, pozostałe parametry neutralne. S3/S4 mierzą pełne cztery referencyjne obiegi od początkowej pozycji, oddzielnie od startu z bramek. Wszystkie solo mają normalną AI bez rywali i zero komend gazu.
''')
rows=[]
for name in solos:
    a=runs[name,'OFF'];b=runs[name,'ON'];ta=a['Run']['Result']['FinishTimes']['1'];tb=b['Run']['Result']['FinishTimes']['1']
    rows.append([link(name,'ON'),f(ta),f(tb),f(tb-ta),'/'.join(f(x) for x in b['Run']['Riders'][0]['Measurement']['LapSeconds']),len(auto(b)),
        ' → '.join(f(x['TargetOffsetMeters'],1) for x in auto(b)) or 'utrzymanie'])
parts.append(table(['Przypadek','4 obiegi OFF [s]','ON [s]','ON−OFF [s]','Okrążenia ON 1/2/3/4 [s]','Zmiany autonom.','Aktywowane cele [m]'],rows))
parts.append('''
Prędkości i koszty poniżej to wykonane stany/siły, nie wynik funkcji oceny. Entry/Exit/Exit+20 m to średnie z przekroczeń danej geometrii w czterech obiegach; minima łuku obejmują cały przejazd. Oznaczenie `Lap` w JSON faz jest indeksem stałego obiegu referencyjnego toru, a nie początkiem indywidualnego pomiaru Rolling. ∫Ft+/m i ∫Ft−/m są składowymi całki siły stycznej (m/s), nie samą zmianą wartości speed przy skręcaniu. Ujemny czas Ft obejmuje dotychczasową korektę i opór; nie całość można przypisać ruchowi bocznemu. Pole AppliedWorkJoules w JSON to lewostronne przybliżenie liniowej pracy F·v, bez pracy momentu obrotowego.
''')
rows=[]
for name in solos:
    for mode in ['OFF','ON']:
        d=runs[name,mode];force=d['Force'][0]
        phase=lambda x:statistics.mean(e['Speed'] for e in d['Phases'] if e['Phase']==x)
        rows.append([name,mode,f(phase('Entry')),f(phase('Exit')),f(phase('Exit+20m')),f(force['MinimumCornerSpeed']),
            f(force['PositiveTangentialDeltaV']),f(force['NegativeTangentialDeltaV']),f(force['CorrectionSeconds']),f(force['MaximumGripRatio'],6)])
parts.append(table(['S','Tryb','Entry m/s','Exit m/s','Exit+20 m/s','Min łuk m/s','∫Ft+/m','∫Ft−/m','Ft<0 [s]','Max grip ratio'],rows))
parts.append('''
S3 wybiera cel 5.5 m w .100 s, aktywuje .340 s, przed Entry .666 s. Offset na wejściu jest nadal 6.861 m; przy Apex 5.579 m. Nie przeskakuje z 7 na 5.5. Początkowe wyjście jest wolniejsze, ale całe cztery obiegi wygrywają — decyzji nie zaliczono tylko za przewidywany zysk. S4 na początkowym wyjściu utrzymuje linię, a następny cel aktywuje dopiero na prostej (1.840 s). S1 nie poprawia czasu, ponieważ utrzymanie już korzystnej linii wygrywa; hash OFF/ON jest identyczny.

Oba S5 dochodzą do .5 m, ale w różnym czasie (niski 32.740 s, wysoki 36.740 s). Odrębne stałe kontrole .5/9.5 m potwierdzają tę samą preferencję fizyczną dla obu możliwości; to nie dowód ignorowania osiągów. Nie ma dowodu, że na dowolnym gripie/torze optimum będzie identyczne. Na tym zestawie żaden wykonany cel autonomiczny nie poszerza linii; model rozważa poszerzenie, ale jego koszt i dodatkowa droga nie wygrywają. Entry/Apex/Exit są oceniane i zazwyczaj kończą się Hold, nie wymuszoną fazową ścieżką.
''')
phase_counts=Counter(e['Phase'] for e in runs['S2-outer','ON']['Evaluations'] if e['Reason']=='Hold')
parts.append('S2 liczba ocen faz: '+', '.join(f'{n}={phase_counts[i]}' for i,n in enumerate(['Approach','Entry','Apex','Exit']))+'. Wszystkie 6 zmian aktywowano w Approach.\n')
parts.append('''
## D. Pięć konfiguracji × OFF/ON

Profile, pola i pozycje dokładnie #94. A1/0, A2/1, close/0 od taśmy; rolling-straight i rolling-bend osobno. Czasy R1/R2/R3/R4; kolejność Rolling to czasy czterech pełnych obiegów od indywidualnych początków, nie wspólna meta regulaminowa. Minięcia pochodzą z dotychczasowego pełnego obserwatora kapsuł i utrzymanego prowadzenia, nie z początkowego rozstawienia ani samej zmiany kolejności czasów.

Autonomia = aktywacje nowego celu, A/D/S = aktywowane ataki/obrony/SideBySide wyłącznie kanału Line (atak może też być SideBySide). Podążanie częściowo to rider-sekundy w aktualnym wspólnym korytarzu ≤10 m, ważone zaakceptowanymi interwałami jak #94. Szczegółowe czasy okrążeń, decyzje, anulowania i zmiany na kolejnych obiegach są w podlinkowanym JSON dla każdego zawodnika.
''')
rows=[]
for name in heats:
    for mode in ['OFF','ON']:
        d=runs[name,mode];g=d['Run'];r=g['Result'];rows.append([link(name,mode),'/'.join(f(r['FinishTimes'][str(i)]) for i in range(1,5)),
            '>'.join('R'+str(x['RiderId']) for x in r['Classification']),len(auto(d)),'/'.join(map(str,counts(d))),len(g['Passes']),
            f(sum(x['FollowingSeconds'] for x in g['Riders'])),f(sum(x['PartialFollowingSeconds'] for x in g['Riders'])),
            f(min(x['MinimumSeparationMeters'] for x in g['Riders'])),f'{r["Work"]["Episodes"]}/0'])
parts.append(table(['Próba','4 obiegi [s] R1–R4','Kolejność','Autonomia','A/D/S','Minięcia','Podąż. s','Częściowo podąż. s','Min fiz. m','Kontakt/Crash'],rows))
parts.append('''
10/10 ukończonych, bez kontaktów, konsekwencji, NaN, teleportacji, przekroczeń granicy toru i zarejestrowanych zbliżeń mechanicznych <.3 m. To nie gwarancja dowolnej sytuacji. A1/close zaczynają samodzielnie zacieśniać po osiągnięciu tempa i horyzontu, zanim pojawi się realny pościg. Po zbliżeniu autonomia ustępuje gazowi/taktyce: A1 ma 1.660 s, close 1.400 s częściowego podążania. A2 ma 2 minięcia zamiast 1, lecz brak aktywowanej obrony w tym konkretnym przebiegu; sama liczba zdarzeń nie jest kryterium jakości. W rolling-straight 9 sugestii autonomicznych, 7 aktywacji — dwie anulowane, nie zaliczone jako wykonany ruch.

**Rolling Pack nie pozostaje tak samo zwarty.** Porównanie wspólnego czasu przy czterech nadal jadących zawodnikach (przed jakąkolwiek metą) mierzy max−min postępu referencyjnego, nie odległość fizyczną kapsuł. ON na prostej rozciąga grupę o około 6.4 m w 40 s; w łuku różnica jest mała. Zawodnicy poprawiają własne czasy, ale nie dowiedziono, że każde opuszczenie grupy pomaga walce. Ten wynik ogranicza ocenę końcową.
''')
pack=read('pack-windows');rows=[]
for name in ['rolling-straight','rolling-bend']:
    for target in [10,20,30,40]:
        xs=[min([x for x in pack if x['Name']==name and x['Enabled']==e],key=lambda x:abs(x['State']['TimeSeconds']-target)) for e in [False,True]]
        rows.append([name,f(xs[0]['State']['TimeSeconds'],2),f(xs[0]['ReferenceSpreadMeters']),f(xs[1]['ReferenceSpreadMeters'])])
parts.append(table(['Pack','Wspólny czas s','Rozpiętość OFF m','ON m'],rows))
parts.append('''
Konflikt sąsiedzki: R1 ref20/offset7/22 m/s, R2 obok offset5.8/24 m/s; kapsuły początkowo oddzielone. O .1 s kandydat 5.5 m jest zablokowany, poszerzenie 8.5 m ocenione gorzej od Hold. Zero autonomicznych komend przez całe 8 s; normalna taktyka może nadal działać. Przegląd nie blokuje się na całe okrążenie. Osobny odczyt snapshotu reprezentuje aktywny AttackOutside na 8.5 m, podczas gdy samodzielna ocena bez rywala preferuje 7 m. Pełne Observe zachowuje atak i nie emituje przeciwnej komendy. To jawny test odczytu stanu pamięci, nie rzekomo wykonany nowy manewr. [Konflikty i stan](autonomous-racing-line/conflicts.json).

## E. Koszt AI i skutki nowej trajektorii

Bare WorldRunner, bez obserwatorów/CSV; po jednej rozgrzewce trzy próbki pełnego przejazdu, mediana wall/AI/line/alloc. .NET 8 Release na tym samym Windows, DOTNET_TieredCompilation=0. Wszystkie 18 hashy odpowiadają właściwemu audytowi. `AutonomousCandidates` liczy rzeczywiście ocenione numerycznie cele; odrzucone korytarze/granice są osobno. Razem dają 3 alternatywy na ocenę. Komendy = wyemitowane/aktywowane; cała taktyka obejmuje również koszt nowego wyboru.
''')
rows=[]
for name in ['S2-outer','A2-rotation-1','rolling-straight']:
    for enabled in [False,True]:
        xs=[x for x in perf if x['Name']==name and x['Enabled']==enabled];x=xs[0];w=x['Work'];median=lambda fn:statistics.median(fn(y) for y in xs)
        rows.append([name,'ON' if enabled else 'OFF',f(x['TimeSeconds']),w['SolverCalls'],w['SolverSubsteps'],
            f(median(lambda y:y['ElapsedMilliseconds'])),f(median(lambda y:y['Work']['TacticalElapsedMilliseconds'])),
            f(median(lambda y:y['Work']['AutonomousElapsedMilliseconds'])),f(median(lambda y:y['AllocatedBytes'])/1048576),
            w['AutonomousEvaluations'],f'{w["AutonomousCandidates"]}/{w["AutonomousBlockedCandidates"]}',f'{w["AutonomousCommands"]}/{w["AutonomousActivations"]}'])
parts.append(table(['Próba','Tryb','Sim s','Solver','Substeps','Wall ms','AI ms','Wybór linii ms','MiB','Oceny','Cele/blocked','Komendy'],rows))
parts.append('''
A2: 6237→7268 solver calls, 0→1073 nominalnych extra proximity, 71→75 clip krzywizny i 20→17 clip deadline; świat trwa 61.46→61.05 s. Czas z istniejącym podziałem proximity 0→10.73 s. Nowy wybór kosztuje około 2.94 ms na pełny bieg, ale rzeczywista zmieniona trajektoria podnosi wall 136.07→162.65 ms (+19.5%) i alokacje. Tego wzrostu nie ukrywamy pod małym kosztem samej formuły. Rolling-straight ma mniej solver calls, ale również inną trajektorię, więc spadek wall nie jest optymalizacją Box2D. Nie dodano nowych substeps ani solverów dla kandydatów. [Surowe próbki](autonomous-racing-line/performance.json).

## F. Gotowość danych do wizualizacji #96

Zachowano **istniejący format HeatMeasurements CSV**, dwa rzeczywiste powtórzenia ON:

- [S3 przed łukiem](autonomous-racing-line/rolling-S3-entry-accepted.csv): czas, rider, X/Y, vx/vy, speed, reference, fizyczny offset, target, Drive, Heading, activity.
- [Cztery motocykle w Rolling Pack](autonomous-racing-line/rolling-straight-accepted.csv): te same pola dla czterech rzeczywistych ciał.

Są to zaakceptowane stany co około .1 s (pierwszy dostępny solver boundary), nie ponownie policzone pozycje. Intencja, observed/requested/actual activation, anulowania, kontakty i konsekwencje są w odpowiednim JSON przejazdu, Result.Decisions/Contacts i Consequences. Fazy i obserwacje kandydatów są diagnostyką decyzji, nie ruchem do odtwarzania. Zachowane orientacje to odczyt ciała, nie kierunek stycznej. #96 powinien używać CSV i zdarzeń; nie uruchamiać drugiego modelu ruchu. Taki zapis nie jest zrzutem wszystkich czterech substeps — szczegóły kontaktów należy brać z ich dokładnych znaczników czasu. Kolektor kończy zapis zawodnika na jego własnej mecie; do reprezentatywnej walki czterech motocykli dostępne jest wspólne okno 0..45 s przed metami, bez uzupełniania brakujących pozycji innym modelem. Bez renderera, UI i nowej biblioteki. [Dokładne hashe obu odtworzeń](autonomous-racing-line/replays.json), cztery motocykle również z odwróconej kolekcji wejściowej.

## Ocena i walidacja

**B — CZĘŚCIOWO.** Potwierdzono fizycznie sensowną, stabilną samodzielną jazdę, korzyść solo i kilka nowych sytuacji podążania w pełnych biegach. Brak podstaw do pełnego A ze względu na rozciąganie grupy i koszt A2; nie oceniono zróżnicowanej nawierzchni ani sytuacji, w której poszerzenie rzeczywiście wygra. Ograniczona próbka celów zostaje eksperymentalna i domyślnie OFF. Nie zmieniono parametrów w celu poprawienia tych wyników.

Regresje sprawdzają: wymagania flagi bez #91/#94, historyczne pięć #94 OFF oraz jego adaptacyjny wariant, geometryczne jednostki horyzontu, solo wewnętrzne bez zmiany hasha, przygotowanie przed Entry, pełną reakcję i zakres celów, oddzielne Home/autonomię, konflikt sąsiada i przeciwnego ataku, nieunikniony Crash/Emergency, brak nadpisania gazu i aktywacji po mecie oraz odwróconą kolekcję. Dotychczasowe #92–#94 sprawdzają również stare osiem OFF/ON, kolejki i realny kontakt. Manifest SHA-256 obejmuje surowe dowody; bazowa pieczęć pozostaje niezmieniona.

Odtworzenie z katalogu repozytorium:

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
$audit='tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
$out='work/autonomous-line-reproduction'
foreach($stage in @('baseline','focus','solo','heats','conflicts','replay','pack-windows')) {
  dotnet $audit autonomous-line $out $stage
}
$env:DOTNET_TieredCompilation='0'
dotnet $audit autonomous-line $out benchmark
python tools/shared-world-speedway/autonomous-line-report.py $out
```

Baseline/ON nie są nadpisywane przez CLI. Generator raportu czyta pliki, sprawdza pieczęć, hashe, reakcję, mety, granice, odtworzenia i benchmarki; nie uruchamia symulacji ani optymalizacji.
''')
if (directory/'validation.json').exists():parts.append('Walidacja lokalna: '+read('validation')['Summary'])
(directory.parent/(directory.name+'.md')).write_text('\n\n'.join(parts).rstrip()+'\n',encoding='utf-8')
manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(directory.iterdir()) if p.is_file() and p.name!='manifest.json'}
(directory/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print(f'Baseline seal, 22 runs, 18 benchmark hashes and 2 accepted-state replays verified; {len(manifest)} files.')
