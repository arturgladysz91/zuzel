"""Read frozen exports only; generate #103 tables, standalone plots and SHA-256 manifest.
Usage: python progressive-width-report.py [--plots] (matplotlib needed only for plots).
"""
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs/experiments/progressive-bend-width'
OLD = ROOT / 'docs/experiments/local-offset-limit'
load = lambda p: json.loads(p.read_text(encoding='utf-8'))
extra = load(DATA / 'measurements.json')
old = [m for m in load(OLD / 'measurements.json') if m['Geometry'] == 'egg-mild']
rows = [('A', m, None) for m in old] + [(e['Measurement']['Geometry'][-1].upper(), e['Measurement'], e) for e in extra]
assert len(rows) == 18 and len(extra) == 12
test_proof = load(DATA / 'targeted-tests.json')
assert test_proof['Counters']['passed'] == '42' and test_proof['Counters']['failed'] == '0'
assert all(m['Plan'] == old[i % 6]['Plan'] for i, (_, m, _) in enumerate(rows))

def table(headers, values):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |', '| ' + ' | '.join(['---'] * len(headers)) + ' |'] +
                     ['| ' + ' | '.join(map(str, row)) + ' |' for row in values]) + '\n'

def f(x, digits=3):
    return '—' if x is None else f'{x:.{digits}f}'

def avg(m, key, corner=None):
    return statistics.mean(c[key] for c in m['Corners'] if corner is None or c['Corner'] == corner)

def plots():
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    plt.rcParams.update({'svg.hashsalt': 'progressive-width-103', 'font.family': 'DejaVu Sans'})
    geometries = [load(OLD / 'egg-mild-geometry.json')] + [load(DATA / f'progressive-{v}-geometry.json') for v in 'bc']
    colors = ['#ffb36d', '#70e2e4', '#e5a5f5']
    labels = ['A · 10 m taper', 'B · progressive 16.6 / 16.6 m', 'C · progressive 17 / 16.2 m']
    with plt.style.context('dark_background'):
        fig = plt.figure(figsize=(14, 7), facecolor='#101b28')
        grid = fig.add_gridspec(2, 2, width_ratios=[1.35, 1], wspace=.22, hspace=.35)
        xy = fig.add_subplot(grid[:, 0]); axes = [fig.add_subplot(grid[i, 1]) for i in range(2)]
        for label, color, g in zip(labels, colors, geometries):
            sections = [s for segment in g['segments'] for s in segment['sections']]
            xy.plot([s['outer']['x'] for s in sections], [s['outer']['y'] for s in sections], color=color, label=label, lw=1.7)
            for ax, indexes in zip(axes, [(1, 4), (5, 8)]):
                start = g['segments'][indexes[0]]['startReferenceMeters']; end = g['segments'][indexes[1]]['startReferenceMeters']
                bend = [s for segment in g['segments'][indexes[0]:indexes[1]] for s in segment['sections']]
                ax.plot([(s['referenceMeters'] - start) / (end - start) * 100 for s in bend],
                        [math.hypot(s['outer']['x'] - s['inner']['x'], s['outer']['y'] - s['inner']['y']) for s in bend], color=color, lw=1.7)
        sections = [s for segment in geometries[0]['segments'] for s in segment['sections']]
        xy.plot([s['inner']['x'] for s in sections], [s['inner']['y'] for s in sections], color='#d9e3de', lw=1, label='Unchanged kerb / reference geometry')
        xy.set_aspect('equal', adjustable='box'); xy.set_xlabel('X [m]'); xy.set_ylabel('Y [m]')
        xy.set_title('Outer boundaries in one metric XY frame'); xy.legend(loc='upper center', fontsize=8, bbox_to_anchor=(.5, -.12))
        for i, ax in enumerate(axes):
            ax.set(xlim=(0, 100), ylim=(11.8, 17.3), xlabel=f'Bend {i+1} progress [%]', ylabel='Physical width [m]')
            ax.set_xticks([0, 25, 50, 75, 100]); ax.set_yticks([12, 14, 16, 16.6, 17])
            ax.axvline(50, color='#778b9b', lw=.5, ls=':')
        for ax in [xy] + axes:
            ax.set_facecolor('#101b28'); ax.grid(alpha=.15)
        fig.suptitle('Egg Mild reference unchanged · width hypothesis only', fontsize=15, y=.98)
        fig.text(.5, .015, 'C: assumed maxima at bend midpoints; published widths do not establish an apex survey.', ha='center', fontsize=9, color='#aabac7')
        fig.subplots_adjust(bottom=.16, top=.9)
        fig.savefig(DATA / 'outer-width-comparison.svg', metadata={'Date': None})
        svg = DATA / 'outer-width-comparison.svg'
        svg.write_text('\n'.join(line.rstrip() for line in svg.read_text(encoding='utf-8').splitlines())+'\n', encoding='utf-8', newline='\n')
        fig.savefig(DATA / 'outer-width-comparison.png', dpi=180)
        plt.close(fig)

if '--plots' in sys.argv:
    plots()

history = {}
for name in ['autonomous-racing-line', 'slip-coupled-dynamics', 'slip-control-validation', 'physics-driven-racing-line',
             'rear-wheel-dynamics', 'track-geometry-variants', 'local-offset-limit']:
    folder = ROOT / 'docs/experiments' / name
    manifest = load(folder / 'manifest.json')
    for file, expected in manifest.items():
        assert hashlib.sha256((folder / file).read_bytes()).hexdigest().lower() == expected.lower(), (name, file)
    history[name] = len(manifest)

for variant, m, evidence in rows:
    e = m['Evaluation']
    if e['Feasible']:
        assert e['MinimumOuterClearanceMeters'] >= .5 and m['ConservativeMinimumOuterClearanceMeters'] >= .5
        assert e['MinimumInnerClearanceMeters'] >= .05
        if evidence:
            assert evidence['ConservativeMinimumInnerClearanceMeters'] >= 0
    elif evidence:
        assert evidence['FirstOuterViolation'] and evidence['WorstOuterState']['Outer'] < 0
    if evidence and m['Plan']['Name'] in ['inner', 'middle-tighten', 'wide-tighten']:
        assert e['Fingerprint'] == next(a['Evaluation']['Fingerprint'] for a in old if a['Plan']['Name'] == m['Plan']['Name'])

with (DATA / 'results.csv').open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream)
    writer.writerow(['variant', 'plan', 'flying_s', 'flying_distance_m', 'feasible', 'min_outer_m', 'interval_outer_m', 'min_inner_m', 'interval_inner_m', 'max_offset_m', 'max_abs_vout_mps', 'max_slide_deg', 'max_wheelspin'])
    for v, m, x in rows:
        e = m['Evaluation']; writer.writerow([v, m['Plan']['Name'], e['FlyingSeconds'], e['FlyingDistanceMeters'], e['Feasible'],
            e['MinimumOuterClearanceMeters'], m['ConservativeMinimumOuterClearanceMeters'], e['MinimumInnerClearanceMeters'],
            x['ConservativeMinimumInnerClearanceMeters'] if x else '', m['MaximumActualOffsetMeters'], x['MaximumAbsoluteOutwardSpeed'] if x else '', m['MaximumSlideDegrees'], m['MaximumWheelspin']])
with (DATA / 'corners.csv').open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream); keys = list(old[0]['Corners'][0]); writer.writerow(['variant', 'plan'] + keys)
    for v, m, _ in rows:
        for corner in m['Corners']:
            writer.writerow([v, m['Plan']['Name']] + [corner[k] for k in keys])

lines = ['''# Progressive Bend Width (#103)

**PROVISIONAL. Ocena B — wiarygodne porównanie geometrii i wynik częściowo pozytywny; maksymalna linia nadal niewykonalna.**
Stopniowe zwężanie pozwala wykonać niezmieniony `outer-tighten` z zapasem ponad0,5m, ale nie rozwiązuje `outer-max`.
Rekomendowany profil roboczy: **B**, bez twierdzenia o potwierdzonym realizmie Motoareny.

Baza: Draft #102 `experiment/local-offset-limit`, HEAD `4d65219367d8b27f51d3a5ea3ff2e1f517a6bcd1`.
Osobna gałąź `experiment/progressive-bend-width`, jeden Draft do #102, bez merge. Status końcowego CI i HEAD w PR.

## Zamrożone warunki i zakres

A to dokładny Egg Mild z #102:12m na prostych,16,6m w łukach, pierwsze/ostatnie10m taperu. Jego sześć wyników, geometria i replay są **odczytane z poprzedniego manifestu**, bez powtórzenia audytu.
B ma12→16,6→12m w każdym łuku, C12→17→12m na pierwszym i12→16,2→12m na drugim.
17/16,2m są założonymi maksimami w środku łuków. Publikowane szerokości nie potwierdzają położenia pomiarów ani profilu geodezyjnego; nie są tutaj przedstawiane jako pomiar apexu.

Na każdej połowie B/C `W=12+(Wmax−12)S(u)`, `S=6u⁵−15u⁴+10u³`; u rośnie0→1, następnie maleje1→0.
W, W′ i W″ są zgodne na środku i łączeniach z prostymi; pochodne szerokości wynoszą tam0.
Fizyczna banda to `E(s)=P(s)+(W(s)−1)n(s)`. Kerb `P(s)−n(s)` pozostaje ten sam.
Referencyjne P, heading, curvature i lap318,778749943m są dokładnie identyczne z #101, co sprawdzono na12000 punktach i gęstym audycie XY.
Nie zmieniono opony, wheelspin, sił, yaw, regulatora, masy, AI, nawierzchni ani produkcyjnego `src/` i domyślnego baseline.

Plany z #102 są użyte bez zmian:inner, middle-tighten, wide-tighten, outer-max, outer-tighten, middle-to-outer.
To te same polityki i punkty aktywacji; targety fraction nowych planów wynikają z lokalnego limitu danej bandy, a nie z dostrojenia planu.
Neutralny zawodnik A-neutral, Grip1/Ruts0/Moisture0,5, Drive1, fizyka #100, krok10ms/4substeps,3okrążenia, wspólny stan reference0/offset5/speed18.
Ocena czasu/drogi:Lap2+Lap3. Minima, maksima i naruszenia:całe3lap. Brak wyszukiwania planów, korekt XY/yaw lub fizycznej ściany.
[freeze.json](progressive-bend-width/freeze.json) powstał przed pomiarami. B/C×6 to12 nowych pełnych audytów; A tylko6 tanich kontroli fingerprintów.

## Geometria w XY

![Bandy w tej samej skali i szerokości obu łuków](progressive-bend-width/outer-width-comparison.png)
[Wektorowy SVG](progressive-bend-width/outer-width-comparison.svg). Biała kreda i linia referencyjna nie zmieniają się; wszystkie bandy są w jednym układzie metrycznym.
Gęsty audyt16000 punktów sprawdza regularność parametryzacji, kierunek stycznej, krzywiznę, sag oraz przecięcia nieprzyległych krawędzi metodą sweep bounding boxes + dokładny test odcinków.
Nie ma samoprzecięć, załamań ani poprzecznych łączników/ścian. Zarówno geometria eksportu, jak i audit odczytują tę samą funkcję W(s).
''']
checks = load(DATA / 'boundary-checks.json')
lines.append(table(['Profil', 'max |E″| /m', 'max |κ_bandy| /m', 'min |E′|', 'max sag m', 'przecięcia', 'różnica reference'],
                   [[v, f(c['MaximumSecondDerivative'], 6), f(c['MaximumAbsoluteCurvature'], 6), f(c['MinimumDerivativeLength'], 6), f(c['MaximumChordSagMeters'], 7), c['NonAdjacentIntersections'], c['MaximumReferenceDifference']] for v, c in zip('ABC', checks)]))
lines.append('''Maksymalne różnice kierunku/krzywizny mierzone po obu stronach4mm otoczenia połączeń, łącznie ze środkiem łuku: B0,000110rad/0,00000478m⁻¹, C0,000110rad/0,00000520m⁻¹. To różnice w skończonym otoczeniu, nie skok w samym punkcie. Warunki analityczne szerokości i zerowa krzywizna/jej pochodna na końcach referencyjnego Egg Mild zapewniają zgodność przy prostych. Gęste testy na całej bandzie obejmują także węzły spline i dawne10m miejsca zmiany szerokości. Dla chord≤0,05m zachowany zapas0,7mm jest znacznie większy od zmierzonego sag≤0,0172mm B/C. Geometryczne boundy #101 nadal obowiązują:κ≤0,1/m, |κ′|≤0,1/m² oraz, dla B/C, |W′|<0,193 i |W″|<0,0122/m dają |E″|<2/m. Dowód używa `|E″|≤κ(1+κd)+|κ′|d+2κ|W′|+|W″|`, d≤16m.

## Czas, droga i bezpieczeństwo

† Czas odrzuconego planu jest wyłącznie diagnostyką, bez rankingu. Prześwity dotyczą pełnych kapsuł chassis+handlebar przy faktycznym yaw.
''')
lines.append(table(['Profil / plan', 'Lap2+3 s', 'droga m', 'banda min / interval m', 'kreda min / interval m', 'ocena'],
    [[v+' / '+m['Plan']['Name'], f(m['Evaluation']['FlyingSeconds'], 6)+(' †' if not m['Evaluation']['Feasible'] else ''), f(m['Evaluation']['FlyingDistanceMeters']),
      f(m['Evaluation']['MinimumOuterClearanceMeters'], 4)+' / '+f(m['ConservativeMinimumOuterClearanceMeters'], 4),
      f(m['Evaluation']['MinimumInnerClearanceMeters'], 4)+' / '+f(x['ConservativeMinimumInnerClearanceMeters'] if x else None, 4), 'TAK' if m['Evaluation']['Feasible'] else 'ODRZUCONY'] for v,m,x in rows]))
lines.append('''A4/6 safe, B5/6, C5/6. Wszystkie12 nowych światów kończy3okrążenia; ukończenie nie oznacza bezpieczeństwa.
Lokalny cache ponownie oblicza dystans do całej rzeczywistej bandy: okrąg R1,05m obejmuje obie kapsuły dla dowolnego yaw +0,5m marginesu + zapas komórki≤5cm/float/chord. C używa rzeczywistej maksymalnej szerokości17m również w boundzie komórki. Pełny footprint na granicach komórek przy12yaw jest sprawdzony osobno od dynamiki.

Dolna granica między krokami odejmuje od min obu końców `(max speed +1,05·max |angularVelocity|)·h/2+0,012m`. Ten sam bound policzono dla bandy i kredy. Dotyczy pojedynczego ciała bez kontaktów przy held force/torque; nie stanowi ogólnego CCD. Akceptacja wymaga banda≥0,5m także między krokami, kreda≥0,05m na zaakceptowanych stanach i brak przekroczenia kredy między krokami (bound≥0). Dla inner luźny bound kredy0,0173m certyfikuje brak przekroczenia, nie ciągłe0,05m. A nie miało audytu kredy między krokami w #102; znak — jawnie zachowuje to ograniczenie starej kontroli.
Pozostałe warunki #102: contact/crash, tyre budget, energy residual i powtarzalność Lap2/Lap3. Niezmienione trzy literalne plany mają dokładnie te same fingerprinty co A, mimo zmiany szerokości; powierzchnia jest jednorodna, a droga/dynamika zależą od tej samej reference.

## Wykonane offsety i prędkości obu łuków

Każdy wiersz to średnia tego samego łuku z Lap2/Lap3 (pierwszy:segment1, drugi:segment5). Entry = pierwszy zaakceptowany stan łuku, middle = najbliższy geometrycznym50%, exit = pierwsza zaakceptowana prosta; kolejna prosta = max speed pierwsze45m. Są to rzeczywiste stany, nie targety. [corners.csv](progressive-bend-width/corners.csv) zawiera oddzielne72 rekordy, bez uśredniania okrążeń.
''')
lines.append(table(['Profil / plan / łuk', 'offset entry/middle/exit m', 'speed entry/middle/exit m/s', 'max prosta m/s', 'v_out exit m/s'],
    [[v+' / '+m['Plan']['Name']+' / '+str(i+1), '/'.join(f(avg(m,k,c)) for k in ['EntryOffset','ApexOffset','ExitOffset']),
      '/'.join(f(avg(m,k,c)) for k in ['EntrySpeed','MiddleSpeed','ExitSpeed']), f(avg(m,'NextStraightMaximumSpeed',c)), f(avg(m,'ExitOutwardSpeed',c))]
     for v,m,_ in rows for i,c in enumerate([1,5])]))
lines.append('\n## Ruch poprzeczny, slide i wheelspin\n\nPełne10ms obserwacje v_out (velocity·road.Outward), slide i prześwitów są w nowych plikach `*-dynamics.csv`. Wheelspin to niezmieniony instantaneous rear-wheel slip ratio #100; maksimum wszystkich natywnych obserwacji jest w measurements.json. Dla A zachowujemy istniejące v_out na wyjściu; pełnego maksimum nie dorabiamy z rzadkiego replaya.\n')
lines.append(table(['Profil / plan','max actual offset m','czas actual>9,5m s','v_out exit / max |v_out| m/s','max slide °','max wheelspin κ'],
    [[v+' / '+m['Plan']['Name'],f(m['MaximumActualOffsetMeters']),f(m['AboveOldLimitSeconds'],2),f(avg(m,'ExitOutwardSpeed'))+' / '+f(x['MaximumAbsoluteOutwardSpeed'] if x else None),f(m['MaximumSlideDegrees']),f(m['MaximumWheelspin'],5)] for v,m,x in rows]))
lines.append('\n## Naruszenia i przyczyna\n\nW A oba maksymalne plany zawodziły przy późnym10m zwężeniu. W B/C outer-tighten żąda5m przy40% i zdąża schować rzeczywisty obrys przed prostą. Outer-max stale śledzi lokalne maksimum: na malejącej bandzie cel już wraca, ale motocykl ma wciąż offset większy od bezpiecznego. Pierwsze naruszenie jest jeszcze w łuku, mimo stopniowego zwężania; sama zmiana bandy nie zapewnia wykonania.\n')
lines.append(table(['Profil','pierwsze outer<0,5: t / ref / offset / target','v_out / slide','najgorsze: t / ref / offset / target','outer / interval m'],
    [[v, ' / '.join(f(x['FirstOuterViolation'][k]) for k in ['TimeSeconds','ReferenceMeters','OffsetMeters','TargetMeters']),
      f(x['FirstOuterViolation']['OutwardSpeed'])+' / '+f(x['FirstOuterViolation']['SlideDegrees']),
      ' / '.join(f(x['WorstOuterState'][k]) for k in ['TimeSeconds','ReferenceMeters','OffsetMeters','TargetMeters']),
      f(x['WorstOuterState']['Outer'],4)+' / '+f(x['WorstOuterState']['ConservativeOuter'],4)] for v,m,x in rows if x and x['FirstOuterViolation']]))
lines.append('''Najgorsze stany obu outer-max wypadają pod koniec pierwszego łuku (~90%); szerokość spada już ku12m. W B przy offset12,199m target jest9,652m, rzeczywisty v_out≈−0,981m/s: powrót już trwa, lecz jest spóźniony. C na szerszym pierwszym łuku zwiększa wcześniejsze wyniesienie i osiąga gorszy min outer. Odrzucenia OuterClearanceBelow0.5m i OuterIntervalClearanceBelow0.5m pozostają jawne. Pełne trzy okrążenia, wszystkie zapisane pozycje/yaw, najgorsze stany i10ms dynamika pozostają dostępne; brak teleportacji, przycięcia trasy lub zmiany planów pod wynik.

## Przewaga inner i różnica B/C
''')
lines.append(table(['Profil','safe plan','plan−inner s','droga−inner m'],
    [[v,m['Plan']['Name'],f(m['Evaluation']['FlyingSeconds']-next(a['Evaluation']['FlyingSeconds'] for vv,a,_ in rows if vv==v and a['Plan']['Name']=='inner'),6),
      f(m['Evaluation']['FlyingDistanceMeters']-next(a['Evaluation']['FlyingDistanceMeters'] for vv,a,_ in rows if vv==v and a['Plan']['Name']=='inner'))]
     for v,m,x in rows if m['Plan']['Name']!='inner' and m['Evaluation']['Feasible']]))
lines.append('''B/C nie zmieniają zwycięzcy. Trzy literalne plany są identyczne, nowe bezpieczne middle-to-outer jest około1,912s wolniejsze, a outer-tighten około2,296s wolniejszy od inner w B. Większa entry/exit speed nie rekompensuje dłuższej drogi. B/C różnią się numerycznie, ale maksymalna różnica czasu nowego bezpiecznego planu wynosi tylko1,519ms na dwa lotne okrążenia; to niewielki efekt w tej macierzy, bez twierdzenia o istotności statystycznej lub przewadze na prawdziwym torze. Bezpieczeństwo ma tę samą klasyfikację. C daje outer-max na pierwszym łuku mniejszy margines o0,225m.

## Sześć odpowiedzi i następny krok

1. **Częściowo:** progressive rozwiązuje wyjazd outer-tighten, lecz outer-max nadal ma naruszenie bandy.
2. **B vs C:** małe różnice czasu, taka sama klasyfikacja5/6. Niesymetryczne maksima nie dają tu istotnej korzyści.
3. **Szeroka linia:** można bezpiecznie używać offsetów>9,5m; outer-tighten i middle-to-outer są wykonalne z ciągłym outer bound≥0,5m. Nie dowiedziono utrzymywania maksymalnej linii tuż przy0,5m.
4. **Inner:** zachowuje czas i przewagę; poprawa bezpieczeństwa szerokich planów nie usuwa ich kosztu dłuższej drogi.
5. **Ograniczenie:** geometria wpływa na dostępną przestrzeń, ale po usunięciu późnego zwężenia outer-max nadal jest ograniczony wykonaniem polecenia przez niezmienione sterowanie i dynamikę. To nie dowód błędnej opony ani zbyt małego yaw.
6. **Profil B:** rekomendowany do dalszej pracy jako prostsza, symetryczna hipoteza. C pozostaje wariantem czułości; brak potwierdzonej geodezji.

**Jeden konkretny następny krok:** na zamrożonym B przetestować plan z wcześniejszym powrotem z lokalnego maksimum, przed malejącą połową łuku, bez zmian opony/yaw/regulatora. Ten eksperyment nie wykonuje takiej kalibracji.

## Replay i geometrie

Istniejący [replay viewer](http://127.0.0.1:8000/tools/replay-viewer/?case=width-b-outer-tighten) ma18 opcji A/B/C×6, rzeczywisty kerb/bandę/limit oraz checkbox band A/B/C w jednym XY. Poniżej jest jeden widok porównujący trzy bandy i szerokości obu łuków.
[A inner](http://127.0.0.1:8000/tools/replay-viewer/?case=width-a-inner), [B middle-to-outer](http://127.0.0.1:8000/tools/replay-viewer/?case=width-b-middle-to-outer), [C outer-max odrzucony](http://127.0.0.1:8000/tools/replay-viewer/?case=width-c-outer-max).
Ślady porównawcze to zapisane drugie okrążenia oddzielnych przejazdów inner/middle-to-outer/outer-max. R1 wykonuje własny CSV i heading. To nie wspólny wyścig. JS nie symuluje ruchu ani nie wylicza nowych targetów. Częstotliwość replay≈0,1s nie zastępuje audytu10ms.

![Rzeczywisty bezpieczny outer-tighten B](progressive-bend-width/replay-safe.png)
![Zachowany odrzucony outer-max C](progressive-bend-width/replay-rejected.png)

Zrzuty pokazują dokładne zaakceptowane próbki CSV7,410s (bez interpolacji); C jest celowo powiększone istniejącą kamerą, aby uwidocznić pełny motocykl poza bandą. Selektory A inner/B middle-to-outer/C outer-max oraz postęp odtwarzania i yaw zweryfikowano w przeglądarce.

## Koszt i walidacja

.NET8 Release Windows, tiered JIT OFF, rozgrzanie kodu,5 niezależnych konstrukcji; mediana. Osobno utworzenie referencyjnej geometrii/BVH, lokalny cache i hot path. Cache odczytywany100000razy/trial; projection+width+preview10000razy/trial. Pomiar nie obejmuje renderowania ani offline audytu kapsuł.
''')
perf = load(DATA / 'performance.json')
lines.append(table(['Profil','geometria ms / B','cache ms / B','odczyt cache µs / B','project+width+preview µs / B'],
    [[v, f(statistics.median(t['GeometryMilliseconds'] for t in r['Trials']))+' / '+str(int(statistics.median(t['GeometryBytes'] for t in r['Trials']))),
      f(statistics.median(t['CacheMilliseconds'] for t in r['Trials']))+' / '+str(int(statistics.median(t['CacheBytes'] for t in r['Trials']))),
      f(statistics.median(t['CacheQueryMicroseconds'] for t in r['Trials']),6)+' / '+str(max(t['CacheQueryBytes'] for t in r['Trials'])),
      f(statistics.median(t['ProjectWidthPreviewMicroseconds'] for t in r['Trials']),6)+' / '+str(max(t['ProjectWidthPreviewBytes'] for t in r['Trials']))] for v,r in zip('ABC',perf['Rows'])]))
lines.append(f'''
Koszt zimnego cache pozostaje istotny; brak nowych solver boundaries, limit jest statyczną tablicą. [performance.json](progressive-bend-width/performance.json) zachowuje wszystkie próby, alokacje i runtime.

Restore i Release --warnaserror poprawne; **42/42 C# i11/11 Node**. Nowe testy: identyczna reference, C2 W, XY tangent/curvature/sag/self-intersection, projection, każda krawędź komórki i pełny footprint12yaw, safety safe/unsafe, deterministyczny observer repeat, własne geometry/limit i0B lookup. Kontrole starego baseline, #100/#101/#102 i stare replaye pozostają w zestawie. [validation.json](progressive-bend-width/validation.json) zawiera podsumowanie, a [targeted-tests.json](progressive-bend-width/targeted-tests.json) rzeczywiste42 wyniki wyodrębnione z końcowego TRX. SHA-256 eksportów są w manifest.json.

**6/6 A** zachowało fingerprinty #102; B/C mają4 dodatkowe obserwowane powtórzenia. **{sum(history.values())}/{sum(history.values())} historycznych plików** zachowało SHA-256: {history}. `src/`, RiderController, ContactAdapter i równania opony/koła/yaw niezmienione. Brak pełnego ponownego uruchamiania historycznych audytów. Standardowe Draft Windows CI: końcowy wynik i link w opisie PR.

Reprodukcja z root repo (nowy, pusty katalog zamiast nadpisywania dowodów):

```powershell
dotnet restore
dotnet build -c Release --no-restore --warnaserror
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll progressive-width ../progressive-reproduction freeze
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll progressive-width ../progressive-reproduction measure
$env:DOTNET_TieredCompilation='0'
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll progressive-width ../progressive-reproduction cost
node --test tools/replay-viewer/replay.test.mjs
python -m http.server 8000 --bind 127.0.0.1
```

`progressive-width-report.py --plots` czyta zapisane dane i wymaga matplotlib wyłącznie do PNG/SVG. Bez --plots generuje tabele/CSV/manifest standardową biblioteką. A jest czytane z local-offset-limit; nie jest kopiowane ani nadpisywane. Eksporter symulacji odmawia nadpisania freeze, replaya i pomiarów. Wartości czasu i czasy wykonania cache nie są źródłem decyzji domenowej.
''')
(ROOT / 'docs/experiments/progressive-bend-width.md').write_text('\n'.join(lines), encoding='utf-8', newline='\n')
(DATA / 'validation.json').write_text(json.dumps({'CSharpPassed':42,'NodePassed':11,'FrozenAFingerprintsMatched':6,
    'NewObservedRepeats':4,'HistoricalManifestFiles':history,'BoundaryChecks':checks,
    'InnerIntervalRequirementMeters':0,'OuterIntervalRequirementMeters':.5}, indent=2)+'\n', encoding='utf-8', newline='\n')
manifest = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.iterdir()) if p.is_file() and p.name != 'manifest.json'}
(DATA / 'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8', newline='\n')
print(f'Report written; {len(rows)} rows, {len(manifest)} evidence files; {sum(history.values())} historical hashes preserved.')
