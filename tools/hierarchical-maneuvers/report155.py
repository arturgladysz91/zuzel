"""Write the Polish research report from sealed measurements, with charged CPU."""
from analyze155 import D,BASE,read,charged
def main():
 with charged('report-export-v1'):
  a=read(D/'analysis.json');rows=a['Final'];lines=[]
  for t in [0,6.6,13.8]:
   for mode in 'ABC':
    x=next(x for x in rows if x['mode']==mode and x['time_s']==t)
    arrival=f"{x['arrival_s']-t:.3f}" if x['arrival_s'] is not None else '—'
    path=f"{x['path_to_goal_m']:.3f}" if x['path_to_goal_m'] is not None else '—'
    lines.append(f"|{t:.1f}|{mode}|{'13/9 + independent' if x['passed'] else 'ABSTAIN'}|{x['planner_cpu_s']:.3f}|{x['process_cpu_s']:.3f}|{x['full_k1']}|{x['physical_forecasts']}|{x['primitive_candidates']}|{x['complete_generator_rollouts']}|{x['distinct_legal_generator_states']}|{arrival}|{path}|")
  table='\n'.join(lines)
  stability='\n'.join(f"|{x['name'].removeprefix('stability-C-0.2-').removesuffix('-v1')}|{x['position_delta_m']:.10f}|{x['planner_cpu_s']:.3f}|{x['process_cpu_s']:.3f}|{x['physical_forecasts']}|13/9 + independent|" for x in a['Stability'])
  report=f'''# Badanie #155 — hierarchiczny generator fizycznych manewrów

**PARTIAL.** C zastąpiło K1 od startu, uzyskało pełny lokalny certyfikat13/9 i przeszło
trzy kontrole własnej następnej obserwacji. Nie uzyskało certyfikatu w stanach6,6 i13,8 s,
w których S0 przechodzi. Brama A nie jest zaliczona; nowych natywnych bram C–E nie
uruchomiono. Nie ukończono trzech okrążeń S. Brak podstaw do deklaracji kosztu biegu
poniżej140 lub100 CPU-s.

Baza: `{BASE}`. Nowa gałąź `experiment/hierarchical-maneuvers-155`.
PROVISIONAL, domyślnie OFF, bez merge. Wynik nie zmienia produkcyjnej fizyki ani danych.
Końcowy SHA i rzeczywisty wynik Windows CI zostaną zapisane w opisie Draft PR oraz
zewnętrznym `validation155-final.json`; niniejszy plik nie deklaruje przyszłego CI PASS.

## 1. Czy kosztowne długie ogony K1 można zastąpić innym generatorem?

**Lokalnie tak; ogólnie jeszcze nie.** C-0.0-v4 generuje manewr od spoczynku do
oryginalnego dalszego przekroju350,779 m i otrzymuje13 węzłów, dziewięć rezerw oraz
świeżą niezależną walidację. Liczba pełnych K1 wynosi0, wobec319 w A. CPU planera
spada z15,15625 do7,78125 s (48,66%). Koszt całego procesu diagnostycznego wynosi
8,328125 wobec15,703125 CPU-s. Nie usunięto wymaganych prognoz fizycznych.

Diagnoza #153/#154:6329 pełnych K1 i247,953125 CPU-s dotyczą historycznej próby #153,
69 decyzji + odmowy13,8 s. #154 S0 zachował wszystkie69 zaakceptowanych fizycznych
hashy i wszystkie pola próbek natywnych, ale końcowa niepełna próba wykonała inną
liczbę obliczeń (suma6169). Nie przedstawiam tej różnicy jako optymalizacji.
167763 lokalne kandydatury i125611 lokalnie legalnych w instrumentowanej diagnostyce
#154 nie są próbami pełnego manewru.1578/4708 to legalne pełne rollouty warunkowo
wybrane przez dotychczasowe wyszukiwanie, nie prawdopodobieństwo losowej bezpiecznej jazdy.

W #154 znaleziono563 powtórzenia zgrubnych końcowych komórek na167763 kandydatury
(około0,34%). To nie dowodzi ogromnej liczby identycznych pełnych stanów: komórki nie
zawierały całego crank/aktywnych komend/kolejki. Dominującym problemem jest wielokrotne
lokalne wybieranie0,9-sekundowej prognozy w budowie dalekich ogonów, bez skutecznego
rozróżniania długoterminowej odzyskiwalności. Cache nie zastępuje tego rozróżnienia.

## 2. Czy nowy generator tworzy wystarczająco różne fizycznie wykonalne manewry?

**Wystarczająco od startu, niewystarczająco w późniejszych stanach.** To ograniczona
wiązka makromanewrów: szerokość4, po trzy obserwacje0,2 s na prymityw, najwyżej130
obserwacji. Fazy Launch/Prepare/Initiate/MidBend/Exit/NextPreparation zależą od
rzeczywistej geometrii, prędkości, β, postępu w łuku i odległości do zmiany krzywizny.
Komendy obejmują lokalny secant momentu yaw wyznaczony z sił opon, jego korekty,
bezwzględne węzły kierownicy i sprzężone impulsy kierownicy/gazu. Parametry prymitywu
mają punkty zmiany; komendy nie pochodzą z ręcznie zapisanej idealnej linii.
Aktualne steering/active/pending są częścią stanu każdej gałęzi. Wheel/crank są
propagowane przez oryginalny model. Wszystkie prymitywy przechodzą pełną fizykę.

```mermaid
flowchart LR
 O[Stan i kolejka po obserwacji] --> F[Adaptacyjna faza i mały słownik prymitywów]
 F --> P[Niezmieniona fizyka każdego etapu]
 P --> H[Wiązka ETA i różnorodność]
 P --> C[Wariant C: prefiks i świadek następnego etapu]
 C --> H
 H --> T[Pełny oryginalny manewr terminalny]
 T --> R[Warunkowe drzewo 13 i 9 rezerw]
 R --> I[Świeża niezależna propagacja]
 I --> A[Akceptacja albo jawne ABSTAIN]
```

C0:3567 kandydatur prymitywów,3191 różnych pełnych fizycznych stanów na końcach
legalnych etapów i92 kompletne legalne rollouty generatora. **92 nie oznacza92
niezależnie certyfikowanych polityk.** Wybrano jedną pełną politykę13/9. Jej dziewięć
rezerw ma trzy różne sekwencje komend; identyczne wczesne reakcje nie usuwają logicznych
węzłów. Minimum nominalnego obrysu1,072957 m, swept lower bound0,975660 m;
minimum sweep w dziewięciu rezerwach0,961587 m. Skończone skorelowane znaki
nie stanowią dowodu odporności w całym otoczeniu.

Zgrubne komórki wiązki uwzględniają rodzinę, steering, active, pending, pozycję,
prędkość, β i yaw, lecz nie są kluczami równoważności certyfikatów. Nie łączą
fizycznych dowodów. Wariant B prunuje zbyt wcześnie potrzebne przebiegi odzyskania
orientacji i poślizgu; proste ETA nie wystarcza. Nie dodano kolejnych wag K1.

## 3. Czy można tanio przewidzieć brak dalszej osiągalności bez pełnego drzewa?

**Częściowo.** Nowa komenda nie może zmienić następnych0,2 s przy opóźnieniu0,24 s.
C odrzuca kandydata, gdy dokładnie propagowany nieodwracalny prefiks nie uzyskuje S.
To konieczna kontrola wymaganego certyfikatu, a nie dowód rzeczywistego uderzenia:
konserwatywna granica sweep może odrzucić fizycznie bezkolizyjny przedział.
Następnie C szuka choć jednego legalnego kolejnego prymitywu0,6 s. W C0 sprawdzono169
stanów:16 bez świadka w słowniku,153 ze świadkiem. Prefiks odrzucił283 kandydatury.
Brak świadka oznacza brak następcy w ograniczonym słowniku, nie brak dowolnego sterowania.
Pozytywny świadek nie gwarantuje ukończenia następnego łuku.

Ranking prędkości wynikającej z krzywizny/oporu opon, zapasu miejsca i wymaganego
tempa do terminala jest **heurystyką**, nie viability funnel. Lokalna linearyzacja
sił opon służy tylko propozycji komendy w otoczeniu aktualnego steering; nie zastępuje
modelu. Pierwsza wersja błędnie ekstrapolowała lokalny Jacobian aż do fizycznego
limitu kierownicy, powodując narastanie yaw i β45. v2 ograniczyło nominalną propozycję
do przedziału sondowania. v3 dodało świadków kolejnego etapu i kolejkę w komórkach,
v4 sprzężone punkty zmiany kierownicy i gazu. Dopiero v4 uzyskało pełny C0.
Wszystkie23 próby i ich faktyczne wersje źródeł zachowano, łącznie z odmowami.

## 4. Czy certyfikowany zbiór terminalny może ograniczyć planowanie całego łuku?

**Potencjalnie tak, w tym badaniu nie został udowodniony.** Zbiór musiałby zawierać
pełne położenie i orientację, prędkość/yaw, wheel/crank, steering, active, wszystkie
komendy i czasy aktywacji oraz wykazać istnienie dopuszczalnego sterowania i zamknięcie
następników przy wymaganych obserwacjach. Kilka bezpiecznych końców trajektorii oraz
13/9 nie dowodzą tej własności. Zachowano więc oryginalny SafetyGoal przez dalszy łuk
oraz26 s. Start/6,6: wspólny cel191,389375 m, SafetyGoal350,778750 m;
13,8:350,778750 /510,168125 m. Brak podmiany dalekiego terminala na krótki bezpieczny koniec.

Limit26 s jest ograniczeniem reprezentacji/wyszukiwania, nie definicją fizycznego
bezpieczeństwa. S1 v2 z #154 pokazuje możliwość legalnej, lecz zbyt wolnej trajektorii,
która nie dociera w czasie do350,779 m. Nie wystarcza to do usunięcia wymogu: potrzebny
jest dowód dalszej kontynuacji. C uwzględnia ETA, lecz nie udowadnia globalnego minimum czasu.
Koszt nowej metody nadal rośnie z horyzontem fizycznej propagacji i liczbą stanów wiązki;
wyeliminowano mnożnik wielokrotnego K1, nie całą zależność od długości manewru.

## 5. Czy nowy algorytm działa od startu, a nie tylko z trudnego stanu?

**Tak dla jednej pełnej polityki C0; nie jako uniwersalny planner.** Końcowa macierz:

|Stan s|Wariant|Wynik|CPU planera s|CPU procesu s|Pełne K1|Fizyczne prognozy|Prymitywy|Kompletne rollouty generatora|Różne legalne stany generatora|Czas do wspólnego celu s|Droga do celu m|
|---:|:---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
{table}

Czas do celu jest względny do obserwacji; droga jest całką odcinków gęstej prognozy,
z interpolacją końcowego przecięcia. „0” w kolumnach generatora A oznacza brak tego
nowego generatora, nie brak legalnych starych rolloutów; te ostatnie są policzone
osobno w `conditional_legal_attempts` w analysis/CSV. Prognozy fizyczne obejmują świeżą
niezależną walidację; żądania i trafienia współdzielenia są osobno w danych.
A w6,6/13,8 ma własną wcześniejszą politykę S0, B/C startują bez historycznych przyszłych
taśm. To jawne porównanie możliwości zastąpienia generatora; nie idealna ablacja
identycznego ciepłego cache. Od startu wszystkie role nie mają wcześniejszej polityki.

C0 wykonuje11172 fizyczne prognozy wobec10502 w A; spadek CPU nie jest spadkiem
wszystkich liczników pracy. Do191,389 m dociera w13,213355 s wobec12,854597 s A:
o0,358758 s (2,79%) wolniej, drogą208,498 wobec196,800 m. Nie deklaruję poprawy szybkości.
CPU mierzono procesowym zegarem Windows, bez odjęcia nieudanych poszukiwań. Profil jest
wyłączną partycją faz; operacje stanowią drugą partycję i nie są dodawane do faz.

## 6. Czy po następnej obserwacji pozostaje bezpieczna kontynuacja?

**Lokalnie tak w0,2 s, brak dowodu ciągłej serii.** Własny prior pochodzi wyłącznie
z C0. Ciało i crank w0,2 s pochodzą z zapisu natywnego #154, z przyczynowym podstawieniem
własnej komendy0,0 w kolejce. Nowa komenda nie działa przed0,24 s, więc nie zmienia
tego rzeczywistego prefiksu. To diagnostyka, nie nowy świat natywny.

|Obserwacja0,2 s|Rzeczywista zmiana pozycji m|CPU planera s|CPU procesu s|Prognozy|Wynik|
|---|---:|---:|---:|---:|---|
{stability}

W joint-micro dodano także nominalnie0,5 µm/s, heading/yaw1e-7 i wheel/crank1e-6;
faktyczne zaokrąglone delty zachowano w danych. Wszystkie trzy kontrole mają0 K1,
nowy certyfikat13/9 i niezależne obliczenia. Korzystają ze sprzężenia i fizycznej
repropagacji własnego anchoru, nie uznają zapisanej taśmy za dowód dla nowej obserwacji.
Nie sprawdzono całego otoczenia niepewności ani kolejnych dziesiątek obserwacji.
B nie ma przyjętej polityki do dalszego testu; C późne stany również nie.

## 7. Czy ukończono pełne trzy okrążenia S?

**Nie.** Brama A: PARTIAL (nowe C przechodzi1/3 stanów, A3/3). Brama B:
lokalne3/3 kontrole jednej obserwacji, brak kompletnej serii. Bramy C (pierwszy łuk
natywnie), D (pierwsze okrążenie) i E (trzy okrążenia) nieuruchomione zgodnie z zakazem
promocji mniej niezawodnego generatora. Nowych światów natywnych #155:0.

Dołączony rzeczywisty zapis natywny jest niezmienionym odniesieniem #154 S0:
13,8 s,69 decyzji,0 okrążeń,0 kontaktów,326,34375 CPU-s sterownika, obrys minimum
0,582069 m i swept minimum0,501181 m. To nie jest osiągnięcie nowego generatora.
Replay oddzielnie oznacza prognozy A/B/C, pełne polityki oraz legalne prefiksy;
po końcu prefiksu nie ekstrapoluje ruchu. Szare tło to wyłącznie native-reference #154.

## 8. Jakie są koszty CPU i mechanizmy niepowodzeń?

W końcowym B0 najdalszy zachowany prefiks kończy się przy45,134 m /4,2 s, β34,42°,
yaw2,378 rad/s; następne propozycje przekraczają β45. B6,6 dochodzi do143,074 m,
β41,47°, a dalsze propozycje tracą obrys albo dopuszczalny poślizg. B13,8 dochodzi
do270,106 m i traci obrys/sweep w następnym etapie. Krótkie ETA preferuje postęp przed
zachowaniem możliwości odkręcenia yaw i opanowania β.

C poprawia wybór etapów, ale nadal traci wszystkie rozszerzenia ograniczonej wiązki:
z6,6 najdalszy legalny prefiks163,675 m /11,4 s (cel191,389, safety350,779),
z13,8:295,460 m /19,2 s (cel350,779, safety510,168). W ostatnim przypadku β24,95°,
yaw1,547 rad/s. Końcowe dane obejmują odrzucenia pose/sweep, nieodwracalnego prefiksu
i brak świadków w słowniku. To ograniczenie propozycji/wiązki w stanie z już aktywną
kolejką, nie dowód fizycznej niemożliwości toru ani sukces wynikający z krótkiego prefiksu.

Pełne profile wszystkich prób są w `cpu-phases155.csv` i oryginalnych Review.Profile.
C0: primitive_physics3,59375 s, necessary_irrevocable_prefix1,9375 s,
hierarchical_generation1,375 s, physical_stage_reachability0,515625 s,
independent_preflight0,171875 s; pozostałe fazy w CSV. Bufor diagnostycznych odrzuceń
ma limit300; jeśli pełny, raport traktuje zliczenia przyczyn jako próbkę, nie pełną populację.
Podsumowania liczników i wszystkie negatywne wersje v1–v4 zachowano.

Budżet eksperymentalny:1200 CPU-s, bez rozszerzenia do1500. `budget.json` zawiera
koszt wszystkich23 prób, trzech kontroli obserwacji, odtworzenia fizycznych hashy,
analiz i eksportu. Ostateczna suma jest w zewnętrznym potwierdzeniu walidacji; procesy
build/test CI są walidacją kodu, nie nowymi eksperymentalnymi przejazdami.
Zatrzymanie wynika z niezawodności, nie wyczerpania budżetu. Nie uruchamiano CEM,
losowego treningu, arbitralnego podnoszenia30 CPU-s/32768 prognoz/26 s ani fallbacku K1.

Końcowy przegląd API zablokował odziedziczenie trybu legacy: konstruktor włączony bez opcji wybiera S0,5 m i niezależną walidację, a niezgodne jawne opcje są odrzucane. Wszystkie pomiary miały te ścisłe opcje już przed poprawką; ich ścieżka obliczeń pozostaje identyczna. Cztery testy nowej klasy ponownie przeszły po zmianie.

## Dane, odtwarzanie i walidacja

- `analysis.json`, `measurements155.csv`, `cpu-phases155.csv`: pełna macierz i profile.
- `[ABC]-*.json.gz`, `stability-*.json.gz`: certyfikaty, stany/kolejki, rezerwy, błędy.
- `replay.json.gz`: fizycznie odtworzone gęste prognozy; hashe przyjętych polityk
  sprawdzono podczas eksportu przez niezmieniony Program154.Restore/Runtime153.
- `sources/*.zip` + SHA-256: dokładne źródła każdej wersji uruchomienia.
- `source-proof.json`:676 zamrożonych blobów kodu/danych/starych narzędzi oraz59
  historycznych drzew eksperymentów, sprawdzane bez potrzeby pobierania bazowego commitu w płytkim CI.
- Interaktywny replay: `tools/replay-viewer/hierarchy155.html`; serwuj repo przez
  `python -m http.server 8035 --bind 127.0.0.1` i otwórz ten plik po HTTP.

Przed publikacją wymagane: restore z lockfile, Release build bez ostrzeżeń, C# core i
trajectory, pełna kolekcja testów Python, polityka shardów, Node replay oraz rzeczywisty
Windows Draft CI na końcowym SHA. Podsumowanie wykonanych lokalnych kontroli jest w
`local-validation.json`. Gotowy raport dostarczony poza repo zawiera potwierdzenie CI.
'''
  p=D/'REPORT.md'
  with p.open('x',encoding='utf-8',newline='\n') as f:f.write(report)
  print('Report155 written; source CI receipt remains external to avoid self-referential SHA.')
if __name__=='__main__':main()
