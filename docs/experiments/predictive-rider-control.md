# Predictive Rider Control — PARTIAL, domyślnie OFF

Predyktor trafnie odtwarza krótką odpowiedź istniejącej fizyki, ale jeden zamrożony
wariant sterownika **nie poprawił pełnego przejazdu**. Kandydat ukończył dwa łuki
w 20,27 s zamiast 19,98 s (+0,29 s, +1,45%), z większym maksymalnym β i większą
liczbą zmian kierunku skrętu. Nie należy go włączać domyślnie ani przedstawiać jako
poprawy osiągów. Status: **eksperyment / PROVISIONAL / PARTIAL**.

## Baza i zakres

Baza to dokładnie HEAD #124 `d8d7e959eab00d0b41236aa6292ab04f3c15f3fa`, rodzic
#123 `c625de6faaeafb1d1ddb86f0598b61642c1452e5`. #124 analizuje zapis #123;
nie jest nowym przejazdem. Historia i SHA sprawdzone w Git i metadanych PR:

| PR | HEAD | Znaczenie dla audytu |
| --- | --- | --- |
| #113 | `fc5d184e95fa3d6d12c27abd59ab8de7e2630bee` | fizyczna kierownica, ±35°, 90°/s |
| #115 | `d1325442e08233a9f44279c27410321f62a4a6e0` | osobna odnoga; pomija reakcję, narusza margines zewnętrzny; nie przeniesiono |
| #117 | `29cece47bc8fc7dfe6e678c08b3a0c8e5c837a89` | chwilowa inwersja χ oraz yaw/β |
| #119 | `8a218fc8cb8be3460c8e42116789ece8ff576498` | diagnoza ograniczeń kierownicy |
| #120 | `46129d74cf725c33d67040663262062ac9067281` | samo zawężenie kandydatów nie rozwiązało problemu |
| #122 | `f9d4dd7d8d09a755830c716c3f34af9e7daecc58` | przygotowana linia i przyczynowa polityka gazu |
| #123 | `c625de6faaeafb1d1ddb86f0598b61642c1452e5` | dwa łuki, jeden świat, 2002 interwały |
| #124 | `d8d7e959eab00d0b41236aa6292ab04f3c15f3fa` | audyt utraty prędkości i linii |

Nie zmieniono `src/CoreSim`, silnika, opon, µ, masy 147 kg, bezwładności
52,72097 kg·m², nawierzchni, geometrii ani historycznych danych. Zmiany istniejącego
narzędzia obejmują cztery jawnie chronione rozszerzenia: fork pamięci przygotowanego
planu, flagę OFF, opcjonalne wywołanie predyktora oraz diagnostyczne podanie wspólnego
drugiego planu. Domyślna serializacja pomija flagę OFF. Stary fingerprint jest identyczny.
Viewer przeniesiono plikami z zachowanego Draft #125 (`bd03eaf81419f06637428f82b96bad02d5870540`),
bez merge jego gałęzi lub fizyki. Ten eksperyment otrzymuje kolejny numer PR zgodnie
z decyzją użytkownika.

## A — czy nieosiągalny kąt tłumaczy problem?

**Nie potwierdzono tej diagnozy jako głównej przyczyny w #123.** #117 przeszukuje
±35°, lecz ocenia także odpowiedź po fizycznym ograniczeniu skrętu. Nie zakłada,
że żądany skrajny kąt został już wykonany. Ograniczenie zakresu może zmienić wybór
na siatce, ale nie dowodzi poprawy przyszłej jazdy.

Sprawdzono 21 natywnych stanów: 1,4–2,8 s, 5,3–6,7 s i 10,4–11,2 s, co 0,2 s.
Odtworzenie sił i kierownicy A z oryginalnego stanu ma maksymalny błąd **0**.
Tylko próbka 5,3 s była ograniczona szybkością: A żąda −35°, zaakceptowany koniec
to około +1,70°; B, ograniczone do osiągalnego zakresu, wykonuje ten sam kąt.
Wejście 1,4–2,8 s nie jest ograniczone szybkością. Różnice A/B są małe; największa
redukcja chwilowego kosztu B wynosi około 0,000247 przy 5,5 s.

Pełna tabela A/B jest w [steering-audit.json](predictive-rider-control/steering-audit.json):
żądany, kontaktowy i końcowy kąt, wektory sił obu kontaktów, moment, χ̇, projekcja
wzrostu β, rzeczywiste natywne zmiany χ/yaw/β, błąd normalny, wykorzystanie opon
i ograniczenie kierownicy. Jednostki sił N, momentu N·m, kątów rad, szybkości rad/s.

#120 poprawił lokalny koszt tylko około 0,04425% w swoim porównaniu #119,
skręcił w niekorzystną stronę podczas odzyskiwania linii i przekroczył kredę
w 6,71 s. Ten wynik dotyczy jego starszego scenariusza. Zawężenie tej samej chwilowej
funkcji celu może usunąć błąd wyboru siatki, nadal wybierając złą sekwencję ruchu.
Nie skopiowano #120; B służy jedynie do audytu. Próbę predykcji potraktowano jako
osobny, falsyfikowalny test kosztu przyszłego ruchu, mimo braku potwierdzenia prostej
hipotezy ogranicznika. Wynik PARTIAL nie rozstrzyga, że lepszy sterownik już istnieje.

## B — predykcja zweryfikowana przed jazdą ON

[protocol.json](predictive-rider-control/protocol.json) zamrożono przed oceną.
63 prognozy = 21 stanów × 0,3 / 0,6 / 0,9 s, krok 0,01 s. Model startuje z natywnego
XY, vx/vy, ψ, yaw rate, zaakceptowanego skrętu, ω tylnego koła, aktywnego Drive,
planu, pamięci przeglądów oraz tylko już zaobserwowanego pending.

Model korzysta z niezmienionych `TwoContactBikeDynamics`, `RearWheelDynamics`,
#117 i polityki `PreparedCornerPlan`. Lekki krok ciała przybliża utrzymaną siłę
w czterech podkrokach Box2D współczynnikiem (N+1)/(2N). To przybliżenie,
nie deklarowana tożsamość z Box2D. Nie tworzy światów dla kandydatów i nie zapisuje
przewidzianego stanu do rzeczywistego motocykla. Tylko kopie koła, kierownicy
i skalarnej pamięci planu są zatwierdzane podczas predykcji.

Nie podawano modelowi przyszłego zarejestrowanego gazu. Przyczynowo wykonuje
istniejący pending, następnie generuje przyszłe przeglądy oryginalną polityką.
Nowy skręt może zacząć działać dopiero po pełnej reakcji **0,24 s**, po zachowaniu
nieodwołanego gazu. Np. polecenie oczekujące w 10,4 s aktywuje się w 10,64 s.

| Błąd maksymalny | Limit przed próbą | 63 prognozy baseline | 42 wybrane prognozy kontra rzeczywisty ON |
| --- | ---: | ---: | ---: |
| XY [m] | 0,10 | 0,000291 | 0,000183 |
| prędkość [m/s] | 0,20 | 0,000182 | 0,000129 |
| χ [°] | 1,0 | 0,00269 | 0,00330 |
| β [°] | 1,0 | 0,00201 | 0,00291 |
| kierownica [°] | 1,5 | 0,01710 | 0,01710 |
| ω koła [rad/s] | 2,0 | 0,00362 | 0,00224 |

Wszystkie horyzonty i próbki przechodzą; dodatkowy limit optymizmu prześwitu
0,10 m i 5000 ocen kontaktu na kandydata również. Dane:
[prediction-validation.json](predictive-rider-control/prediction-validation.json),
pełne sparowane trajektorie w `prediction-trajectories.json.gz` oraz niezależne
porównanie faktycznie wybranych akcji ON w [analysis.json](predictive-rider-control/analysis.json).
Dokładność dotyczy tych krótkich kontynuacji, stałej nawierzchni i tej fizyki;
nie jest certyfikatem dla innych torów, kolizji ani dowolnej kolejki planów.

Mediana jednej prognozy 12,96 ms, maksimum 33,76 ms; bramka szacowanych trzech
50/150 ms przeszła. Przygotowanie 63 osobnych planów do audytu zajęło 6,274 s
oddzielnie; runtime używa forka istniejącego planu. Rzeczywista bramka trzech
akcji w C: mediana 45,79 ms, maksimum 72,11 ms. Pomiar jest diagnostyką jednego
komputera, bez szerokiego benchmarku.

## C — jeden wariant bez dostrajania po wyniku

`EnablePredictiveRiderControl=false`. Co 0,4 s porównuje dokładnie trzy akcje:
oryginalny #117, korektę +0,9° i −0,9°. Korekta jest celem po reakcji 0,24 s,
utrzymywanym przez 0,1 s i wykonanym przez istniejący fizyczny ogranicznik 90°/s.
Przed reakcją wszystkie prognozy mają ten sam ruch. Nie zmienia gazu ani geometrii.

Horyzont 0,6 s ustalono według skali czasu reakcji, przed pełną jazdą. Koszt obejmuje
średni kwadrat błędu normalnego i χ względem planu, dodatni wzrost |β|, postęp
oraz karę za odwrócenie korekty. Wymaga poprawy 0,01 m². Każda próbka predykcji
sprawdza pełne kapsuły, konserwatywną obwiednię między krokami +0,10 m niepewności,
opony i szybkość kierownicy. Brak certyfikowanej opcji nie jest ogłaszany bezpiecznym
fallbackiem; pozostaje oryginalny regulator i obowiązuje natywny stop bezpieczeństwa.
Przy nieprzewidywanym przełączeniu planu zachowuje oryginalny sterownik.

21 diagnoz C i wszystkie trzy prognozy zapisano w `decision-validation.json`
i `decision-forecasts.json.gz`. Dodatkowy `candidate-response-audit.json` podaje
siły, moment i ograniczenia na początku działania każdej prognozowanej akcji,
oraz przewidziane zmiany χ/yaw/β i błąd końcowy. Są to **prognozy**, a nie dodatkowe
natywne przejazdy alternatywnych akcji. Niezależna kontynuacja ON weryfikuje akcje
faktycznie wybrane. Zamrożone źródła modelu i kosztu są chronione hashami.

## D — kontrolowane porównanie natywne

**Jawne odstępstwo od limitu prób:** początkowo przewidziano jeden baseline OFF
i jeden ON. Pierwszy ON ukończył bezpiecznie w 20,28 s, lecz oryginalny dynamiczny
planner przygotował nieco inny Q2 (10,19 s i splice 137,7063 m zamiast 9,91 s
i 137,5302 m). Ten zapis pozostaje w głównym katalogu danych, fingerprint
`C953F8AC60CE60FB6B6EEAE8CF4833C5269EAE8707D0F844E7654B487DB4BE8A`, i jest
**wykluczony z porównania tej samej linii**.

Wykonano dokładnie jedno dodatkowe ON po zamrożeniu wejścia porównania:
oryginalnego Q1/Q2 oraz przygotowania 9,91 s i aktywacji 10,15 s. Łącznie trzy
światy, po jednym świecie na przejazd. Nie zmieniono modelu, kosztu, horyzontu,
progów, fizyki lub tolerancji. Dokumentuje to
[controlled-comparison-protocol.json](predictive-rider-control/controlled/controlled-comparison-protocol.json).
To korekta warunków porównania, nie pierwotnie spełniony limit dwóch światów.

Wspólny Q2 jest wejściem geometrii i zegara eksperymentu. Nie importuje rzeczywistego
baseline XY, prędkości, gazu, koła lub kierownicy do wykonywanego ON. Pamięć gazu
i pending są przenoszone z faktycznego ON. Harmonogram jest diagnostyczny:
kandydat mija pierwszy łuk całym obrysem dopiero w 10,19 s, więc przygotowanie Q2
w 9,91 s nie pochodzi z jego własnego zdarzenia minięcia. Domyślne replanning OFF
zachowuje oryginalne zachowanie. Geometria wspólnego prefiksu nie skacze.

| Metryka | Baseline #124 / #123 | Kandydat, wspólny plan |
| --- | ---: | ---: |
| Do końca drugiego łuku, cały obrys [s] | 19,98 | **20,27** |
| Interwały / światy na wykonanie | 2002 / 1 | 2031 / 1 |
| Łuk 1 wejście / minimum / wyjście [km/h] | 68,62 / 45,48 / 54,92 | 68,61 / 44,07 / 56,26 |
| Łuk 2 wejście / minimum / wyjście [km/h] | 66,33 / 57,74 / 57,84 | 66,93 / 57,78 / 57,88 |
| Błąd linii max / RMS, cały przejazd [m] | 5,906 / 1,876 | 5,375 / 1,868 |
| Błąd max / RMS, łuk 1 [m] | 5,906 / 2,770 | 5,375 / 2,757 |
| Błąd max / RMS, łuk 2 [m] | 1,475 / 0,766 | 1,435 / 0,750 |
| Maksymalny |β| [°] | 12,25 | **13,86** |
| Praca silnika [kJ] | 80,121 | 79,685 |
| Strata przednia / tylna boczna / tylna wzdłużna [kJ] | 18,180 / 17,874 / 15,400 | 18,343 / 18,151 / 14,525 |
| Łożysko / praca oporów [kJ] | 1,219 / −32,459 | 1,213 / −32,501 |
| Średni Drive / czas Drive = 0 | 15,55% / 7,80 s | 15,65% / 7,30 s |
| Komendy gazu / odwrócenia skrętu | 48 / 52 | 50 / **62** |
| Nowe korekty skrętu: decyzje / aktywacje | 0 / 0 | 10 / 10 |
| Pending na końcu / spóźnione | 1 / 0 | 1 / 0 |
| Dodatni gaz po nowszej intencji zero | 6 | 5 |
| Min konserwatywny prześwit kreda / banda [m] | 1,490 / 1,735 | 1,490 / 1,807 |
| Min banda w drugim łuku [m] | 3,769 | **3,309** |
| Rzeczywista droga XY / postęp referencji [m] | 331,834 / 288,982 | 334,078 / 288,880 |
| Natywne oceny sił / pomocnicze przy granicy krzywizny | 2002 / 5 | 2031 / 7 |
| Oceny kontaktu oryginalnej inwersji #117 | 102102 | 98481 |
| Oceny przygotowanej polityki gazu | 2600 | 2639 |
| Dodatkowe oceny kontaktu predykcji | 0 | **359358** |
| Dodatkowy CPU predykcji łącznie [ms] | 0 | **1668,45** |
| Mediana / maksimum trzech akcji [ms] | — | 36,60 / 68,56 |

Prędkości wejścia/wyjścia interpolowano offline pomiędzy sąsiednimi natywnymi
próbkami na tych samych stacjach toru. RMS jest ważony czasem; droga XY sumuje
odcinki natywnych pozycji. Koniec oznacza przekroczenie tej samej płaszczyzny całym
obrysem, więc końcowy postęp CoM może nieznacznie różnić się z orientacją motocykla.
Nie ma skrótu poza torem. Pomiary solvera 44,28/29,38 ms i prepared 247,89/232,10 ms
są pojedynczymi, zależnymi od rozgrzania pomiarami; nie są dowodem przyspieszenia.
Poza powyższymi query counts są dwie pomocnicze oceny lifecycle przygotowania planu.

Oba przejazdy zachowują dodatni konserwatywny prześwit, fizyczne limity opon,
90°/s i pełne 0,24 s reakcji. Brak energii ponad pracę silnika. Niezależny bilans
impulsów tłumaczy resztę energii numerycznej (0,09772 J / 0,05644 J), a niewyjaśniony
błąd ma wartość bezwzględną <10⁻⁶ J. Kandydat pozostaje kontrolowany, lecz większe β,
więcej odwróceń i mniejszy margines drugiego łuku wykluczają twierdzenie, że wszystkie
istotne metryki bezpieczeństwa uległy poprawie.

Baseline OFF fingerprint:
`33741F49880FADBCA5C7ECE240B32EF0017463A30B1BF85B32E06131D5CD4A03`.
Kontrolowane ON:
`A367628ED457B6C075B059E9811C485A242D1C80A9E102519CFBC266675CA316`.
Każdy interwał sił/skrętu został odtworzony z jego natywnego stanu bez nowego świata.
Powtarzalność samego predyktora jest sprawdzona osobno; nie deklarujemy drugiego
powtórzenia pełnego natywnego ON, którego celowo nie uruchomiono.

## Otwórz rzeczywistą jazdę na Windows

Z głównego katalogu tego checkoutu:

```powershell
py -m http.server 8001 --bind 127.0.0.1
```

- [Kandydat PARTIAL](http://127.0.0.1:8001/tools/replay-viewer/?case=predictive-candidate)
- [Baseline #123/#124](http://127.0.0.1:8001/tools/replay-viewer/?case=predictive-baseline)

Domyślnie pauza, najazd pierwszego łuku, 0,25×, zbliżenie, wygładzanie OFF.
Selektor przełącza osobne przejazdy jednego zawodnika. Łuk 1/2, Play/Pause,
tempo, kroki, suwak, zoom, śledzenie i cały tor pochodzą z istniejącego viewera.
Nowe znaczniki pokazują podjęcie i **rzeczywistą aktywację** skrętu, oddzielnie od gazu.
Aktualny Drive i skręt pochodzą z natywnych CSV, a nie z intencji AI.

Turkus: rzeczywiste XY i motocykl 2,10 m. Zielona przerywana: zapisany wspólny plan.
Złota strzałka: heading, niebieska: rzeczywiste v ×0,2 s, różowy łuk: β.
Kreda biała, banda złota. Plan przełącza się w 10,15 s; motocykl jedzie bez resetu.
Nie rysujemy prognoz jako rzeczywistej jazdy. Historyczny czteroosobowy #95 pozostaje
osobnym przejazdem oznaczonym starszą fizyką. Opcjonalny przycisk zapisuje klip WebM.

## Celowana walidacja

```powershell
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter "FullyQualifiedName~PredictiveRiderControlTests|FullyQualifiedName~ContinuousCornerPlanningTests" --warnaserror
python -m unittest discover -s tests/calibration -p test_predictive_rider.py
python -m unittest discover -s tests/calibration -p test_continuous_corners.py
node --test tools/replay-viewer/replay.test.mjs
```

18 testów C# PASS; 5 nowych kontroli danych i 6 kontroli historycznego kontraktu
PASS; 26 testów importu/replaya PASS. Testy obejmują niezmienianie zaakceptowanego
stanu, pending i reakcję, deterministyczność predykcji, limity fizyczne, oryginalny
fingerprint OFF, każdy kontakt obu przejazdów, wspólną geometrię i wszystkie natywne
XY/heading/t/Drive/kierownicę kandydata. Brak nowych historycznych macierzy lokalnych.
Standardowe Windows CI należy oceniać na dokładnym HEAD Draft PR; jego link i wynik
są w opisie PR. Stan przeglądarki i wykonane czynności zapisano w `validation.json`.

`predictive-rider-report.py` ponownie wylicza wyłącznie analizę zapisanych danych;
nie uruchamia symulacji. `probe.csproj` dokumentuje jednorazowe fazy eksperymentu;
pliki attempt blokują ich ciche ponowienie. Hash manifest obejmuje nowe dane.
Historyczny source guard nadal sprawdza oryginalne hashe fizyki; cztery jawne
rozszerzenia integracji mają osobne hashe i wymagają identycznego baseline OFF.

## Następny krok wynikający z dowodów

Pozostawić wariant OFF. Przed kolejną jazdą zbadać offline, dlaczego zamrożony koszt
0,6 s akceptuje korekty, które obniżają lokalny błąd, ale zwiększają straty prędkości,
β i liczbę odwróceń w pierwszym łuku. Dokładniejszy model sam nie naprawia celu
sterowania. Dalszy krok wymaga nowego, jawnego protokołu oceny kosztu sekwencji;
ten PR nie rozpoczyna strojenia ani optymalizacji linii.
