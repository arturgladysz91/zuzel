"""Build the reviewable report and extend the existing viewer from native records."""
import csv, gzip, hashlib, json, math
from pathlib import Path
from analyze145 import ROOT, OLD, read, save, point, ref, time, rider

D=ROOT/'docs/experiments/corner-speed-loss';N=D/'native'
a=read(D/'saved-analysis.json');audit=read(N/'score-audit.json')
results=[read(p) for p in sorted(N.glob('*-result.json'))]
corners=a['corners'];get=lambda name,lap=2,bend=2:next((c for c in corners if (c['run'],c['lap'],c['bend'])==(name,lap,bend)),None)
baseline=get('K1');energy=baseline['entry_to_minimum']['energy'];budget=read(N/'budget.json')
geometry=read(ROOT/'docs/experiments/causal-yaw-throttle/progressive-b-viewer-geometry.json')
starts=[s['startReferenceMeters'] for s in geometry['segments']]

def runpath(name):return (OLD/'native' if name in ['A','B','F','K1'] else N)/(name+'.json.gz')
def straight_after(name,lap=2,bend=2):
    run=read(runpath(name));samples=run['Riders'][0]['Samples'];L=run['LapMeters'];end=(lap-1)*L+starts[4 if bend==1 else 8]
    goal=(lap-1)*L+starts[5] if bend==1 else lap*L+starts[1]
    p,q=point(samples,end),point(samples,goal)
    return None if not p or not q else q['time_s']-p['time_s']

selected=['A','B','F','K1','joint-grid-29','exit-full-338','steer-large-31','autonomous-two-command-K1']
curves=[];indexruns=[];metrics=[]
for name in selected:
    path=runpath(name);run=read(path);L=run['LapMeters'];samples=run['Riders'][0]['Samples'];points=[]
    # Display downsampling only. Exact native captures and 10ms interval CSV remain available.
    for j,s in enumerate(samples):
        if j%5 and j!=len(samples)-1:continue
        i=s['Input'];v=i['Rider'];local=v['TotalReferenceMeters']%L
        bend=1 if starts[1]<=local<starts[4] else 2 if starts[5]<=local<starts[8] else 0
        points.append(dict(t=i['Time'],reference=v['TotalReferenceMeters'],lap=min(3,1+int(v['TotalReferenceMeters']/L)),bend=bend,
            x=v['Position']['X'],y=v['Position']['Y'],speed=v['Speed']*3.6,gas=i['Active']['Drive'],requestedGas=s['RequestedDrive'],
            steering=math.degrees(i['Steering']),requestedSteering=math.degrees(s['RequestedSteering']),beta=math.degrees(v['SlideRadians']),
            slip=s['Powertrain']['Wheel']['InstantSlipRatio'] if s['Powertrain'] else 0))
    curves.append(dict(name=name,outcome=run['Outcome'],points=points))
    indexruns.append(dict(Id=name,File=('../standing-start-three-laps/native/' if name in ['A','B','F','K1'] else 'native/')+name+'.json.gz',Riders=1,Outcome=run['Outcome']))
    previous=0;laps=[]
    for c in run['Crossings']:laps.append(dict(Lap=c['Lap'],LapSeconds=c['Time']-previous));previous=c['Time']
    metrics.append(dict(Run=name,RiderId=1,Laps=laps))
with (D/'comparison.json.gz').open('wb') as f:
    with gzip.GzipFile(filename='',fileobj=f,mode='wb',mtime=0) as z:z.write(json.dumps(curves,separators=(',',':')).encode())
save(D/'lap-metrics.json',metrics)
save(D/'index.json',dict(Title='#145 · Przyczyny utraty prędkości · 3 pełne okrążenia',DefaultRun='K1',Runs=indexruns,
    GeometryFile='../causal-yaw-throttle/progressive-b-viewer-geometry.json',BendReferences=[starts[j] for j in [1,4,5,8]],
    Note='Zapis natywny; ręczne interwencje są diagnostyką. Nieudane próby i naruszenia pozostają widoczne. Prototyp autonomiczny nie przeszedł walidacji.'))

lines=['# #145 — Corner Speed Loss: trzy nieprzerwane okrążenia',
       '', '**Werdykt: diagnoza potwierdzona częściowo; legalna poprawa diagnostyczna, brak zatwierdzonej poprawki autonomicznej.**',
       '', 'Baza `experiment/standing-start-three-laps-144`, dokładnie `9342ac506c6fc7b713c91842b4e6fff55b4891b6`. Eksperyment domyślnie OFF. Bez zmiany produkcyjnej fizyki, silnika, przyczepności, geometrii, wag i historycznych danych. Bez CEM i bez merge.',
       '', '## Sześć odpowiedzi', '',
       '1. **Dlaczego K1 traci 47,73 km/h?** Wjeżdża z 93,886 km/h, narasta β do 27,255°, a siły poprzeczne wykonują dużą ujemną pracę w kierunku ruchu. Gaz pozostaje zamknięty przez 4,843 z 5,073 s do minimum. Praca przedniej opony −14,666 kJ, tylnej siły poprzecznej −18,653 kJ, oporów −9,364 kJ i tylnej siły napędowej +4,777 kJ sumuje się do spadku energii postępowej −37,906 kJ. To pomiar bilansu; nie procentowa atrybucja winy sterownika.',
       '2. **Sterownik czy fizyka?** Oba mechanizmy są powiązane. Ubytek energii bezpośrednio realizują siły opon i opory; sterownik ogranicza dopływ mocy i kształtuje yaw/linię. Przy tej samej kierownicy samo dodanie gazu w 29,0 s kończy się naruszeniem obrysu lub brakiem certyfikacji. Skoordynowane gaz+skręt dają legalną poprawę. Nie wyizolowano wiarygodnego udziału procentowego ani jednej wyłącznej przyczyny. Moment do koła do minimum jest dodatni; nie wykazano hamowania silnikiem w tej fazie.',
       '3. **Czy linia jest zła i wydłuża drogę?** Drugie okrążenie K1 ma 343,97 m, lecz jego pierwszy łuk ma 117,13 m (A: 104,35 m). Badany drugi łuk K1 ma 102,07 m, nawet nieco mniej niż A: 102,84 m. Dodatkowy dystans całego okrążenia pochodzi głównie z pierwszego łuku. Drugi łuk łączy szybki zjazd z offsetu około 7 m w okolice wewnętrznej i oscylacje yaw. To obserwacja linii; globalnie optymalnej linii nie wyznaczono.',
       '4. **Czy można szybciej bez zmiany fizyki?** Tak, lokalnie i z legalną kontynuacją do trzech okrążeń: `joint-grid-29` skraca badany łuk 6,211→6,054 s, podnosi minimum 46,160→50,376 km/h i wyjście 52,587→56,791 km/h. `exit-full-338` także legalnie skraca łuk. Te ręczne działania nie są autonomicznym sterownikiem ani dowodem wykonalności utrzymania 90 km/h przez cały łuk.',
       '5. **Opóźnienie, akcje czy planowanie?** Opóźnienie 0,24 s, review 0,2 s i szybkość skrętu 90°/s są zachowane. W 29,0 s do wejścia pozostaje około 15,6 m; aktywacja następuje 0,24 s później. Alternatywa gaz+skręt istnieje już w dotychczasowym zbiorze. Wartość pośrednia −10° i większy skręt −25° nie dały legalnego pełnego dystansu. Ten sam manewr wcześniej i później ma gorszą legalność. H09 z utrzymanym działaniem błędnie porządkuje niektóre rzeczywiste kontynuacje; prosty prototyp dwóch komend zawodzi na pierwszym okrążeniu. Krótki horyzont i niewykonana przyszła korekta pozostają ograniczeniem, a nie samodzielnie udowodnioną wyłączną przyczyną.',
       '6. **Co zmienić w algorytmie?** Kolejny uzasadniony etap to ocenianie skoordynowanego manewru do wyjścia z łuku i następnej prostej, z pełnym stanem napędu, kolejką oraz sprawdzaniem, czy planowana druga korekta rzeczywiście pozostaje wybierana przy kolejnych obserwacjach. Kryterium czasu i wyjścia trzeba zweryfikować przyczynowo. Nie ma podstaw do podnoszenia przyczepności, mocy ani globalnego zwiększania gazu. Dostarczony default-OFF prototyp dwóch komend jest **odrzucony** po natywnym teście; nie rekomendujemy jego wdrożenia.',
       '', '## Każdy z sześciu łuków każdego wariantu', '',
       'Wejście/wyjście interpoluje się wyłącznie jako skalarną metrykę w tych samych przekrojach referencyjnych. Minimum jest dokładnym zachowanym stanem natywnym (rozdzielczość nominalnie 10 ms), nie obietnicą minimum ciągłego między próbkami. Trzy fazy są równymi częściami długości referencyjnej całego łuku.', '',
       '| Wariant / okr. / łuk | V wejście | V min | V wyjście km/h | Czas s | Droga m | Min: t / ref m | Główna zmierzona ujemna praca | Dowód |',
       '|---|---:|---:|---:|---:|---:|---|---|---|']
labels={'front_translation_J':'przednia opona','rear_lateral_translation_J':'tylna Fy / scrub','resistance_J':'opory'}
for c in corners:
    if c['run'] not in ['A','B','F','K1']:continue
    loss=c['entry_to_minimum'];leader=labels[c['measured_leading_negative_body_work'][0]] if loss else 'rozpędzanie; minimum na wejściu'
    lines.append(f"|{c['run']} / {c['lap']} / {c['bend']}|{c['entry']['speed_kph']:.3f}|{c['minimum']['speed_kph']:.3f}|{c['exit']['speed_kph']:.3f}|{c['duration_s']:.3f}|{c['distance_m']:.2f}|{c['minimum']['time_s']:.3f} / {c['minimum']['reference_m']:.3f}|{leader}|[fazy i bilans](saved-analysis.json), `{c['run']}-intervals.csv.gz`|")
lines+=['', '## Ranking mechanizmów potwierdzonych pomiarem i próbami', '',
    '1. **Scrub poprzeczny i orientacja sił opon względem prędkości.** Największe ujemne składniki pracy w K1: tylna Fy i przednia opona. Przy największym opóźnieniu 31,050 s: −5,690 m/s², β=27,230°, front/rear utilization=0,966/0,975. Siły są prawie wyczerpane poprzecznie. Zmiana samego skrętu zmienia minimum i wyjście; dobry wynik lokalny nie gwarantuje legalności kolejnego okrążenia.',
    '2. **Zbyt długa przerwa w dopływie mocy przy odzyskiwaniu prędkości.** Jedna decyzja gazu w 33,8 s daje legalną szybszą kontynuację. Nie oznacza to, że wcześniejsze zamknięcie gazu jest w całości zbędne: analogiczna ingerencja w 29,0 s zawodzi.',
    '3. **Ograniczona siła i promień.** 90 km/h na R=36 m wymaga 2552 N przy absolutnym budżecie 1852 N. To niewykonalne w tym zamrożonym modelu. Dla R=50 m zostaje tylko około 15 N rezerwy w idealnej sumie kontaktów, przed sprzężeniem Fx/Fy i wymaganiami momentu yaw. Scalarna granica jest warunkiem koniecznym, nie certyfikatem wykonalności.',
    '4. **Opory ruchu.** Ich zmierzona praca do minimum wynosi −9,364 kJ. Nie stosowano osobnego eksperymentu wyłączającego opory, więc nie podajemy przyczynowego zysku po ich hipotetycznym usunięciu.',
    '5. **Linia, przejściowy yaw i krótka ocena utrzymanej komendy.** Wspólna ingerencja jest lepsza niż sam gaz, istniejący score odrzuca ją. To potwierdza błąd lokalnego porządkowania rzeczywistych kontynuacji. Nie dowodzi, że samo wydłużenie horyzontu albo poszerzenie kierownicy rozwiąże problem.',
    '', 'Duży wheelspin nie jest dominującym składnikiem badanego spadku: praca strat tylnego poślizgu wzdłużnego wynosi około 0,488 kJ, w porównaniu z dużo większymi pracami poprzecznymi. Sprzęgło: 1,970 J ciepła; dodatkowy drag wału: 0. Moment koła pozostaje dodatni, a energia obrotowa wału i koła jest oddawana. Nie utożsamiamy dodatniego momentu z wystarczającą mocą do utrzymania prędkości.',
    '', '## K1: bilans trzech faz drugiego łuku drugiego okrążenia', '',
    '|Faza|Czas s|ΔK postęp. kJ|Przód kJ|Tył Fx kJ|Tył Fy kJ|Opory kJ|Spalanie kJ|Gaz 0 s|',
    '|---|---:|---:|---:|---:|---:|---:|---:|---:|']
for name,p in baseline['phases'].items():
    e=p['energy'];lines.append(f"|{name}|{p['duration_s']:.3f}|{e['translation_change_J']/1000:.3f}|{e['front_translation_J']/1000:.3f}|{e['rear_drive_translation_J']/1000:.3f}|{e['rear_lateral_translation_J']/1000:.3f}|{e['resistance_J']/1000:.3f}|{e['combustion_J']/1000:.3f}|{p['closed_gas_s']:.3f}|")
lines+=['', 'Cały bilans zawiera osobno energię postępową, yaw, koła i wału, pracę spalania, ciepło sprzęgła, straty przekładni, łożyska, oba kierunki poślizgu i opory. Reszta bilansu do minimum: −0,079 J (zaokrąglenia float/native), wobec około 38 kJ zmiany energii postępowej. Prace yaw nie są ponownie dodawane do strat postępowych. Wszystkie 72 fazy bazowe oraz fazy interwencji i zakresy gazu/skętu/yaw/β/Fx/Fy/utilizacji/gapów znajdują się w [saved-analysis.json](saved-analysis.json); pełne chwilowe wartości w CSV.gz.',
    '', '## Wykonalność fizyczna i promień', '',
    '|Promień m|Absolutna granica km/h|Wymagana siła przy 90 km/h N|Wymagany yaw przy 90 rad/s|',
    '|---:|---:|---:|---:|']
for q in audit['ForceBounds']:lines.append(f"|{q['RadiusMeters']:.0f}|{q['AbsoluteUpperKph']:.2f}|{q['Demand90Newtons']:.1f}|{q['RequiredYaw90RadiansPerSecond']:.3f}|")
with gzip.open(D/'K1-intervals.csv.gz','rt',encoding='utf-8') as f:observed=list(csv.DictReader(f))
lines+=['', 'Rzeczywiste promienie K1 na badanym łuku (najbliższy zachowany przedział):', '',
    '|Punkt|t s|V km/h|R trajektorii m|R lokalnej geometrii z offsetem m|F wymagane na lokalnym R przy 90 N|',
    '|---|---:|---:|---:|---:|---:|']
for label,target in [('wejście',baseline['entry']['time_s']),('maksymalne opóźnienie',31.05),('minimum',34.67)]:
    q=min(observed,key=lambda q:abs(float(q['time_s'])-target))
    values=[f"{float(q[k]):.3f}" if q[k] else 'N/D' for k in ['time_s','speed_kph','trajectory_radius_m','local_offset_radius_m','local_radius_90_demand_N']]
    lines.append('|'+label+'|'+'|'.join(values)+'|')
lines+=['', 'Obliczenie: `F=m v²/R`, `v_upper=sqrt((C_front+C_rear) R/m)`, masa 147 kg, µ=1,2845, Fz przód/tył 665,571/776,499 N; C przód/tył 854,926/997,413 N. Obciążenia są statyczne, jak w #144, nie dodano transferu masy. Rozdzielenie Fx/Fy ogranicza tylna opona; moment yaw wynika wyłącznie z r×F dwóch kontaktów. Maksymalny moment przy pełnym poprzecznym budżecie obu kół jest tylko luźną granicą — nie można równocześnie dowolnie wybrać maksymalnego Fy i yaw.',
    '', 'Fx/Fy w CSV są opisane w układzie korpusu. Przedni kontakt ma w układzie skręconego koła tylko Fy (Fx=0); projekcja tej siły na oś korpusu może mieć Fx. Nie jest to osobny przedni napęd ani mechaniczny hamulec.',
    '', 'Są to promienie porównawcze z zakresu skali geometrii, a nie założona idealna linia ani sztuczny limit w sterowniku. Rzeczywisty promień trajektorii z wektora v i jego zmiany oraz promień lokalnej geometrii są osobnymi polami w CSV. Szerszy tor nie gwarantuje dłuższego odcinka o R=50 m: trzeba jeszcze legalnie osiągnąć tę pozycję i przejść zwężenie. Wyłącznie legalne natywne kontynuacje dowodzą wykonalności konkretnej alternatywy. `full-gas-entry-sequence` zaczyna z identycznego prefiksu przy około 94 km/h i przekracza β45; nie podtrzymuje 90 km/h przez łuk. To nie dowód globalnej niemożliwości dowolnej szerokiej linii.',
    '', '## Konkretne decyzje i pełna punktacja', '',
    'Pełne rozbicie wszystkich wybranych i odrzuconych działań dla dziewięciu odtworzonych decyzji (A/B/F i sześć K1), wraz z dodatkowymi hipotezami przestrzeni akcji, jest w [score-audit.json](native/score-audit.json). Odtworzenie wyniku każdej pierwotnej kandydatury jest dokładne: maksymalna różnica punktacji = 0. Prognozy nie tworzą nowych światów. Safety jest porządkowane przed score; nie wolno tłumaczyć odrzucenia wyłącznie niższym score.', '']
for target in [29,33.8]:
    d=next(q for q in audit['Audits'] if q['Run']=='K1' and abs(q['Time']-target)<1e-8);v=d['Input']['Rider']
    lines+=[f"### Obserwacja {target:.1f} s",'',f"Stan: v={v['Speed']*3.6:.3f} km/h; ref={v['TotalReferenceMeters']:.3f} m; offset={v['Road']['OffsetMeters']:.3f} m; β={math.degrees(v['SlideRadians']):.3f}°; yaw={v['AngularVelocity']:.3f} rad/s; koło={d['Input']['Omega']:.3f} rad/s; wał={d['Crank']*30/math.pi:.1f} RPM; oczekujące komendy={len(d['Input']['Pending'])}. Pełny stan przed decyzją i fingerprint w JSON. Aktywacja {target+.24:.2f} s, bez obchodzenia kolejki.",'',
        '|Skręt °|Gaz|Safety|Score K1|Progress|Kierunek|Yaw|Zmiana skrętu|Clearance + unsafe|',
        '|---:|---:|---:|---:|---:|---:|---:|---:|---:|']
    shown=[x for x in d['Leaves'] if x['InOriginalGrid'] and (abs(x['Action']['SteeringRadians']-d['Chosen']['SteeringRadians'])<1e-8 or abs(x['Action']['SteeringRadians']+5*math.pi/180)<1e-7 and abs(x['Action']['Drive']-.31820688)<1e-7)]
    for x in shown:
        t=x['Terms'];lines.append(f"|{math.degrees(x['Action']['SteeringRadians']):.1f}|{x['Action']['Drive']:.3f}|{x['Safety']}|{x['Score']:.3f}|{t['Progress']:.3f}|{t['Direction']:.3f}|{t['Yaw']:.3f}|{t['Steering']:.3f}|{t['Clearance']+t['Unsafe']:.3f}|")
    lines+=['', 'K1 usuwa składniki Throttle i Geometry; ich pierwotne wartości pozostają zapisane w pełnym audycie. W 29,0 s gaz 0 wygrywa z częściowym głównie przez prognozowany yaw i kierunek. W 33,8 s częściowy gaz przegrywa głównie przez kierunek, a pełny przez yaw; wszystkie te trzy kandydatury mają Safety=Safe. Legalna natywna ingerencja w 33,8 s pokazuje zbyt późny powrót do napędu w tej konkretnej kontynuacji.']
lines+=['', '## Interwencje: identyczny pełny prefiks od startu', '',
    'Każdy nowy świat zaczyna się od właściwego v=0, koła=0, wału i sprzęgła #144. Prefiks wykonuje zapisane oryginalne decyzje przez natywną kolejkę i aktuator. Nie wczytuje się uproszczonego checkpointu i nie ustawia po fakcie pozycji, prędkości, yaw ani β. Pierwsze fingerprinty stanów przed ingerencją są identyczne. Następne punkty sekwencji celowo różnią się od bazy po wcześniejszej ingerencji. Powtórka `replay-K1` odtwarza fingerprint **całego** oryginalnego dystansu.', '',
    '|Próba|Wynik / ukończone okr.|Łuk L2 B2 s|V min / wyjście km/h|Następna prosta s|Całość s|',
    '|---|---|---:|---:|---:|---:|']
for r in results:
    name=r['Treatment']['Name'];c=get(name);straight=straight_after(name) if c else None
    finish=r['Crossings'][-1]['Time'] if r['Outcome']=='completed' else None
    lines.append(f"|{name}|{r['Outcome']} / {r['CompletedLaps']}|{c['duration_s']:.3f}|{c['minimum']['speed_kph']:.3f} / {c['exit']['speed_kph']:.3f}|{straight:.3f}|{finish:.3f}|" if c and straight is not None and finish is not None else f"|{name}|{r['Outcome']} / {r['CompletedLaps']}|{format(c['duration_s'],'.3f') if c else 'N/D'}|{format(c['minimum']['speed_kph'],'.3f')+' / '+format(c['exit']['speed_kph'],'.3f') if c else 'N/D'}|{format(straight,'.3f') if straight is not None else 'N/D'}|N/D|")
lines+=['', 'Zmiany i dokładne czasy są zamrożone w `treatments-initial.json`, `treatments-followup.json`, `treatments-autonomous.json`, `treatments-exact-grid.json`. Osobne `*-result.json` zawierają cały stan przed ingerencją, fingerprint, legalność, minimalne gapy i źródłowe SHA. Duży skręt −25° skraca badany łuk, ale narusza obrys na kolejnym okrążeniu: **nie jest sukcesem**. Interwencja złożona z trzech korekt także kończy się brakiem certyfikacji. Nie ukrywamy późnych porażek.',
    '', 'Pierwsza interwencja `joint-29` używała matematycznie obliczonego −5°, co różni się o jeden ULP float32 od zapisanego kąta siatki. Dodatkowy `joint-grid-29` powtarza próbę z dokładnym bitem oryginalnej kandydatury −0,08726647 rad. Także kończy legalnie i daje ten sam zaokrąglony wynik łuku. Wszystkie wcześniejsze próby pozostają w kosztach i danych.',
    '', 'Legalna szersza linia `joint-grid-29` ma na badanym łuku 108,42 m wobec 102,07 m K1, a jednak jest o 0,157 s szybsza. To bezpośredni przykład, dlaczego najmniejszy dystans nie oznacza najmniejszego czasu.',
    '', '## Pełne trzy okrążenia: porównanie z A/B/F/K1', '',
    '|Wariant|Okr.1 s|Okr.2 s|Okr.3 s|Razem s|Okr.2+3 s|Min. gap inner/outer m|Wynik|',
    '|---|---:|---:|---:|---:|---:|---:|---|']
for name in selected:
    run=read(runpath(name));m=next(q for q in metrics if q['Run']==name);ls=[q['LapSeconds'] for q in m['Laps']]
    times=[f'{x:.3f}' for x in ls]+['N/D']*(3-len(ls));samples=run['Riders'][0]['Samples']
    whole=f'{sum(ls):.3f}' if len(ls)==3 else 'N/D';warm=f'{sum(ls[1:]):.3f}' if len(ls)==3 else 'N/D'
    lines.append(f"|{name}|{'|'.join(times)}|{whole}|{warm}|{min(s['Inner'] for s in samples):.4f} / {min(s['Outer'] for s in samples):.4f}|{run['Outcome']}|")
lines+=[
    '', '## Prototyp autonomiczny — wynik negatywny', '',
    '`autonomous-two-command-K1`: jedna zmiana algorytmiczna, bez zmiany wag — zamiast utrzymania pierwszej komendy przez H09 ocenia się do 36 opóźnionych par spośród maksymalnie sześciu korzeni wybranych z dotychczasowej siatki. Obie komendy zachowują reakcję 0,24 s; druga jest obserwowana hipotetycznie w +0,2 s. W świecie natywnym wykonywana jest tylko pierwsza, po czym sterownik ponownie podejmuje decyzję. Wał/koło i nieodwracalne kolejki są przenoszone między częściami prognozy.',
    '', 'Próba od właściwego startu **narusza obrys w 6,57 s**, przed końcem pierwszego okrążenia. Nie ma dowodu, że sam dodatkowy krok planowania wystarcza. Planowana korzystna druga korekta nie jest zobowiązaniem kolejnej decyzji; receding-horizon może zmienić przyszłość, na której opierał ocenę. Zapis planów pokazuje ten problem, ale pełnego związku przyczynowego tej porażki nie izolowano osobnym eksperymentem. Prototyp pozostaje OFF i nie jest proponowany do produkcji.',
    '', '## Koszt, zakres i odtworzenie', '',
    f"**{len(budget)} nowych światów, {sum(r['CpuSeconds'] for r in budget):.3f} CPU-s rezerwacji natywnych**; dodatkowo {audit['CpuSeconds']:.3f} CPU-s audytu bez światów. Wszystkie próby zachowano. Budżet nie obejmuje kompilacji, testów regresji i prezentacji danych; nie są to trening ani nowe próby badawcze. Bez szerokiego przeszukiwania wag. Wszystkie 4×6 łuków zachowanych #144 przeanalizowano przed pierwszym nowym światem.",
    '', 'Generator `generate.py --check` dowodzi, że nowy collector jest dokładnie #144 z nazwą klasy i jednym wstrzyknięciem chooser. Używa publicznych rekordów #144 i tego samego ContactAdapter/Box2D, nie alternatywnego solvera. Historyczne źródła #144 oraz dane bazowe są nienaruszone. `provenance.json` podaje hashe końcowych źródeł i surowych zapisów; jest manifestem odtwarzania, nie deklaracją prerejestracji powstałą przed badaniem.',
    '', 'Odtworzenie: `powershell -File tools/corner-speed-loss/run.ps1 -Evidence work/repro145`. Analiza zapisów sama nie uruchamia światów. Pełne natywne odtworzenie wymaga .NET 8 i Windows. Skrypt zachowuje niekompletne rezerwacje i nie ponawia ich po cichu.',
    '', '[Rozbudowany istniejący viewer](http://127.0.0.1:8018/tools/replay-viewer/?case=corner145&run=K1&t=29): porównanie A/B/F/K1, legalnych ingerencji, późnej porażki większego skrętu i nieudanego prototypu. Wybór każdego okrążenia/łuku, prędkość po czasie i pozycji, gaz żądany/aktywny, skręt żądany/rzeczywisty, β/wheelspin, wspólne linie XY i skoki do Vmin/maksymalnego opóźnienia. Wykresy są próbkowane do prezentacji; surowe 10ms dane pozostają w zapisach.',
    '', '![K1: drugi łuk drugiego okrążenia](corner145.svg)',
    '', 'Walidacja jest raportowana osobno w `validation.json`. Lokalny wynik ani zielony Draft CI nie oznacza pełnej walidacji gotowego zawodnika. Wniosek o realnym żużlu pozostaje ograniczony prowizoryczną oponą i modelem płaskiego motocykla.']
(D/'README.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')

# Compact independent phase table for direct spreadsheet review.
with (D/'phases.csv').open('w',encoding='utf-8',newline='') as f:
    rows=[]
    for c in corners:
        for name,p in c['phases'].items():
            rows.append(dict(run=c['run'],lap=c['lap'],bend=c['bend'],phase=name,seconds=p['duration_s'],closed_gas_s=p['closed_gas_s'],
                **p['energy'],**{f'{k}_{kind}':v[kind] for k,v in p['stats'].items() for kind in ['min','mean','max']}))
    w=csv.DictWriter(f,fieldnames=list(rows[0]),lineterminator='\n');w.writeheader();w.writerows(rows)

sources=[*ROOT.joinpath('tools/corner-speed-loss').glob('*.cs'),*ROOT.joinpath('tools/corner-speed-loss').glob('*.py')]
save(D/'provenance.json',dict(BaseHead='9342ac506c6fc7b713c91842b4e6fff55b4891b6',EnabledByDefault=False,Worlds=len(budget),
    NativeReservationCpuSeconds=sum(r['CpuSeconds'] for r in budget),OfflineScoreAuditCpuSeconds=audit['CpuSeconds'],
    Sources={str(p.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
    NativeFiles={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in N.glob('*.json.gz')},
    HistoricalInputHashes=a['sources'][:4]))
print(f'Report: {len(corners)} completed corners, {len(budget)} worlds, {sum(r["CpuSeconds"] for r in budget):.3f} CPU-s')
