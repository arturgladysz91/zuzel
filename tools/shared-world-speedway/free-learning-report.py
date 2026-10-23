"""Read-only native evidence aggregation and SVG figures. Never invokes simulation."""
import csv,difflib,gzip,hashlib,html,json,math,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'docs/experiments/free-racing-learning'
BASE='e23261afe1205c71a8db3733b6553485f4de29f5'
def load(name):
    p=DATA/name
    return json.loads(gzip.decompress(p.read_bytes()) if p.suffix=='.gz' else p.read_bytes())
def save(name,value):(DATA/name).write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def sha(b):return hashlib.sha256(b).hexdigest().upper()
def source_guard():
    sources={};old=json.loads((ROOT/'docs/experiments/free-racing/protocol.json').read_bytes())['Sources']
    for name in ['WorldRunner.cs','WorldModel.cs','Program.cs','FreeRacingModel.cs']:
        p='tools/shared-world-speedway/'+name
        original=subprocess.check_output(['git','show',BASE+':'+p]).decode().replace('\r\n','\n')
        current=(ROOT/p).read_bytes().replace(b'\r\n',b'\n').decode()
        a,b=original.splitlines(keepends=True),current.splitlines(keepends=True)
        edits=[{'Before':''.join(a[i:j]),'After':''.join(b[k:l])} for tag,i,j,k,l in difflib.SequenceMatcher(None,a,b,autojunk=False).get_opcodes() if tag!='equal']
        restored=current
        for e in reversed(edits):
            assert e['After'] and restored.count(e['After'])==1,(name,e)
            restored=restored.replace(e['After'],e['Before'],1)
        assert restored==original
        sources[name]={'BaseSha256':sha(original.encode()),'ApprovedSha256':sha(current.encode()),'BaseRawSha256':old.get(name),
            'ApprovedRawSha256':sha((ROOT/p).read_bytes()),'Edits':edits}
    save('source-extensions.json',{'BaseHead':BASE,'Sources':sources,'Rule':'Exact reversible integration/callback extensions; old OFF code and evidence remain guarded.'})
def root_cause():
    old=json.loads(gzip.decompress((ROOT/'docs/experiments/free-racing/preflight-train-16-3.json.gz').read_bytes()))
    first=old['Decisions'][0];rows=[]
    for c in first['Candidates']:
        overspeed=2*max(0,c['ExitSpeed']-c['GeometrySpeedEnvelope'])**2
        rows.append({'SteeringDegrees':c['Action']['SteeringRadians']*180/math.pi,'Drive':c['Action']['Drive'],
            'Progress':c['ProgressMeters'],'OriginalScore':c['Score'],'OverspeedCost':overspeed,'ScoreWithoutOverspeedTerm':c['Score']-overspeed})
    save('128-root-cause.json',{'NativeWorlds':0,'OriginalChosen':first['Action'],
        'CounterfactualWithoutOverspeedTerm':min(rows,key=lambda r:r['ScoreWithoutOverspeedTerm']),
        'Conclusion':'The initial right request is selected by the finite geometric overspeed cost despite slightly less predicted progress. Without that term the narrow old set selects +0.034 degrees. This establishes a local scoring cause, not a complete causal decomposition of the later -35degree saturation. Physical signs are correct.', 'Candidates':rows})
def svg(name,title,content,w=1000,h=520):
    (DATA/name).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><rect width="100%" height="100%" fill="#101e2a"/><g font-family="Segoe UI,Arial" fill="#dbe8ef"><text x="30" y="35" font-size="22">{html.escape(title)}</text>{content}</g></svg>',encoding='utf-8')
def line(points,color,width=2):return f'<polyline points="'+ ' '.join(f'{x:.2f},{y:.2f}' for x,y in points)+f'" fill="none" stroke="{color}" stroke-width="{width}"/>'
def figures(generations,overlays):
    colors=['#73dfdf','#ffd27b','#d99fe3','#8fc8ff','#b4d985','#ff9c86','#e8eff5'];total=max(g['Episodes'] for g in generations)
    charts=[('Maksymalny legalny postęp [m]',lambda g:g['MaximumLegalProgress'],160),('Legalne ukończenia [%]',lambda g:g['Completed']*10,100),
        ('Najlepszy ukończony czas generacji [s]',lambda g:min((max(r['GateSeconds'] for r in c['Results']) for c in g['Candidates'] if all(r['Completed'] for r in c['Results'])),default=None),20),
        ('Wyjazdy poza granice /10',lambda g:g['Exits'],10)]
    body=''
    for i,(label,value,upper) in enumerate(charts):
        x0=70+(i%2)*480;y0=85+(i//2)*210
        body+=f'<text x="{x0}" y="{y0}" font-size="14">{label}</text><path d="M{x0},{y0+155}h410 M{x0},{y0+15}v140" stroke="#48677a" fill="none"/>'
        for level in range(1,5):
            points=[(x0+g['Episodes']/total*410,y0+155-value(g)/upper*135) for g in generations if g['Level']==level and value(g) is not None]
            if points:body+=line(points,colors[level-1])+''.join(f'<circle cx="{x}" cy="{y}" r="3" fill="{colors[level-1]}"/>' for x,y in points)
        body+=f'<text x="{x0+330}" y="{y0+177}" font-size="12">{total} epizodów</text>'
    body+='<text x="45" y="505" font-size="13">Kolory: poziom1 turkus · poziom2 złoty · poziom3 różowy · poziom4 niebieski. Czasy tylko ukończonych prób.</text>'
    svg('learning-progress.svg','Uczenie z rzeczywistych epizodów Box2D',body)
    names=load('protocol.json')['Names'];ranges=list(zip(load('protocol.json')['Lower'],load('protocol.json')['Upper']))
    body=''
    for i,name in enumerate(names):
        x0=55+(i%2)*480;y0=70+(i//2)*105;lo,hi=ranges[i]
        body+=f'<text x="{x0}" y="{y0}" font-size="13">{name} [{lo}, {hi}]</text>'
        body+=line([(x0+g['Episodes']/total*410,y0+75-(g['BestPolicy'][name]-lo)/(hi-lo)*60) for g in generations],colors[i%len(colors)])
    svg('policy-parameters.svg','Parametry kolejnych najlepszych polityk treningowych',body,h=520)
    geometry=json.loads((ROOT/'docs/experiments/progressive-bend-width/progressive-b-geometry.json').read_bytes())
    points=[p[k] for s in geometry['segments'] for p in s['sections'] for k in ['inner','outer']];xmin=min(p['x'] for p in points);xmax=max(p['x'] for p in points);ymin=min(p['y'] for p in points);ymax=max(p['y'] for p in points)
    scale=min(900/(xmax-xmin),330/(ymax-ymin));tx=lambda x:50+(x-xmin)*scale;ty=lambda y:420-(y-ymin)*scale
    body='<text x="30" y="62" font-size="13">Wspólny stan: 9m/s, offset4m, s22m. Ślady wyłącznie natywne; brak idealnej linii.</text>'
    for key,color in [('inner','#d6d9ce'),('outer','#b29878')]:body+=line([(tx(p[key]['x']),ty(p[key]['y'])) for s in geometry['segments'] for p in s['sections']],color,1)
    for i,(name,run) in enumerate(overlays.items()):
        body+=line([(tx(s['Input']['Rider']['Position']['X']),ty(s['Input']['Rider']['Position']['Y'])) for s in run['Samples']],colors[i%len(colors)],2)
        body+=f'<text x="{30+(i%3)*325}" y="{465+(i//3)*22}" fill="{colors[i%len(colors)]}" font-size="12">{html.escape(name)}: {"PASS" if run["Metrics"]["Completed"] else "DNF"}</text>'
    svg('native-trajectories.svg','Porównanie polityk na identycznym stanie wejściowym',body)
    body=''
    for i,(label,key,upper) in enumerate([('Prędkość wejścia','EntrySpeed',25),('Minimum prędkości','MinimumSpeed',25),('Prędkość wyjścia','ExitSpeed',25)]):
        x0=60+(i%2)*480;y0=95+(i//2)*210;body+=f'<text x="{x0}" y="{y0}">{label} [m/s]</text>'
        for level in range(1,5):
            pts=[(x0+g['Episodes']/total*410,y0+150-sum(r[key] for c in g['Candidates'] for r in c['Results'] if r[key] is not None)/max(1,sum(r[key] is not None for c in g['Candidates'] for r in c['Results']))/upper*135) for g in generations if g['Level']==level and any(r[key] is not None for c in g['Candidates'] for r in c['Results'])]
            if pts:body+=line(pts,colors[level-1])
    svg('corner-speeds.svg','Prędkości w kolejnych generacjach — rzeczywiste pomiary',body)
    body='';causes=sorted({c for g in generations for c in g['Causes']});palette={c:colors[i%len(colors)] for i,c in enumerate(causes)}
    for g in generations:
        x=60+g['Generation']/len(generations)*880;y=420
        for cause in causes:
            height=g['Causes'].get(cause,0)*30;y-=height
            body+=f'<rect x="{x:.1f}" y="{y:.1f}" width="{max(1,860/len(generations)):.1f}" height="{height}" fill="{palette[cause]}"/>'
    for i,cause in enumerate(causes):body+=f'<text x="{40+(i%4)*240}" y="{460+(i//4)*22}" fill="{palette[cause]}">{html.escape(cause)}</text>'
    body+='<text x="50" y="80">Każdy słupek = 10 ocen w generacji; osobny abort diagnostyczny pozostaje w attempts.csv.</text>'
    svg('termination-causes.svg','Przyczyny zakończenia rzeczywistych prób',body)
def report(a,generations,overlays):
    selected=[overlays['overlay-'+n] for n in ['initial','after-100','after-200','final']]
    def number(v):return '—' if v is None else f'{v:.3f}'
    def row(label,values):return '| '+str(label)+' | '+' | '.join(str(x) for x in values)+' |\n'
    table='| Metryka — wspólny stan 9m/s, offset4m | Początkowa | Po100 | Po200 | Końcowa |\n|---|---:|---:|---:|---:|\n'
    table+=row('Legalne ukończenia',[int(r['Metrics']['Completed']) for r in selected])
    for label,key in [('Legalny postęp [m]','LegalProgress'),('Czas pierwszego łuku +3m [s]','GateSeconds'),('Maksymalne β [°]','MaximumBetaDegrees'),('Prędkość wejścia [m/s]','EntrySpeed')]:table+=row(label,[number(r['Metrics'][key]) for r in selected])
    table+=row('Minimum prędkości w łuku [m/s]',[number(min((s['Input']['Rider']['Speed'] for s in r['Samples'] if s['Input']['Rider']['Road']['IsTurn']),default=None)) for r in selected])
    for label,key in [('Prędkość wyjścia na bramce [m/s]','ExitSpeed'),('Średni gaz [0–1]','MeanDrive'),('Pełny gaz — udział czasu [0–1]','FullDriveFraction'),('Oscylacje poprzeczne ≥1m','LargeTrajectoryOscillations'),('Droga XY [m]','DistanceMeters'),('Minimalny prześwit swept [m]','MinimumClearanceMeters'),('CPU epizodu [s]','CpuSeconds'),('Oceny kontaktu epizodu','PhysicsEvaluations')]:table+=row(label,[number(r['Metrics'][key]) for r in selected])
    validation='| Stały przypadek | Początkowa | Po100 | Po200 | Końcowa |\n|---|---:|---:|---:|---:|\n'
    for i,scenario in enumerate(load('protocol.json')['Validation']):
        rs=[a['InitialValidation'][i]]+[load('after-'+str(n)+'-'+scenario['Name']+'-metrics.json') for n in [100,200]]+[a['FinalValidation'][i]]
        validation+=row(scenario['Name'],[number(r['GateSeconds'])+'s' if r['Completed'] else 'DNF' for r in rs])
    gen_table='| Generacja | Poziom | Licznik prób | Ukończenia /10 | Max / średni legalny postęp [m] | Najlepszy czas 2/2 [s] | Przyczyny |\n|---:|---:|---:|---:|---:|---:|---|\n'
    for g in generations:
        best=min((max(r['GateSeconds'] for r in c['Results']) for c in g['Candidates'] if all(r['Completed'] for r in c['Results'])),default=None)
        gen_table+=row(g['Generation'],[g['Level'],g['Episodes'],g['Completed'],f'{g["MaximumLegalProgress"]:.2f} / {g["AverageLegalProgress"]:.2f}',number(best),', '.join(f'{k}: {v}' for k,v in g['Causes'].items())])
    final=selected[-1];initial=selected[0];policies='| Preferencja | Początkowa | Końcowa |\n|---|---:|---:|\n'
    for name in load('protocol.json')['Names']:policies+=row(name,[number(initial['Policy'][name]),number(final['Policy'][name])])
    def offsets(run):
        vs=[s['Input']['Rider'] for s in run['Samples'] if s['Input']['Rider']['Road']['IsTurn']]
        return f'{min(v["Road"]["OffsetMeters"] for v in vs):.2f}–{max(v["Road"]["OffsetMeters"] for v in vs):.2f}m'
    baseline=load('matched-124.json');post=load('post-training-prediction.json')
    (DATA/'README.md').write_text(f'''# Free Racing Learning #129 — {a['Grade']}

Status **PROVISIONAL**, narzędzie badawcze domyślnie OFF. Jeden zawodnik, istniejąca natywna fizyka Box2D. Wynik dotyczy tego modelu oraz zbadanych stanów, nie dowodzi optymalności ani realizmu skalibrowanego motocykla.

## 1. PR i baza

[Draft PR #129](https://github.com/arturgladysz91/zuzel/pull/129), bez merge. Dokładny HEAD jest w opisie PR. Gałąź `experiment/free-racing-learning-129` zaczyna się dokładnie od `e23261afe1205c71a8db3733b6553485f4de29f5` (#128). Stare wyniki #128 i wcześniejszych audytów pozostają zachowane.

## 2. Kontrola fizycznego skrętu przed treningiem

Pięć natywnych światów: 8m/s, offset5m, s4m, gaz1; do0,24s kierownica0, potem impuls0/±5/±15° do0,5s. [Wyniki](physics.json), [protokół](physics-protocol.json), pełne siły obu opon i momenty w `impulse-*.json`.

Globalnie +X to kierunek prostej, +Y lewo/do wnętrza; dodatni moment i kąt są CCW. Dla+5°: Δψ=0,12638rad, Δχ=0,07141rad, Δβ=0,05497rad, ΔY=+0,08286m; dla+15° odpowiednio0,19240/0,11417/0,07823rad i+0,12070m. Ujemne impulsy dają lustrzaną reakcję. Dodatni skręt prowadzi w lewo, lecz ψ, χ i β nie są tym samym. Geometria lewego łuku zwiększa heading. Nie znaleziono błędu znaków i nie zmieniono sił ani momentów.

## 3. Przyczyna pierwszej decyzji #128

[Przeliczenie zapisanych ocen bez nowego świata](128-root-cause.json) potwierdza lokalną przyczynę: koszt przekroczenia geometrycznej obwiedni prędkości przeważył niewielką przewagę postępu i wybrał−0,666°. Po odjęciu tego jednego członu, na tym samym wąskim zbiorze wygrywa+0,034°. Nie jest to pełny dowód przyczyny późniejszego nasycenia−35°. Zbiór±0,7° wokół nominalnej decyzji ograniczał możliwość ucieczki z błędnego wyboru.

## 4. Rzeczywisty wybór działań

18–23 odrębne pary (maksimum25 w teście API): kierownica0/±0,5/±1,5/±5/±15°, wykonany i aktywnie żądany kąt, gaz0 lub wyuczona wartość oraz zachowanie aktywnej pary. Co0,2s każdy kandydat przechodzi predyktor0,6s. Wszystko trafia do kolejki z pełną reakcją0,24s i oryginalnego aktuatora±35°,90°/s. Bezpieczne w horyzoncie, niepewne i niebezpieczne są odrębnymi klasami; bezpieczeństwo ma pierwszeństwo przed kosztem. `NoSafeContinuation` jawnie oznacza brak bezpiecznego kandydata, nie sukces decyzji. To lokalna ocena, nie gwarancja całego przejazdu.

## 5. Jak CEM uczy się z niepowodzeń

Pięć polityk na generację × dwa identyczne dla nich stany. Populacja: zachowany najlepszy kandydat, trzy mutacje Gaussa oraz jeden szeroki kandydat z ograniczonego rozkładu tanh(normal). Dwie elity aktualizują średnią0,4starej+0,6nowej, z dolnym ograniczeniem odchylenia15% zakresu. Seed1292026, osiem preferencji. W protokole słowo „uniform” przy imigrancie jest nieprecyzyjne; kod i zapisane wartości używają tanh(normal).

Porządek: liczba legalnych ukończeń; przy2/2 najgorszy czas bramki; następnie minimalny i średni legalny postęp, zbliżanie do granic i odzyskiwanie prześwitu na metr. Sam czas przetrwania nie daje nagrody. Krok powodujący wyjazd nie zwiększa legalnego postępu; reverse, oszustwo projekcją, brak postępu i naruszenia obrysu kończą epizod. Nieukończona polityka nie wygrywa z ukończoną. W generacji2 było1/10 ukończeń i żadnej pary2/2, a elity mimo tego zmieniły rozkład; generacje3/4 osiągnęły7/10 i8/10.

## 6. Faktyczna liczba prób i budżet

**{a['TrainingEpisodes']} natywnych prób treningowych**, w tym jeden zachowany abort kontroli pomiarowej; {a['PhysicsWorlds']} światów fizyki/probe i {a['ValidationWorlds']} walidacji (w tym powtórzenia, replay i#124). Razem **{a['TotalNativeWorlds']} światów**. [Każda rezerwacja i koszt](attempts.csv), [budżet](budget.json).

Przed treningiem: limity200/1000/opcjonalnie2000, CPU2400/7200/12000s, kontakty50/160/300mln, fizyka≤8, walidacja≤36, epizod≤90CPU s, generacja≤150CPU s. Po200 rozszerzono na podstawie natywnych ukończeń, nie samej liczby prób. Zakończenie: **{a['Stop']}**. Fazy2000 nie uruchomiono. Rezerwacja następuje przed utworzeniem świata; krótka prognoza nie jest epizodem.

## 7. Wyniki kolejnych generacji

Kolory na wykresach rozdzielają poziomy; zmiana zadania powoduje skok metryk. Licznik epizodów od generacji5 obejmuje także zachowany abort pomiarowy, dlatego nie zawsze jest wielokrotnością10. Pełne populacje, średnie, odchylenia, przyczyny i koszty: [generations.json](generations.json).

{gen_table}
![Uczenie](learning-progress.svg)
![Przyczyny](termination-causes.svg)

## 8. Prosta

Poziom1: dwa różne stany6/8m/s, offset3/6m, s4m i drobne odchylenia. Obie pierwsze generacje ukończyły10/10 prób. Początkowy ogólny regulator z szerokim wyborem działań już kontrolował tę prostą; nie przypisujemy CEM całej tej zdolności. CEM poprawił czas przed przejściem na łuk. Nie ma nagrody za środek ani konkretny offset.

## 9. Pierwszy bezpieczny łuk

Poziom 2: 8/10m/s, poziom 3: 12/14m/s, poziom 4: 16/18m/s. Przejście wymaga dwóch kolejnych generacji ≥6/10 oraz najlepszej pary 2/2. Bramka to cały obrys za stałą płaszczyzną 3m po pierwszym łuku, następnie 1s legalnej jazdy. Zachowano pełny pierwszy bezpieczny przejazd i końcową politykę zweryfikowaną tylko przez natywne ukończenia. Pierwsza istotna poprawa na łuku i pierwszy bezpieczny łuk to ta sama polityka 19 z generacji 3; dwa replaye są osobno policzonymi odtworzeniami.

## 10. Prędkość i czas

Tabela poniżej porównuje identyczny, ustalony przed treningiem stan:9m/s, offset4m, s22m, lateral+0,12m/s, heading+0,025rad, yaw+0,02rad/s. Prędkość początkowa nie jest prędkością wejścia: silnik rozpędza motocykl przed łukiem. Minimum łuku wyliczono z próbek `Road.IsTurn`; `MinimumSpeed` w surowych metrykach obejmuje cały epizod. CPU zależy od długości epizodu i komputera; DNF nie ma czasu bramki ani prędkości wyjścia.

{table}
![Prędkości](corner-speeds.svg)

## 11. Trajektoria i wyuczone preferencje

Nie ma listy XY, target-offsetu, komend przypisanych do czasu ani fabryki przygotowanej ścieżki w sterowniku. Geometria daje kierunek i promień oraz granice; osiem preferencji wybiera działania z bieżącego stanu. Twarda ocena ryzyka i ogólna struktura kosztu są zaprogramowanym algorytmem, a nie wynikiem uczenia. CEM uczy wartości preferencji z rzeczywistych wyników Box2D.

Na wspólnym przypadku obserwowany offset w łuku zmienił się z{offsets(initial)} (DNF) do{offsets(final)}. To wynik sterowania, nie zadany korytarz. Dowód korzystniejszej trajektorii jest lokalny: ukończenie i czas na badanych stanach. Nie twierdzimy, że odkryto globalną linię optymalną.

{policies}
![Preferencje](policy-parameters.svg)
![Natywne XY](native-trajectories.svg)

## 12. Niezależne przypadki i baseline#124

Walidacja została ustalona przed treningiem; nigdy nie uczestniczy w elitach, średniej ani awansie curriculum. Końcowa polityka ma{sum(r['Completed'] for r in a['FinalValidation'])}/3 ukończenia, poprawę ukończenia/czasu na{a['ImprovedHeldOutScenarios']}/3 przypadków względem początkowej. Nie wybrano polityki według walidacji. Wyniki po200 nie muszą być lepsze od po100 — ten brak monotoniczności jest zachowany. Końcowy przypadek14m/s pozostaje DNF po115m legalnego postępu, mimo wcześniejszych ukończeń przez polityki po100/200. To dowód niepełnej generalizacji i powód wyniku PARTIAL, a nie podstawa do wyboru polityki na walidacji.

{validation}
Baseline#124 uruchomiono ponownie na pierwszym, z góry wskazanym przypadku9m/s/offset4m, tych samych oponach, masie, silniku, geometrii, perturbacjach i zamrożonej nawierzchni. Zmieniono wyłącznie sposób sterowania, używając oryginalnego kontrolera#124. Wynik **{'ukończony' if baseline['Completed'] else 'DNF'}**, czas{number(baseline['GateSeconds'])}s, legalny postęp{baseline['LegalProgress']:.2f}m, minimalny swept prześwit{baseline['MinimumClearanceMeters']:.4f}m. [Pełny nowy zapis](matched-124.json). Historyczne19,98s obejmuje dwa łuki i inne warunki, więc nie jest tutaj porównaniem czasu.

## 13. Odtwarzanie i weryfikacja

Uruchom z katalogu repo `python -m http.server 8012`, następnie [polityka początkowa](http://127.0.0.1:8012/tools/replay-viewer/?case=free-learning&run=overlay-initial), [po100](http://127.0.0.1:8012/tools/replay-viewer/?case=free-learning&run=overlay-after-100), [po200](http://127.0.0.1:8012/tools/replay-viewer/?case=free-learning&run=overlay-after-200), [końcowa](http://127.0.0.1:8012/tools/replay-viewer/?case=free-learning&run=overlay-final). Selektor zawiera wszystkie kompletne zapisy, także DNF. Niekompletny eksport jest zachowany osobno, bez udawania pełnego replay.

Snapshoty XY i orientacja są natywne, bez ekstrapolacji. Nakładki kamieni milowych porównują ten sam stan. Można odtwarzać, przewijać, oglądać gaz, wykonaną/żądaną kierownicę, β, yaw, koło, prześwity, kolejkę i kandydatów. Żadna prognoza nie jest pokazywana jako wykonany przejazd.

Przed pierwszą próbą19 testów C# (FreeRacing+FreeLearning), przed wznowieniem21; pięć impulsów i18 porównań prognoza/natywna kontynuacja. Po treningu{len(post['Records'])} dodatkowych porównań na łuku dla0,3/0,6/0,9s i{post['DecisionsMatched']} bitowo identycznych rekonstrukcji decyzji. Progi: XY≤0,1m, v≤0,2m/s, χ/β≤1°, kierownica≤1,5°, koło≤2rad/s, yaw≤0,05rad/s. Wynik prognoz: **{post['Passed']}**. Powtórzenie końcowej polityki ma ten sam fingerprint: **{load('determinism.json')['SameFingerprint']}**. Testy C#/Python/Node oraz standardowy Windows Draft CI opisano w PR. Dane historyczne chronią dotychczasowe testy oraz jawny łańcuch rozszerzeń#128→#129.

## 14. Całkowity koszt

CPU symulacji, decyzji i audytów offline: **{a['CpuSeconds']:.3f}s**, **{a['ContactEvaluations']:,} ocen kontaktu**, **{a['TotalForecasts']:,} prognoz** (w tym{a['EpisodeControlForecasts']:,} przy decyzjach natywnych epizodów) i **{a['NativeSteps']:,} kroków natywnych**. Prognozy obejmują zagnieżdżone działania w sprawdzeniach przyczynowych; oryginalne18 prognoz przeliczono osobno w celu dokładnego zliczenia, a dodatkowy koszt tego audytu również uwzględniono. Nie dodawano kroków predyktora do liczby światów. Kompilacja, serializacja po pomiarze CPU epizodu, testy jednostkowe, agregacja i rendering są kosztem narzędzi poza tym pomiarem; nie określamy tej liczby jako całkowitego czasu pracy komputera.

## 15. Ograniczenia i zachowane problemy

* Jeden eksport walidacji przerwała legalna metadata nieskończonego promienia przy zerowej krzywiźnie. Ciało pozostało skończone. Zachowano częściowy gzip i odzyskane metryki/koszt; ponowny świat policzono osobno. Naprawa dotyczy wyłącznie serializacji. [Dowód](export-failure.json).
* Próba52 została przerwana przez fałszywy alarm kontroli90°/s na kroku1µs: różnicowanie zaokrąglonych kątów float dało1,57207 zamiast1,57080rad/s. Błąd kąta1,276e−9rad był mniejszy od połowyULP1,863e−9. Poprawiono sprawdzenie na bitowo dokładny oryginalny clamp; nie zmieniono aktuatora. Abort i ponowny świat są policzone oddzielnie, RNG wznowiono z zachowaniem sekwencji. [Dowód](rate-guard-recovery.json), [wznowienie](resume.json), zachowane wcześniejsze protokoły.
* Predykcja0,6s i reakcja0,24s nie gwarantują bezpieczeństwa całego łuku. Mutacje nadal dają DNF. Walidacja to tylko trzy ustalone stany, a nie szeroki dowód odporności na nawierzchnie, pogodę i wyższe prędkości.
* Końcowy DNF14m/s zatrzymano w6,92s: od6,2s brak bezpiecznego działania, od6,4s wszystkie prognozowane warianty niebezpieczne. Ostatni punkt ma jeszcze0,0865m prześwitu od bandy, ale konserwatywne dolne ograniczenie prześwitu w całym kroku wynosi−0,0057m. `boundary` oznacza tu odrzucenie niecertyfikowanego przedziału, nie udowodnione przecięcie bandy w ostatnim snapshotcie. Ten konserwatyzm może odrzucać także fizycznie legalny krok. [Audyt](validation-failure-audit.json), [replay DNF](http://127.0.0.1:8012/tools/replay-viewer/?case=free-learning&run=final-holdout-14-5).
* Po awansie curriculum ocenia tylko dwa bieżące stany treningowe. Brak utrzymywania wcześniejszych poziomów może sprzyjać zapominaniu; jest to hipoteza, nie potwierdzona przyczyna tego DNF. Nie wykorzystano walidacji do zastąpienia końcowej polityki wcześniejszą.
* Zachowano obwiednię wiarygodnościβ≤45° z#128; nie ma kary za dążenie doβ=0 ani nagrody za duży uślizg. To ograniczenie modelu, nie dowód fizycznej utraty kontroli powyżej45°. Kontra i uślizg pozostają możliwe.
* Brak dowodu globalnej optymalności linii, brak kalibracji do rzeczywistego motocykla i brak treningu drugiego łuku. Nie uruchomiono rywalizacji czterech zawodników.

## Odtworzenie procesu w nowym katalogu danych

Z katalogu repo, po `dotnet build SpeedwayManager.sln -c Release`, uruchom kolejno `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll free-learning NOWY_KATALOG ETAP` dla etapów `freeze-physics`, `physics`, `freeze`, `probe`, `train`, `final`. Nigdy nie nadpisuj zamrożonych wyników. Następnie `dotnet run --project tools/free-learning-audit -c Release -- NOWY_KATALOG offline` oraz `baseline`. Skrypt `free-learning-report.py` agreguje wyłącznie zapisane dane w katalogu tego raportu i nie uruchamia symulacji. Poprawiony harness nie generuje dwóch zachowanych awarii narzędziowych; logiczna sekwencja CEM i fizyka pozostają deterministyczne, ale liczniki/checkpointy nowego treningu mogą się przez to różnić.
''',encoding='utf-8')
def main():
    source_guard();root_cause()
    if len(sys.argv)>1 and sys.argv[1]=='sources':return
    generations=load('generations.json');result=load('training-result.json');budget=load('budget.json')
    overlays={p.stem[:-5]:json.loads(gzip.decompress(p.read_bytes())) for p in sorted(DATA.glob('overlay-*.json.gz'))}
    # p.stem is overlay-name.json -> remove .json, retaining useful public recording names.
    figures(generations,overlays)
    initial=[load('initial-'+s['Name']+'-metrics.json') if (DATA/('initial-'+s['Name']+'-metrics.json')).exists() else load('initial-'+s['Name']+'-export-retry-metrics.json') for s in load('protocol.json')['Validation']]
    final=[load('final-'+s['Name']+'-metrics.json') for s in load('protocol.json')['Validation']]
    improvement=sum(b['Completed'] and (not a['Completed'] or b['GateSeconds']<a['GateSeconds']-.02) for a,b in zip(initial,final))
    grade='PASS' if result['Verified'] and all(x['Completed'] for x in final) and improvement>=2 else 'PARTIAL'
    predictor=load('post-training-prediction.json')
    if not predictor['Passed'] or not load('determinism.json')['SameFingerprint']:grade='PARTIAL'
    # Include all native worlds: generations alone omit validation, probes and retained aborts.
    native_steps=sum(json.loads(p.read_bytes())['NativeSteps'] for p in DATA.glob('*-metrics.json'))
    native_steps+=sum(r['Steps'] for r in load('physics.json')['Records'])
    native_steps+=load('export-failure.json')['RecoveredMetrics']['NativeSteps']+load('matched-124.json')['Result']['Work']['SolverCalls']
    failed=load('final-holdout-14-5.json.gz');last=failed['Samples'][-1]
    save('validation-failure-audit.json',{'NativeWorlds':0,'Name':failed['Name'],'StopSeconds':failed['Metrics']['DurationSeconds'],
        'LegalProgress':failed['Metrics']['LegalProgress'],'InstantaneousFinalInner':last['Inner'],'InstantaneousFinalOuter':last['Outer'],
        'ConservativeSweptMinimum':failed['Metrics']['MinimumClearanceMeters'],
        'FirstNoSafeContinuation':next(d['Time'] for d in failed['Decisions'] if d['NoSafeContinuation']),
        'FirstAllCandidatesUnsafe':next(d['Time'] for d in failed['Decisions'] if all(c['Safety']==2 for c in d['Candidates'])),
        'Conclusion':'Conservative swept bound rejects the interval although endpoint outer clearance is still positive. This is a DNF without a certified legal interval and exit tail, not proof of an actual endpoint capsule crossing. No safe candidate remains before the stop.'})
    save('analysis.json',{'Grade':grade,'TrainingEpisodes':sum(e['Category']=='training' for e in budget),'PhysicsWorlds':sum(e['Category']=='physics' for e in budget),
        'ValidationWorlds':sum(e['Category']=='validation' for e in budget),'TotalNativeWorlds':len(budget),'Generations':len(generations),'Stop':result['Stop'],'VerifiedPolicy':result['Verified'],
        'InitialValidation':initial,'FinalValidation':final,'ImprovedHeldOutScenarios':improvement,'ParameterSelectionUsedValidation':False,
        'CpuSeconds':sum(e['CpuSeconds'] for e in budget)+load('prediction-validation.json')['CpuSeconds']+predictor['CpuSeconds'],
        'ContactEvaluations':sum(e['Evaluations'] for e in budget)+load('prediction-validation.json')['Evaluations']+predictor['Evaluations'],
        'EpisodeControlForecasts':sum(e['Forecasts'] for e in budget),'OfflinePredictionComparisons':len(load('prediction-validation.json')['Records'])+len(predictor['Records']),
        'TotalForecasts':sum(e['Forecasts'] for e in budget)+predictor['Forecasts']+predictor['OriginalPreflightForecasts'],
        'NativeSteps':native_steps,
        'AbortedExports':1,'RetainedDiagnosticRateAbort':1,'PostTrainingPredictionPassed':predictor['Passed'],
        'Milestones':{name:run['Metrics'] for name,run in overlays.items()}})
    report(load('analysis.json'),generations,overlays)
    runs=[]
    for p in sorted(DATA.glob('*-metrics.json')):
        name=p.name[:-13];m=json.loads(p.read_bytes());runs.append({'Name':name,'Metrics':m})
    save('index.json',{'Grade':grade,'Runs':runs,'Milestones':list(overlays),'MissingMilestones':[x for x in ['overlay-after-100','overlay-after-200','overlay-final'] if x not in overlays]})
    with (DATA/'attempts.csv').open('w',newline='',encoding='utf-8') as f:
        writer=csv.writer(f);writer.writerow(['number','name','category','complete_capture','cpu_s','contact_evaluations','forecasts'])
        writer.writerows([e['Number'],e['Name'],e['Category'],e['Complete'],e['CpuSeconds'],e['Evaluations'],e['Forecasts']] for e in budget)
    save('manifest.json',{p.name:sha(p.read_bytes()) for p in sorted(DATA.iterdir()) if p.is_file() and p.name not in ['manifest.json','budget.lock']})
    print(json.dumps(load('analysis.json'),indent=2,ensure_ascii=False))
if __name__=='__main__':main()
