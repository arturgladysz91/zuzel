"""Read frozen #101 evidence, verify history and export tables/figures. Never execute physics."""
import csv
import hashlib
import json
import math
import statistics
import xml.etree.ElementTree as ET
from html import escape
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs/experiments/track-geometry-variants'
load = lambda name: json.loads((DATA / (name + '.json')).read_text(encoding='utf-8-sig'))
write = lambda name, value: (DATA / (name + '.json')).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
rows, historical = load('measurements'), load('historical-controls')
stats, rejected, performance = load('geometry-validation'), load('rejected-details'), load('performance')
shapes = ['baseline'] + [r['Name'] for r in stats]
plans = [p['Name'] for p in load('freeze')['Plans']]
by = {(r['Geometry'], r['Evaluation']['Plan']['Name']): r for r in rows}
assert len(rows) == 35 and len(historical) == 6
assert all(abs(r['Energy']['BalanceResidualJoules']) < 5 for r in rows + historical)
assert all(r['Evaluation']['MaximumGripUtilization'] <= 1.00001 for r in rows + historical)
assert all(r['Work']['SolverCalls'] == r['Work']['RearWheelCommits'] for r in rows)
assert all(r['Fingerprint'] == r['ObservedFingerprint'] for r in load('repeatability'))
assert [r['Geometry'] for r in rejected] == ['egg-medium', 'egg-strong', 'egg-asymmetric']
assert load('selection') == ['egg-mild']
history = {}
for folder in ['autonomous-racing-line', 'slip-coupled-dynamics', 'slip-control-validation', 'physics-driven-racing-line', 'rear-wheel-dynamics']:
    base = ROOT / 'docs/experiments' / folder
    manifest = json.loads((base / 'manifest.json').read_text())
    assert all(hashlib.sha256((base / name).read_bytes()).hexdigest() == sha for name, sha in manifest.items()), folder
    history[folder] = len(manifest)
previous = json.loads((ROOT / 'docs/experiments/rear-wheel-dynamics/line-comparison.json').read_text())
baseline_matches = []
for old in previous:
    if old['Case'] != 'A-neutral':
        continue
    current = by['baseline', old['Plan']] if old['Model'] != 'historical' else next(r for r in historical if r['Geometry'] == 'baseline' and r['Evaluation']['Plan']['Name'] == old['Plan'])
    assert current['Evaluation']['Fingerprint'] == old['Evaluation']['Fingerprint']
    assert current['Evaluation']['FlyingSeconds'] == old['Evaluation']['FlyingSeconds']
    baseline_matches.append({'Model': old['Model'], 'Plan': old['Plan'], 'Fingerprint': current['Evaluation']['Fingerprint']})
assert len(baseline_matches) == 6
assert (DATA / 'baseline-geometry.json').read_bytes() == (ROOT / 'tools/replay-viewer/geometry.json').read_bytes()
trx = ET.parse(DATA / 'targeted-release.trx').getroot()
counts = trx.find('{*}ResultSummary/{*}Counters').attrib
assert int(counts['passed']) == 32 and counts['failed'] == '0'
for file in DATA.glob('*.json'):
    assert '"NaN"' not in file.read_text(encoding='utf8'), file

def avg(row, key):
    return statistics.mean(c[key] for c in row['Corners'])

def table(headers, data):
    return '\n'.join(['| ' + ' | '.join(headers) + ' |', '|' + '---|' * len(headers)] + ['| ' + ' | '.join(map(str, r)) + ' |' for r in data])

records = []
for r in rows + historical:
    e = r['Evaluation']
    entry = {'geometry': r['Geometry'], 'wheel': r['Wheel'], 'plan': e['Plan']['Name'], 'feasible': e['Feasible'], 'rejections': ';'.join(e['Rejections']),
             'flying_seconds': e['FlyingSeconds'], 'flying_distance_m': e['FlyingDistanceMeters'], 'three_lap_distance_m': e['DistanceMeters'],
             'minimum_corner_speed_mps': min(c['MinimumSpeed'] for c in r['Corners']), 'maximum_slide_deg': r['MaximumSlideDegrees'],
             'maximum_instant_kappa': r['MaximumSlipRatio'], 'minimum_inner_clearance_m': e['MinimumInnerClearanceMeters'],
             'minimum_outer_clearance_m': e['MinimumOuterClearanceMeters'], 'outside_accepted_samples': e['OutsideSamples'],
             'whole_system_energy_residual_j': r['Energy']['BalanceResidualJoules'], 'fingerprint': e['Fingerprint']}
    entry.update({key: avg(r, key) for key in ['EntrySpeed','MiddleSpeed','ExitSpeed','EntryOffset','ApexOffset','ExitOffset','StraightOffset','ExitOutwardSpeed','AddedWideningFirst12Meters','NextStraightMaximumSpeed']})
    records.append(entry)
with (DATA / 'results.csv').open('w', newline='', encoding='utf8') as f:
    writer = csv.DictWriter(f, records[0].keys()); writer.writeheader(); writer.writerows(records)
with (DATA / 'corners.csv').open('w', newline='', encoding='utf8') as f:
    flat = [dict(geometry=r['Geometry'], wheel=r['Wheel'], plan=r['Evaluation']['Plan']['Name'], **c) for r in rows + historical for c in r['Corners']]
    writer = csv.DictWriter(f, flat[0].keys()); writer.writeheader(); writer.writerows(flat)

# Small code-native SVG exports: geometry and accepted data only, no resimulation.
palette = ['#334155', '#64748b', '#b45309', '#0284c7', '#15803d', '#be123c', '#7c3aed']
def svg_start(width, height, title):
    return [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" role="img"><title>{escape(title)}</title><rect width="100%" height="100%" fill="white"/>']
def text(svg, x, y, label, size=12, color='#172033'):
    svg.append(f'<text x="{x}" y="{y}" font-family="Arial,sans-serif" font-size="{size}" fill="{color}">{escape(str(label))}</text>')
def poly(svg, points, color, width=1, close=False):
    value = ' '.join(f'{x:.3f},{y:.3f}' for x,y in points)
    svg.append(f'<polyline points="{value}" fill="none" stroke="{color}" stroke-width="{width}"'+ (' />' if not close else ' />'))

svg = svg_start(1000, 1320, 'Frozen track variants, same metre scale')
text(svg, 20, 28, 'Track geometry variants · reference + both physical edges · same scale', 18)
for i, shape in enumerate(shapes):
    geometry = load(shape + '-geometry'); sections = [p for s in geometry['segments'] for p in s['sections']]
    col, row = i % 2, i // 2
    ox, oy, scale = 165 + col*500, 300 + row*310, 2.2
    def point(p): return ox+p['x']*scale, oy-p['y']*scale
    for key, color, line_width in [('outer','#94a3b8',1.4),('inner','#94a3b8',1.4),('reference',palette[i],1.8)]:
        poly(svg, [point(p[key]) for p in sections], color, line_width)
    text(svg, 20+col*500, 60+row*310, shape, 15, palette[i])
    text(svg, 20+col*500, 78+row*310, f"L={geometry['lapMeters']:.3f} m · straights=62 + 62 m")
    if i:
        st = stats[i-1]
        text(svg, 20+col*500, 96+row*310, f"bulge={st['FirstExtentMeters']:.2f}/{st['SecondExtentMeters']:.2f} m · transverse span={st['HeightMeters']:.2f} m", 11)
poly(svg,[(20,1290),(130,1290)],'#172033',2);text(svg,20,1280,'50 m',12)
svg.append('</svg>'); (DATA / 'shapes.svg').write_text('\n'.join(svg), encoding='utf8')

def line_chart(filename, title, panels, ylo, yhi, ylabel):
    svg = svg_start(1100, 370*len(panels)+35, title)
    text(svg, 16, 25, title, 18)
    for panel, (label, series) in enumerate(panels):
        top = 65+panel*370; left=65; bottom=top+250; width=1000
        text(svg, left, top-12, label, 14)
        for j in range(6):
            y = top+250-j*50; value=ylo+(yhi-ylo)*j/5
            poly(svg, [(left,y),(left+width,y)], '#e2e8f0'); text(svg, 8,y+4,f'{value:.3g}',11)
        for j in range(9):
            x=left+width*j/8; poly(svg, [(x,top),(x,bottom)],'#e2e8f0'); text(svg,x-8,bottom+20,round(318.77875*j/8),11)
        text(svg,left,bottom+40,'Reference metres along lap'); text(svg,8,top-10,ylabel,10)
        for j,(name,points,color) in enumerate(series):
            poly(svg,[(left+x/318.77875*width, bottom-(y-ylo)/(yhi-ylo)*250) for x,y in points],color,1.6)
            text(svg,left+(j%4)*250,bottom+60+(j//4)*18,name,11,color)
    svg.append('</svg>'); (DATA/filename).write_text('\n'.join(svg),encoding='utf8')

panels=[]
for plan in plans[:3]:
    series=[]
    for shape in ['baseline','egg-mild','egg-medium','egg-strong']:
        r=by[shape,plan]; label=shape+(' (rejected)' if not r['Evaluation']['Feasible'] else '')
        series.append((label,[(t['Reference'],t['Speed']) for t in r['Trace']],palette[shapes.index(shape)]))
    panels.append((plan+' · second lap · actual accepted speed',series))
line_chart('speed.svg','Speed distribution · wheel ON · no fitted plans',panels,16,25,'m/s')
series=[]
for i, shape in enumerate(shapes):
    r=by[shape,'inner']; series.append((shape,[(t['Reference'],t['Curvature']) for t in r['Trace']],palette[i]))
line_chart('curvature.svg','Reference curvature · zero on straights · shoulder peaks in compressed bends',[('Same 318.779 m lap',series)],0,.06,'1/m')

timing = table(['Geometria','inner s','middle s','wide s','wide-entry s','open-exit s'],
    [[s]+[f"{by[s,p]['Evaluation']['FlyingSeconds']:.6f}"+(' †' if not by[s,p]['Evaluation']['Feasible'] else '') for p in plans] for s in shapes])
deltas=[]
for s in shapes:
    a,b,c=[by[s,p]['Evaluation'] for p in plans[:3]]
    deltas.append([s,f"{b['FlyingSeconds']-a['FlyingSeconds']:+.6f}",f"{c['FlyingSeconds']-a['FlyingSeconds']:+.6f}", ' / '.join(r['Evaluation']['Plan']['Name'] for r in sorted((by[s,p] for p in plans),key=lambda r:r['Evaluation']['FlyingSeconds']) if r['Evaluation']['Feasible'])])
geometry_table=table(['Geometria','c₁/c₂','zasięg x łuk1/2 m','rozstaw y m','κmax 1/m','Rmin m'],
    [['baseline','—','31 / 31','62','0.032258','31']]+[[r['Name'],f"{r['FirstCompression']}/{r['SecondCompression']}",f"{r['FirstExtentMeters']:.3f} / {r['SecondExtentMeters']:.3f}",f"{r['HeightMeters']:.3f}",f"{r['MaximumCurvature']:.6f}",f"{1/r['MaximumCurvature']:.3f}"] for r in stats])
speed_rows=[]
for s in ['baseline','egg-mild','egg-medium','egg-strong','egg-asymmetric']:
    for p in plans[:3]:
        r=by[s,p]; e=r['Evaluation']
        speed_rows.append([s+'/'+p+(' †' if not e['Feasible'] else ''),f"{e['FlyingDistanceMeters']:.3f}",
            f"{min(c['MinimumSpeed'] for c in r['Corners']):.3f}",f"{avg(r,'EntrySpeed'):.3f} / {avg(r,'MiddleSpeed'):.3f} / {avg(r,'ExitSpeed'):.3f}",
            f"{avg(r,'ExitOutwardSpeed'):.3f}",f"{avg(r,'AddedWideningFirst12Meters'):.3f}",f"{e['MinimumInnerClearanceMeters']:.4f} / {e['MinimumOuterClearanceMeters']:.4f}"])
old_table=table(['Geometria','plan','bez koła s','inner gap m','wykonalny'],[[r['Geometry'],r['Evaluation']['Plan']['Name'],f"{r['Evaluation']['FlyingSeconds']:.6f}",f"{r['Evaluation']['MinimumInnerClearanceMeters']:.5f}",r['Evaluation']['Feasible']] for r in historical])
perf_table=table(['Geometria','projekcja mediana µs','alokacje / 10000'],[['baseline',f"{statistics.median(p['MicrosecondsPerProjection'] for p in performance[0]['Projection']):.3f}",0]]+[[s['Name'],f"{statistics.median(p['MicrosecondsPerProjection'] for p in s['Projection']):.3f}",0] for s in stats])
world_table=table(['3 okrążenia inner','mediana ms','solver / force','oceny granicy','commit koła'],[[r['Geometry'],f"{statistics.median(x['Milliseconds'] for x in r['PlainWorld']):.3f}",r['PlainWorld'][0]['SolverCalls'],r['PlainWorld'][0]['RearWheelBoundaryEvaluations'],by[r['Geometry'],'inner']['Work']['RearWheelCommits']] for r in performance])
validation={'TargetedRelease':counts,'NodeTests':{'Passed':9,'Failed':0},'HistoryManifestsUnchanged':history,'Baseline100Exact':baseline_matches,
    'MainWorlds':35,'HistoricalWorlds':6,'RepeatabilityWorlds':7,'RejectedDetailWorlds':3,'CostWorldsIncludingWarmup':8,
    'MaximumEnergyResidualJoules':max(abs(r['Energy']['BalanceResidualJoules']) for r in rows+historical),
    'MaximumProjectionErrorMeters':max(r['MaximumProjectionErrorMeters'] for r in stats),'FeasibleMainPlans':sum(r['Evaluation']['Feasible'] for r in rows),
    'CI':'Standard Draft Windows CI on final HEAD; exact link and status in PR/final delivery.'}
write('validation',validation)

report=f'''# Track Geometry Variants (#101)

**A — poprawny, ograniczony eksperyment geometrii; brak empirycznego potwierdzenia realizmu toru.** Baza #100 `{load('freeze')['BaseHead']}`. Geometria wpływa na przewagę inner, ale nie wykazuje, że prawdziwy tor powinien dawać inne proporcje. Najlepszym bezpiecznym kandydatem w głównej próbie jest **Egg Mild**. Nie zmieniono domyślnego toru.

## Zakres i zamrożenie

[freeze.json](track-geometry-variants/freeze.json) powstał przed pierwszym przejazdem. 7 geometrii × 5 planów = **35 światów**: neutralny zawodnik/setup, jednolity Grip1, wilgotność0,5, bez kolein, 3 okrążenia, stały krok docelowy10ms. Start: reference0, offset5m, v18m/s, yaw zgodny z tangent; plany aktywują target na t0. Cel to Lap2+Lap3, bez nagrody za linię. Oba flagi #100 ON. Bez optymalizacji planów, zmiany gazu, AI, umiejętności, zużycia, opony, ω, yaw, masy lub wymiarów. Produkcyjne `src/CoreSim` pozostaje nietknięte.

Plany `inner`=1/1/1/1, `middle-tighten`=5/3/1/5, `wide-tighten`=9,5/7/2/9,5 to te same plany #99–#100. Nowe `speedway-wide-entry`=9/9/2/7 (apex komenda50%, exit85%) i `speedway-open-exit`=9/6/1/9,5 (40%/65%) są jawnymi hipotezami, nie rekonstrukcją jazdy zawodnika. Wszystkie przygotowują entry15m przed łukiem; drive1. Komenda apex i rzeczywisty offset przy geometrycznym środku nie są tym samym.

## Jawna geometria

Baseline zachowuje dokładny dziewięciosegmentowy `TrackMetricEmbedding`: proste35+27m przez metę i62m, Rref31m, po3×float(π/3) na łuk; lap318,778749943m. Fizyczny kerb leży offset−1m, banda11m na prostej i15,6m w łuku. Dotychczasowe skoki szerokości4,6m pozostają w baseline.

Nowe łuki są krzywymi parametryzowanymi długością referencyjną. Dla t=s/L:

`g(t)=r(t)·(1+c cos(2πt)); I=∫₀¹g(t)dt; κ(t)=πg(t)/(L I); θ(t)=∫κ ds; P'= (cosθ,sinθ)`.

`r=S(min(t,1−t)/0,05)` do1, `S(z)=10z³−15z⁴+6z⁵`. Krzywizna i jej pochodna przy łączeniu ze straight są zerowe; środek łuku jest coraz bardziej płaski, ramiona ciaśniejsze. Żaden wariant nie ma spiczastego środka. Całka Simpsonem na4096 interwałach/łuk; druga połowa jest lustrzana. Piątego stopnia Hermite łączy pozycję, tangent i κ w węzłach. To C2 w XY, z bardzo małym błędem parametryzacji; nie normalizujemy ciał ani ich prędkości.

{geometry_table}

Łączna długość łuków i oba straight62m są zachowane; zmienia się poprzeczny rozstaw prostych. Mild ściska zasięg x o3,56%, Medium8,77%, Strong14,31%. Asymmetric zachowuje wspólny rozstaw y64,356m; łuki mają100,020 i94,758m (inne długości są konieczne dla domknięcia). To kontrolowane kształty modelowe, bez pomiarów geodezyjnych Motoareny.

Kontrole: `smooth-width-stadium` zachowuje kołowy kształt z nową reprezentacją i samymi gładkimi krawędziami (odziedziczony skok krzywizny circle/straight jest tu zamierzony). `round-smooth` dodaje rampę κ, c=0. Oddzielają wpływ szerokości i wygładzenia od dalszego ściśnięcia.

Szerokość we wszystkich nowych geometriach wynosi12m na prostej i16,6m w środku łuku, z C2 taperem przez pierwsze/ostatnie10m łuku. Bez zwężenia poniżej12m, bez rozszerzenia do nowych targetów. Maksymalny dopuszczony target nadal9,5m. Ograniczenie pełnej szerokości planów z #99 nadal istnieje. Przy jednolitej nawierzchni zachowano próbkowanie i bazowy budżet sił #100: jego `v_safe²/R` jest algebraicznie niezależne od R; stały promień w tej kalibracji nie jest używany jako krzywizna drogi. Heading, preview, reference speed i metry planu pochodzą z nowej krzywej. Nie deklarujemy obsługi AI ani porównania niejednolitej nawierzchni na tych kształtach.

![Kształty w tej samej skali](track-geometry-variants/shapes.svg)

## Wynik wszystkich planów

{timing}

† Pełny plan odrzucony geometrycznie; czas pozostawiony diagnostycznie. Nie używamy go w rankingu wykonalnych. Wszystkie35 światów ukończyły3 okrążenia, **32/35** przeszły kryteria. Pozycje poza torem są zachowane w danych, bez korekty i bez dodanej bandy w solverze.

{table(['Geometria','middle−inner s','wide−inner s','ranking wykonalnych'],deltas)}

Mild zmniejsza stratę middle0,310608→0,091231s (**70,6%**), a wide1,366302→0,949231s (**30,5%**). Inner sam zwalnia0,404814s, middle0,185436s, wide przyspiesza0,012258s. To częściowo utrudnienie jazdy przy kredzie, a nie duży zysk szerokiej. Kontrola taperu zmienia czasy o kilka mikrosekund; samo C2 przejście zmniejsza middle−inner do0,243070s, ściśnięcie Mild dalej do0,091231s. Główne zmiany nie pochodzą od taperu.

W Medium/Strong/Asymmetric middle jest najszybszym **wykonalnym** planem, lecz inner odrzucono. Nie traktujemy tego jako dowodu, że geometria uczciwie „odwróciła” przewagę bez kosztu bezpieczeństwa.

Nie znika matematyczny koszt stałego offsetu: dla wypukłej zamkniętej krzywej `dl=(1+κd)ds`, więc po pełnym obrocie dodatkowa droga wynosi `2πd`, niezależnie od tego, czy łuk jest kołem. Zmiana kształtu zmienia rozkład wymaganych sił/prędkości i koszt zmiennego offsetu, nie usuwa samego kosztu jazdy na zewnątrz.

## Prędkości, rzeczywiste trajektorie i wyjście

{table(['Geometria/plan','d dwóch lotnych m','vmin łuku m/s','entry / middle / exit m/s','vout exit m/s','Δoffset pierwsze12m','min gap inner/outer m'],speed_rows)}

Entry/middle/exit to średnie z czterech łuków Lap2–3, middle oznacza geometryczne50% łuku. Exit to pierwsza rzeczywiście zaakceptowana prosta; wynik nie jest przenoszony na hipotetyczny stan w dokładnym s=end. Długość lotnej trajektorii pochodzi z wykonanej drogi i interpolacji przekroczeń. Prześwity oraz max slide/κ obejmują cały przejazd3lap, także wejście w stan ustalony. κ w [results.csv](track-geometry-variants/results.csv) jest maksymalnym chwilowym κ przed zaakceptowanym interwałem, nie κ punktu środkowego z innej tabeli #100. Wszystkie offsety straight/entry/apex/exit, minima, slide i κ są w CSV; [corners.csv](track-geometry-variants/corners.csv) zachowuje każdy łuk osobno.

Mild: szeroki exit zyskuje nad inner1,807m/s wobec1,565m/s baseline; następna prosta1,246 wobec1,085m/s. Middle exit zyskuje1,021 wobec0,820m/s. Jednocześnie absolutne exit speed nie rosną: inner19,158→18,802m/s, wide20,722→20,609m/s. Rzeczywista droga wide skraca się711,938→709,382m; inner wydłuża645,118→646,497m. Względny zysk szerszych planów jest mierzalny, bez deklaracji poprawności empirycznej.

Na wyjściu Mild vout inner maleje1,758→1,352m/s, poszerzenie0,358→0,228m; wide4,509→4,256m/s i1,918→1,839m. Gładkie κ nie tworzy nagłego zaniku wymagania skrętu jak circle/straight. Nadal występuje wczesne wychodzenie na zewnątrz i późniejsze prostowanie. Nowe plany „speedway” nie wygrały; wide-entry jest najwolniejszy. Nazwa planu nie zastępuje rzeczywiście osiągniętego apexu.

![Prędkość rzeczywistych przejazdów](track-geometry-variants/speed.svg)
![Rozkład krzywizny](track-geometry-variants/curvature.svg)

## Geometria bezpieczeństwa i sanity bez wheelspin

Audyt liczy odległość **całej chassis i handlebar capsule** od obu krawędzi, z ich rzeczywistym yaw. Polilinia brzegu ma odstęp≤5cm; dla C2 kształtów ograniczenie |edge''|<2/m daje sag<0,625mm. Odejmujemy0,7mm oraz istniejące0,1mm zapasu numerycznego. Kołowa kontrola ma skok szybkości parametru offsetu przy łączeniu; jej geometryczny sag także sprawdzono niezależnie. Największa zmierzona odległość brzegu od cięciwy pozostaje poniżej0,7mm. BVH przyspiesza obliczenie odległości; nie zastępuje testu samym offsetem środka.

Medium/inner: minimum **+0,002927m**, narusza wymagane0,05m, bez ujemnych próbek. Strong/inner: **−0,102027m**,156 ujemnych próbek. Asymmetric/inner: **−0,111026m**,96 ujemnych próbek. Najgorsze stany są na pierwszym okrążeniu, na wejściu w drugi łuk (t10,53/10,61/10,48s); także transient od wspólnego offsetu5m jest częścią wykonalności. [rejected-details.json](track-geometry-variants/rejected-details.json) zawiera stan, obrysową ocenę i sąsiednie kroki. Nie rozszerzono toru ani nie zmieniono targetów po pomiarze.

Audyt obejmuje każdy zaakceptowany stan10ms oraz podziały istniejącego runnera. To nie jest nowa certyfikacja ciągłego CCD pomiędzy stanami; zapas5cm i krok≤0,24m są jawne. Brak dowodu, że na tym torze nie wystąpi błąd przy dowolnej prędkości lub planie.

Zamrożona reguła wyboru dopuściła tylko Mild spośród4 egg z wszystkimi3 bazowymi planami wykonalnymi. Wykonano zatem krótki sanity tylko baseline+Mild, po3 plany, bez wheelspin:

{old_table}

Bez koła surowa strata middle do inner także maleje0,262932→0,096144s, więc efekt czasu nie jest wyłącznie wheelspin. **Jednak Mild/inner przecina kerb o9,691mm** w historycznym modelu. Nie nazywamy tego bezpieczną poprawą ani niezależnością od opony. Mild z modelem #100 pozostaje kandydatem do dalszej pracy; oba modele nie mają identycznego marginesu.

## Koszt i deterministyczność

Projekcja używa statycznego BVH nad4096 kawałkami/łuk, następnie najwyżej5 kroków Newtona na odwiedzonym kawałku i porównania z prostymi. Bez linearnego przeszukiwania wszystkich węzłów, przebudowy drzewa, RNG, zegara domenowego lub alokacji w zapytaniu. Podziały siatki nie tworzą nowych kroków fizyki; istniejący scheduler nadal widzi tylko koniec logicznego straight/turn.

{perf_table}

5 powtórzeń po warmup, po10000 zapytań, ten sam Release; czasy hosta są diagnostyką. BVH ma zauważalny koszt≈2,8–3,0× native inverse, nie ukrywamy go.

{world_table}

3 powtórzenia pełnego, nieobserwowanego3lap inner po warmup: baseline28,728ms→Mild38,847ms (**+35,2%**); koszt obejmuje utworzenie geometrii i świata. Solver4815→4869 (**+1,12%**), tyle samo pojedynczych force/commit. Zmiana wynika z czasu jazdy i istniejących granic, nie z tysięcy dodatkowych korekt kontaktu. Offline audyt geometrii nie działa w zwykłym ticku. Każda geometria ma dokładnie zgodny fingerprint zwykłego i obserwowanego wykonania; w próbach kosztu wszystkie powtórzenia także zgodne.

Wykonano35 głównych światów,6 historycznych,7 prostych powtórzeń,3 celowane powtórzenia odrzuceń i8 prób kosztu z warmup. Nie uruchamiano szerokiego benchmarku ani ponownie głównej macierzy. Eksport geometrii ma camelCase wymagane przez viewer; korekta samej serializacji nie liczyła fizyki ponownie.

## Walidacja i odtwarzanie

Release całego rozwiązania z warnings-as-errors:0 ostrzeżeń/błędów. **32/32** celowanych testów (8 nowych geometrii + istniejące #99/#100/sterowanie/replay), **9/9 Node**. Testy obejmują domknięcie, długość, finity, normalne, roundtrip reference/offset, porównanie projekcji z niezależną gęstą próbką także daleko od toru, C2, krawędzie, brak alokacji i powtórzenie świata. Max roundtrip reference={validation['MaximumProjectionErrorMeters']:.9f}m. Max |whole-system ΔE−W|={validation['MaximumEnergyResidualJoules']:.6f}J. Solver=wheel commit we wszystkich nowych głównych przejazdach, bez podwójnego napędu.

6 fingerprintów i czasów baseline (#100 normalgrip,3 plany×2 modele) jest zgodnych **bitowo**. Native geometry.json jest identyczny bajtowo. Manifesty #95/#97/#98/#99/#100 pozostają bez zmian: {history}. [validation.json](track-geometry-variants/validation.json), [TRX](track-geometry-variants/targeted-release.trx), [geometry-validation.json](track-geometry-variants/geometry-validation.json), [performance.json](track-geometry-variants/performance.json). Standardowe Draft Windows CI sprawdza końcowy HEAD; link/status w PR i dostarczeniu. Bez merge.

Viewer dostał wyłącznie wybór zapisanych #101 przejazdów i ładowanie ich własnego `*-geometry.json`. Stare replaye wracają do starej geometrii. CSV≈0,1s, bez projekcji/zmiany yaw dla wyglądu; pozycje każdej zapisanej próbki są testowane. Dostępne inner i wide dla baseline i czterech egg, także odrzucone. [Dane](track-geometry-variants/) zawierają pełne tabele i lekki ślad drugiego okrążenia. `track-geometry DIR freeze`, potem `measure`, `verify`; `export` tylko odtwarza pliki geometrii. Helper `track-geometry-report.py` nie liczy ruchu ani nie stroi.

## Sześć odpowiedzi i jeden następny PR

1. **Czy stadium nadmiernie premiuje inner?** W tym modelu kształt stanowi część przewagi; brak danych prawdziwych przejazdów, by rozstrzygnąć słowo „nadmiernie”. Nie usuniemy geometrią kosztu2πd stałej zewnętrznej linii.
2. **Który ogranicza przewagę?** Mild jest najlepszym bezpiecznym porównaniem wszystkich3 bazowych planów przy #100. Strong najbardziej zmniejsza surowe różnice, ale inner jest niewykonalny; nie jest zwycięzcą porównania bezpieczeństwa.
3. **Realistyczniejszy charakter?** Mild jest najmocniej uzasadnionym kandydatem dzięki C2 i mniejszemu wyrzutowi na prostą. Realizm empiryczny pozostaje nierozstrzygnięty. Medium ma ciekawy kompromis z ciaśniejszymi ramionami; Strong jest zbyt agresywny dla zamrożonego inner i jego regulatora, mimo gładkiej geometrii. Asymmetry nie daje tu dodatkowej korzyści bezpieczeństwa. Sam taper jest za słaby.
4. **Odzysk na entry/exit?** Tak względnie: Mild podnosi przewagę exit/straight speed middle i wide nad inner; absolutne prędkości nie rosną. Wide nadal przegrywa0,949s i pozostaje dłuższy o62,885m na2lap.
5. **Do dalszej pracy?** Mild nadaje się do dalszego eksperymentu, jeszcze nie do zastąpienia Motoareny: brak geodezji, mapy gripu, pełnej szerokości planów i zgodnego marginesu historycznej opony. Nie deklarujemy rozstrzygnięcia AI/czteroosobowej walki.
6. **Jeden następny PR:** walidacja **Egg Mild na podstawie rzeczywistych danych geometrii i trajektorii jednego toru**, z zamrożonymi równaniami #100. Porównać pomierzone współrzędne/krawędzie oraz entry/apex/exit i sprawdzić margines kerbu, zanim zaczniemy kalibrować wybór linii. Nie uruchamiać kolejnego szerokiego przeszukiwania planów pod założonego zwycięzcę.
'''
(ROOT/'docs/experiments/track-geometry-variants.md').write_text(report,encoding='utf8')
manifest={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(DATA.iterdir()) if f.is_file() and f.name!='manifest.json'}
write('manifest',manifest)
print(json.dumps({'main':len(rows),'historical':len(historical),'feasible':validation['FeasibleMainPlans'],'tests':counts['passed'],'files':len(manifest),'unchanged':history},ensure_ascii=False))
