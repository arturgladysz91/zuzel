# Full Heat Validation: Four Riders and Solo Time Trials (#92)

**PROVISIONAL — B: CZĘŚCIOWO.** Wszystkie 8 głównych biegów kończy się czterema ukończonymi okrążeniami; 0 przerwanych, 0 DNF, 0 kontaktów i 0 wyjazdów poza tor. Tak samo kończą się 2 kontrole OFF i 33 próby solo. Są potwierdzone minięcia po pierwszym łuku, lecz dominacja pola A, ostrożne odpuszczanie gazu, brak obron w tym zestawie i niepodłączony kanoniczny Start nie pozwalają uznać modelu za gotowy. Nie zmieniono mechaniki, parametrów ani scoringu, aby poprawić wynik.

Baza: Draft #91, `experiment/tactical-decision-quality`, HEAD `a56a2b777ca28274c2ea227f51cbfb1ee087a60b`. Zakres: jeden moduł pomiarowy, sześć testów jego miar, wejście CLI i jawna odpowiedzialność testów w istniejącym CI. Produkcyjne `src/`, `HeatSimulator`, Box2D, regulator, nawierzchnia, geometria, progi Crash oraz algorytmy #89–#91 pozostają identyczne z bazą.

## Metoda i odtwarzanie

[Pełna konfiguracja i zamrożone profile](full-heat-and-solo-validation/configuration.json). Motoarena Toruń: referencja okrążenia 318.778749943 m, promień 31 m, proste 35 + 62 + 27 m, szerokość prostej 12 m i łuku 16.6 m. Rzeczywiste środki pól A/B/C/D mają offsety 0.5/3.5/6.5/9.5 m od wewnętrznej referencji. Start: referencja 0 m, prędkość 0. Domowy target każdego zawodnika jest środkiem jego pola; to ten sam styl, a różne początkowe linie wynikają z pól.

Jeden świat Box2D.NET 3.1.654, krok 10 ms, 4 substeps na wywołanie solvera; istniejące dodatkowe podziały przy zbliżeniu, krzywiźnie i deadline reakcji pozostają aktywne. AI co 100 ms. W głównych biegach Tactical=true, SideBySide=true, Quality=false. W kontrolach Tactical=false, SideBySide=false, Quality=false, z dotychczasową reaktywną ochroną kontaktową. W solo te trzy flagi są false i TrafficEnabled=false, bez rywali i komend. Zmiana polityki AI nie zmienia wersji fizyki.

Każda komórka nawierzchni: Grip=1, Ruts=0, Moisture=0.5, EffectiveGrip=0.9175; powierzchnia zamrożona przez WorldRunner. Ten sam motocykl referencyjny, masa zawodnika 70 kg, BikeSetup.Neutral (0.5/0.5), legacy RiderStyle.Balanced i PreferredLine.Neutral. Brak losowej formy, zużycia, nowych band, systemu sędziowskiego i powtórek. Limit 90 s świata; `Run(... requiredLaps:4, stopOnCrash:true)`. Osiem rotacji oraz profile ustalono przed smoke; bez dobierania kolejności mety.

Najpierw zakończono jeden solo i jeden pełny heat w Debug. Dopisanie diagnostyki nie zmieniło modelu. Finalne, zachowane dane pochodzą z Release, .NET 8.0.26, Windows 10, SDK 10.0.101. Etapy: smoke → heats → solo → repeat → benchmark. Niezależne świeże procesy powtórzyły pięć wybranych prób z odwróconą kolejnością wejściową. Benchmark ma osobny proces z DOTNET_TieredCompilation=0, aby wczesne przełączanie kodu JIT nie udawało kosztu diagnostyki; hash fizyczny pozostał identyczny. Pierwsza próba benchmarku z domyślnym tieringiem wykazała taki dryf i nie służy do wnioskowania o narzucie.

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
# Każdy etap to oddzielny proces; smoke jest bramką przed kolejnymi etapami.
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll full-validation docs/experiments/full-heat-and-solo-validation smoke
# Ten sam prefiks polecenia: heats, solo, repeat; następnie benchmark z DOTNET_TieredCompilation=0.
python tools/shared-world-speedway/full-validation-report.py
```

Obserwator interpoluje czas przecięcia kolejnych wielokrotności `Frame.LapMeters` na `TotalReferenceMeters` między dwoma zaakceptowanymi stanami. Nie modyfikuje motocykla i nie resetuje go do okrążenia lotnego. Pierwsze okrążenie zawiera reakcję i przyspieszanie; lotne to osobno okrążenia 2/3/4. Czwarta suma zgadza się z produktem WorldRunner do 1e-8 s. Sześć cyfr po przecinku ujawnia małe różnice numeryczne, a nie deklarowaną mikrosekundową dokładność fizyki 10 ms.

Minięcie wymaga ustalonego wcześniejszego lidera całym motocyklem, rzeczywistej fazy obok siebie, przejścia drugim całym motocyklem z zapasem ≥0.15 m i utrzymania tego przez ≥1 s oraz ≥10 m. Projekcja obejmuje istniejące kapsuły podwozia i kierownicy (`TacticalAudit.Extent/ClearGap`). Pary porównujemy lokalnie (oba dystanse <20 m, zgodne styczne >0.9), z kontrolą rozwiniętej referencji, aby nie zaliczać motocykli po przeciwnej stronie toru lub zdublowanych. Pierwsze rozdzielenie pól tylko ustala lidera. Krótkie zmiany referencji i kolejność ID przy remisie nie są minięciami. Miara jest konserwatywna: nie liczy niespełniających czasu utrzymania minięć tuż przed metą.

W JSON są czasy, istotne komendy i stany po ich aktywacji, rzeczywiste zmiany porządku referencji (jawnie odrębne od minięć), kontakty/konsekwencje i anomalie. Czasy pomiarów kończą się przy własnej mecie, choć ciało jedzie dalej do ukończenia ostatniego zawodnika; klasyfikacja obserwatora zamraża ukończonych według czasu przecięcia. Dwie trajektorie co 0.1 s: [A1/0](full-heat-and-solo-validation/A1-rotation-0-trajectory.csv), [A2/0](full-heat-and-solo-validation/A2-rotation-0-trajectory.csv); żadnych pełnych snapshotów co 10 ms w plikach zbiorczych.

Gdy wystąpi Crash, status całej próby to Interrupted-Crash, z czasem, wszystkimi pozycjami/prędkościami i uczestnikami w CrashSnapshot oraz ContactEvent/ConsequenceObservation; bez hurtowego DNF i bez automatycznej powtórki. Ten tryb sprawdza krótka istniejąca próba Scenarios.E w regresji, nie jest dodatkowym pełnym biegiem. OutsideTrack jest osobną anomalią geometryczną, niezależną od wystąpienia kontaktu; w badanych biegach nie wystąpiła. Min. prześwit dotyczy całego footprintu i istniejącej lokalnej geometrii granic, bez założenia fizycznych band.

## A. Pełne biegi

Kolejność kanonicznych liczb: Reaction/Start/Technique/TrackReading/Attack/Defense/PairRiding/Strength. Legacy: Start/Speed/SlideControl; pozostałe trzy legacy zawsze 50. A1: wszystkie osiem i wszystkie legacy =50, combativeness=0.5. A2 poniżej; masa, setup i pozostałe preferencje wspólne. R2/R3 mają także różne legacy Speed, więc ich wynik nie jest izolowanym dowodem działania Technique/Attack.


| A2 | Kanoniczne | Legacy S/Sp/SC | Combativeness |
|---|---|---|---|
| R1 | 80/80/50/50/50/45/50/55 | 80/50/50 | 0.5 |
| R2 | 50/50/85/70/55/55/50/60 | 50/55/80 | 0.5 |
| R3 | 55/55/60/50/85/45/50/70 | 55/65/60 | 0.8 |
| R4 | 60/55/70/65/45/85/50/65 | 55/50/70 | 0.3 |


### Wyniki i geometria pól


| Bieg | Pole A | Pole B | Pole C | Pole D | Wynik | Czas zwycięzcy s | Wyprzedzenia | Kontakty | Crash |
|---|---|---|---|---|---|---|---|---|---|
| [A1-rotation-0](full-heat-and-solo-validation/A1-rotation-0.json) | R1 | R2 | R3 | R4 | R1 > R2 > R3 > R4 | 60.532 | 0 | 0 | 0 |
| [A1-rotation-1](full-heat-and-solo-validation/A1-rotation-1.json) | R4 | R1 | R2 | R3 | R4 > R1 > R2 > R3 | 60.532 | 0 | 0 | 0 |
| [A1-rotation-2](full-heat-and-solo-validation/A1-rotation-2.json) | R3 | R4 | R1 | R2 | R3 > R4 > R1 > R2 | 60.532 | 0 | 0 | 0 |
| [A1-rotation-3](full-heat-and-solo-validation/A1-rotation-3.json) | R2 | R3 | R4 | R1 | R2 > R3 > R4 > R1 | 60.532 | 0 | 0 | 0 |
| [A2-rotation-0](full-heat-and-solo-validation/A2-rotation-0.json) | R1 | R2 | R3 | R4 | R2 > R1 > R3 > R4 | 60.173 | 1 | 0 | 0 |
| [A2-rotation-1](full-heat-and-solo-validation/A2-rotation-1.json) | R4 | R1 | R2 | R3 | R4 > R1 > R2 > R3 | 59.817 | 0 | 0 | 0 |
| [A2-rotation-2](full-heat-and-solo-validation/A2-rotation-2.json) | R3 | R4 | R1 | R2 | R3 > R4 > R2 > R1 | 59.142 | 1 | 0 | 0 |
| [A2-rotation-3](full-heat-and-solo-validation/A2-rotation-3.json) | R2 | R3 | R4 | R1 | R2 > R3 > R4 > R1 | 59.154 | 0 | 0 | 0 |
| [A1-control-OFF](full-heat-and-solo-validation/A1-control-OFF.json) | R1 | R2 | R3 | R4 | R1 > R2 > R3 > R4 | 60.532 | 0 | 0 | 0 |
| [A2-control-OFF](full-heat-and-solo-validation/A2-control-OFF.json) | R1 | R2 | R3 | R4 | R2 > R1 > R3 > R4 | 60.042 | 1 | 0 | 0 |


Główne: 8/8 ukończonych, 0 przerwanych. Kontrole osobno: 2/2, 0 przerwanych. W A1 cały wynik podąża za polem: A/B/C/D = 60.532/61.779/63.131/64.566 s w każdej rotacji. Kontrola bez taktyki: 60.532/61.432/62.356/63.295 s. Samo AI kosztuje odpowiednio około 0.000/0.347/0.774/1.271 s dla tych pól. A2: zwycięża R2 dwukrotnie, R3 i R4 po razie, każdorazowo zawodnik startujący z A lub B. Mocniejszy start R1 nie gwarantuje zwycięstwa. To obserwacje czterech prób na zestaw, bez estymacji prawdopodobieństw zwycięstwa.

### Każdy zawodnik: ukończenie, okrążenia i walka

Okrążenia oraz meta w sekundach; średnia i maksimum w m/s. A/D/S oznacza aktywowane intencje ataku, obrony i side-by-side (w tym Hold). Logiczne Hold może nie zmienić gazu ani targetu. Min. prześwit granic i odstęp całych motocykli w metrach; odstęp ujemny oznaczałby nakładanie, nie samą różnicę referencji. Δmeta to odstęp czasowy do zwycięzcy, nie fizyczny dystans ciał po ich mecie.


| Bieg | Zawodnik/pole | Meta / Δmeta s | L1 / L2 / L3 / L4 s | Śr./max m/s | A/D/S | Minięcia | Kontakty | Min. granica / rywal m |
|---|---|---|---|---|---|---|---|---|
| A1-rotation-0 | R1/A | 60.532 / 0.000 | 16.228 / 14.768 / 14.768 / 14.768 | 21.266 / 23.339 | 0/0/2 | 0 | 0 | 1.092 / 2.200 |
| A1-rotation-0 | R2/B | 61.779 / 1.247 | 16.835 / 14.983 / 14.980 / 14.980 | 22.056 / 24.190 | 0/0/1 | 0 | 0 | 4.095 / 2.200 |
| A1-rotation-0 | R3/C | 63.131 / 2.599 | 17.460 / 15.273 / 15.199 / 15.199 | 22.778 / 25.007 | 0/0/1 | 0 | 0 | 3.985 / 2.200 |
| A1-rotation-0 | R4/D | 64.566 / 4.034 | 17.945 / 15.777 / 15.422 / 15.422 | 23.439 / 25.795 | 0/0/1 | 0 | 0 | 0.995 / 2.200 |
| A1-rotation-1 | R1/B | 61.779 / 1.247 | 16.835 / 14.983 / 14.980 / 14.980 | 22.056 / 24.190 | 0/0/1 | 0 | 0 | 4.095 / 2.200 |
| A1-rotation-1 | R2/C | 63.131 / 2.599 | 17.460 / 15.273 / 15.199 / 15.199 | 22.778 / 25.007 | 0/0/1 | 0 | 0 | 3.985 / 2.200 |
| A1-rotation-1 | R3/D | 64.566 / 4.034 | 17.945 / 15.777 / 15.422 / 15.422 | 23.439 / 25.795 | 0/0/1 | 0 | 0 | 0.995 / 2.200 |
| A1-rotation-1 | R4/A | 60.532 / 0.000 | 16.228 / 14.768 / 14.768 / 14.768 | 21.266 / 23.339 | 0/0/2 | 0 | 0 | 1.092 / 2.200 |
| A1-rotation-2 | R1/C | 63.131 / 2.599 | 17.460 / 15.273 / 15.199 / 15.199 | 22.778 / 25.007 | 0/0/1 | 0 | 0 | 3.985 / 2.200 |
| A1-rotation-2 | R2/D | 64.566 / 4.034 | 17.945 / 15.777 / 15.422 / 15.422 | 23.439 / 25.795 | 0/0/1 | 0 | 0 | 0.995 / 2.200 |
| A1-rotation-2 | R3/A | 60.532 / 0.000 | 16.228 / 14.768 / 14.768 / 14.768 | 21.266 / 23.339 | 0/0/2 | 0 | 0 | 1.092 / 2.200 |
| A1-rotation-2 | R4/B | 61.779 / 1.247 | 16.835 / 14.983 / 14.980 / 14.980 | 22.056 / 24.190 | 0/0/1 | 0 | 0 | 4.095 / 2.200 |
| A1-rotation-3 | R1/D | 64.566 / 4.034 | 17.945 / 15.777 / 15.422 / 15.422 | 23.439 / 25.795 | 0/0/1 | 0 | 0 | 0.995 / 2.200 |
| A1-rotation-3 | R2/A | 60.532 / 0.000 | 16.228 / 14.768 / 14.768 / 14.768 | 21.266 / 23.339 | 0/0/2 | 0 | 0 | 1.092 / 2.200 |
| A1-rotation-3 | R3/B | 61.779 / 1.247 | 16.835 / 14.983 / 14.980 / 14.980 | 22.056 / 24.190 | 0/0/1 | 0 | 0 | 4.095 / 2.200 |
| A1-rotation-3 | R4/C | 63.131 / 2.599 | 17.460 / 15.273 / 15.199 / 15.199 | 22.778 / 25.007 | 0/0/1 | 0 | 0 | 3.985 / 2.200 |
| A2-rotation-0 | R1/A | 60.441 / 0.268 | 16.136 / 14.768 / 14.769 / 14.768 | 21.298 / 23.340 | 0/0/3 | 0 | 0 | 1.092 / 1.168 |
| A2-rotation-0 | R2/B | 60.173 / 0.000 | 16.309 / 14.760 / 14.553 / 14.552 | 22.360 / 24.794 | 0/0/5 | 1 | 0 | 3.061 / 1.168 |
| A2-rotation-0 | R3/C | 60.937 / 0.763 | 16.453 / 14.729 / 14.731 / 15.024 | 23.033 / 25.632 | 1/0/5 | 0 | 0 | 3.981 / 1.235 |
| A2-rotation-0 | R4/D | 61.119 / 0.945 | 16.900 / 14.697 / 14.660 / 14.862 | 21.994 / 25.845 | 1/0/3 | 0 | 0 | 1.100 / 2.045 |
| A2-rotation-1 | R1/B | 61.339 / 1.521 | 16.396 / 14.981 / 14.981 / 14.981 | 22.215 / 24.192 | 0/0/6 | 0 | 0 | 4.096 / 0.686 |
| A2-rotation-1 | R2/C | 61.431 / 1.614 | 16.588 / 14.975 / 14.952 / 14.916 | 22.233 / 25.626 | 1/0/18 | 0 | 0 | 2.417 / 0.686 |
| A2-rotation-1 | R3/D | 62.267 / 2.449 | 16.696 / 14.983 / 15.353 / 15.235 | 23.917 / 26.176 | 0/0/2 | 0 | 0 | 1.100 / 1.619 |
| A2-rotation-1 | R4/A | 59.817 / 0.000 | 16.048 / 14.590 / 14.590 / 14.590 | 21.520 / 23.638 | 0/0/1 | 0 | 0 | 1.086 / 2.167 |
| A2-rotation-2 | R1/C | 62.737 / 3.595 | 16.660 / 15.665 / 15.212 / 15.199 | 22.922 / 25.009 | 0/0/2 | 0 | 0 | 3.975 / 1.200 |
| A2-rotation-2 | R2/D | 61.681 / 2.539 | 16.725 / 14.986 / 14.985 / 14.985 | 24.145 / 26.159 | 0/0/1 | 1 | 0 | 1.100 / 1.200 |
| A2-rotation-2 | R3/A | 59.142 / 0.000 | 15.890 / 14.418 / 14.417 / 14.417 | 21.766 / 23.925 | 0/0/1 | 0 | 0 | 1.081 / 2.200 |
| A2-rotation-2 | R4/B | 60.508 / 1.366 | 16.319 / 14.730 / 14.730 / 14.730 | 22.125 / 24.214 | 0/0/1 | 0 | 0 | 3.097 / 2.172 |
| A2-rotation-3 | R1/D | 63.799 / 4.644 | 17.503 / 15.452 / 15.422 / 15.422 | 23.721 / 25.796 | 0/0/1 | 0 | 0 | 0.995 / 2.188 |
| A2-rotation-3 | R2/A | 59.154 / 0.000 | 15.909 / 14.415 / 14.415 / 14.415 | 21.761 / 23.939 | 0/0/1 | 0 | 0 | 1.081 / 2.089 |
| A2-rotation-3 | R3/B | 59.818 / 0.664 | 16.154 / 14.555 / 14.555 / 14.555 | 22.380 / 24.509 | 0/0/1 | 0 | 0 | 3.093 / 2.089 |
| A2-rotation-3 | R4/C | 61.910 / 2.755 | 16.855 / 15.018 / 15.018 / 15.018 | 23.227 / 25.309 | 0/0/1 | 0 | 0 | 3.983 / 2.188 |
| A1-control-OFF | R1/A | 60.532 / 0.000 | 16.228 / 14.768 / 14.768 / 14.768 | 21.266 / 23.338 | 0/0/0 | 0 | 0 | 1.092 / 2.200 |
| A1-control-OFF | R2/B | 61.432 / 0.900 | 16.491 / 14.981 / 14.981 / 14.981 | 22.181 / 24.187 | 0/0/0 | 0 | 0 | 4.096 / 2.200 |
| A1-control-OFF | R3/C | 62.356 / 1.824 | 16.758 / 15.199 / 15.199 / 15.199 | 23.062 / 25.009 | 0/0/0 | 0 | 0 | 3.985 / 2.200 |
| A1-control-OFF | R4/D | 63.295 / 2.762 | 17.028 / 15.422 / 15.422 / 15.422 | 23.911 / 25.795 | 0/0/0 | 0 | 0 | 0.995 / 2.200 |
| A2-control-OFF | R1/A | 60.440 / 0.398 | 16.136 / 14.768 / 14.768 / 14.768 | 21.298 / 23.339 | 0/0/0 | 0 | 0 | 1.092 / 2.069 |
| A2-control-OFF | R2/B | 60.042 / 0.000 | 16.169 / 14.624 / 14.624 / 14.624 | 22.695 / 24.798 | 0/0/0 | 1 | 0 | 4.098 / 2.069 |
| A2-control-OFF | R3/C | 60.929 / 0.886 | 16.413 / 14.839 / 14.839 / 14.839 | 23.602 / 25.635 | 0/0/0 | 0 | 0 | 3.981 / 2.200 |
| A2-control-OFF | R4/D | 62.574 / 2.532 | 16.856 / 15.239 / 15.239 / 15.239 | 24.186 / 26.103 | 0/0/0 | 0 | 0 | 0.992 / 2.200 |


### Rzeczywiste minięcia po pierwszym łuku


| Bieg | Para | Cały motocykl od s | Potwierdzenie s | Przebyty dystans utrzymania m |
|---|---|---|---|---|
| A2-rotation-0 | R2 przed R1 | 46.520 | 47.520 | 22.924 |
| A2-rotation-2 | R2 przed R1 | 25.250 | 26.250 | 24.678 |
| A2-control-OFF | R2 przed R1 | 24.390 | 25.390 | 23.240 |


W głównych próbach są 2 potwierdzone minięcia, oba R2 nad R1, w okrążeniach 4 (A2/0) i 2 (A2/2). Kontrola A2/OFF ma osobne minięcie R2/R1 już w 25.390 s, względem 47.520 s przy AI w tym ustawieniu; naturalna różnica tempa potrafi wyprzedzić bez taktyki. Brak podstaw, by przypisać te dwa sukcesy wyłącznie algorytmowi ataku. Chwilowe porządki referencji w JSON zawierają także pierwsze rozdzielenie z pól i nie są doliczane do tej tabeli.

### Aktywność przez pełne cztery okrążenia i anomalie

Aktywacje L1–L4 przypisano według własnych przecięć mety. Obserwacje są wywoływane do ukończenia biegu; brak późnej komendy przy braku bliskich rywali nie oznacza zatrzymania AI. Typowa prędkość to mediana ważona czasem, histogram 0.25 m/s; zakres poniżej obejmuje czterech zawodników. Gaz=0 jest rzeczywistym wejściem sterowania, a scrub w łuku może zmniejszać siłę mimo gazu=1.


| Bieg | Obserwacje AI/taktyki | Decyzje/aktywacje/zmiany sterowania | Aktywacje L1/L2/L3/L4 | Typowa m/s min–max | Gaz=0 R1/R2/R3/R4 s | Rewersy targetu R1–R4 | Największy spadek w 1 s, m/s |
|---|---|---|---|---|---|---|---|
| A1-rotation-0 | 2580/2498 | 16/16/6 | 16/0/0/0 | 21.375–24.375 | 0.00 / 3.20 / 4.90 / 6.10 | 0/0/0/0 | 1.280 |
| A1-rotation-1 | 2580/2498 | 16/16/6 | 16/0/0/0 | 21.375–24.375 | 3.20 / 4.90 / 6.10 / 0.00 | 0/0/0/0 | 1.280 |
| A1-rotation-2 | 2580/2498 | 16/16/6 | 16/0/0/0 | 21.375–24.375 | 4.90 / 6.10 / 0.00 / 3.20 | 0/0/0/0 | 1.280 |
| A1-rotation-3 | 2580/2498 | 16/16/6 | 16/0/0/0 | 21.375–24.375 | 6.10 / 0.00 / 3.20 / 4.90 | 0/0/0/0 | 1.280 |
| A2-rotation-0 | 2444/2425 | 37/37/16 | 22/6/1/8 | 21.375–23.375 | 0.00 / 5.80 / 4.30 / 3.60 | 0/0/2/1 | 2.279 |
| A2-rotation-1 | 2488/2447 | 49/49/28 | 13/5/17/13 | 21.625–24.625 | 0.00 / 14.69 / 6.50 / 0.00 | 0/2/0/0 | 2.044 |
| A2-rotation-2 | 2508/2439 | 12/12/4 | 9/3/0/0 | 21.875–24.625 | 3.70 / 0.00 / 0.00 / 0.00 | 0/0/0/0 | 1.503 |
| A2-rotation-3 | 2548/2445 | 12/12/5 | 12/0/0/0 | 21.875–24.375 | 4.10 / 0.00 / 0.00 / 2.40 | 0/0/0/0 | 1.456 |
| A1-control-OFF | 2528/0 | 0/0/0 | 0/0/0/0 | 21.375–24.375 | 0.00 / 0.00 / 0.00 / 0.00 | 0/0/0/0 | 1.280 |
| A2-control-OFF | 2500/0 | 0/0/0 | 0/0/0/0 | 21.375–24.625 | 0.00 / 0.00 / 0.00 / 0.00 | 0/0/0/0 | 1.450 |


1. **Utrata napędu przy odrzuceniu ataku, bez kontaktu.** A1/0: R2 NoPassingCorridor 10.640→13.840 s (3.2 s), R3 10.740→15.640 s (4.9 s), R4 10.840→16.940 s (6.1 s). Target praktycznie bez zmiany. Kod `TrafficDecision.Tactics` odrzuca kandydatów zarówno przez Corridor, jak i gain<=0, a następnie zwraca BackOut/Drive=0 z jedną etykietą NoPassingCorridor; powrót wymaga oddalenia przodu >8 m lub jego braku. To nie dowodzi, że każdy z tych epizodów był fizycznie niepotrzebny, ale odrzucenie korzyści ataku i wymóg bezpiecznej redukcji gazu nie są tutaj oddzielnie uzasadnione. Kontrola pokazuje spowolnienie mimo 0 kontaktów. Minimalny następny PR: rozdzielić powody odrzucenia i wykazać konieczność lift względem aktualnego korytarza; bez zmiany gripu lub mocy.
2. **Powtarzane lift/resume w późnym biegu.** A2/1 R2: 14.69 s gazu=0 łącznie, m.in. ImmediateThreat/ResumeContest 37.240→38.540, 42.640→44.240, 44.940→46.040, 50.440→52.040, 52.840→53.940 i 58.140→59.740 s. Widoczne na L3/L4 i dokładnie powtórzone w świeżym procesie. Maksymalna utrata prędkości w 1 s R2 =2.044 m/s, bez kontaktu. To konkretny kandydat do diagnozy nadmiernie ostrożnej taktyki, nie upadek ani błąd solvera; bez wyrzucania tej próby z raportu.
3. **Brak wzbudzonej obrony.** W głównych próbach 0 aktywowanych DefendLine. Sam fakt istnienia konsumenta Defense nie dowodzi jego jakości w pełnym biegu. R4 nie jest dowodem skutecznej obrony, nawet gdy wygrywa z A.
4. **Linie i ograniczenia.** Maksymalnie 2 rewersy znaczących zmian targetu (>0.05 m) u pojedynczego zawodnika, 0 w A1; brak szybkiego przełączania co obserwację. Nie jest to dowód braku wszystkich drobnych oscylacji: trajektorie zachowano tylko dla dwóch przypadków. Maksymalne spadki prędkości obejmują naturalne wejścia w łuk. Wszystkie końce i przesunięcia są zaakceptowanymi stanami solvera, bez teleportacji; maksimum zastosowanej siły/budżetu ≈1 (odchylenie float). Min. prześwit granic jest dodatni w każdej próbie. Zero kontaktów nie waliduje zachowania zatłoczonego wyścigu; może współwystępować z nadmierną ostrożnością.

## B. Próby indywidualne

### Kanoniczne 20/50/80

W każdej komórce poziomu: **pierwsze okrążenie / najlepsze lotne, s**. Wszystkie legacy=50, masa/setup/styl i start z pola A identyczne. Status dotyczy odpowiedzialności w tym silniku i badanego bodźca, nie produkcyjnego starego HeatSimulator. N/A oznacza istniejącego konsumenta niewzbudzonego w solo; INACTIVE oznacza brak konsumenta w SharedWorldSpeedway.


| Cecha | Poziom 20 | Poziom 50 | Poziom 80 | Wpływ na start | Wpływ na lotne okrążenie | Rzeczywisty konsument kodu |
|---|---|---|---|---|---|---|
| Reaction — ACTIVE | 16.252716 / 14.767945 | 16.228442 / 14.767955 | 16.203778 / 14.767951 | wcześniej o 0.048980 s 20→80 | brak mechanizmu; różnice numeryczne µs | RiderController.TapeReactionSeconds |
| Start — INACTIVE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | brak; launch nadal czyta legacy Start |
| Technique — PARTIAL | 16.228465 / 14.767950 | 16.228442 / 14.767955 | 16.228421 / 14.767931 | identyczny moment napędu i siła startowa | mikrosekundy; niewielka zmiana yaw | RiderController skillAuthority (torque); TrafficDecision margin; PhysicalContactAnalyzer.Reserve |
| TrackReading — INACTIVE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | brak konsumenta; brak percepcji zmiennej nawierzchni |
| Attack — NOT APPLICABLE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | TrafficDecision.Tactics / SideBySide: horyzont, margines i wybór; brak rywala |
| Defense — NOT APPLICABLE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | TrafficDecision.Tactics / SideBySide; brak rywala, 0 DefendLine w głównych biegach |
| PairRiding — INACTIVE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | brak modelu partnera/konsumenta w tym świecie |
| Strength — NOT APPLICABLE | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 16.228442 / 14.767955 | 0 | 0 | WorldRunner.BeginContact → PhysicalContactAnalyzer.Reserve; bez kontaktu |


Reaction: pierwszy dodatni napęd dokładnie 0.264490/0.240000/0.215510 s. Maksymalna siła styczna launch jest identyczna 1390.712524 N, a różnica pierwszego okrążenia 20→80 =−0.048938 s. Mechanizm zmienia czas reakcji, nie moc po starcie. Różnica 42 µs względem idealnego przesunięcia reakcji i niemal identyczne lotne wynikają z innej fazy siatki czasowej i położeń float; nie nadajemy im znaczenia sportowego.

Technique: maksymalny błąd orientacji względem zadanego headingu spada 0.478598/0.476290/0.475890 rad; pierwsza siła i czas launch identyczne. Kod mnoży torque przez 0.75+0.25*(Technique−1)/98, bez nowej mocy ani gripu. Czasy pierwszego okrążenia zmieniają się o zaledwie 44 µs między 20 i 80. Najlepsze lotne 50 jest o 4.48 µs gorsze niż 20 mimo poprawy maksymalnego yaw. Poszczególne lotne też nie są ściśle monotoniczne. Reprodukcja 80 jest dokładna, lecz to słaby sygnał w 10 ms/float, nie dowód istotnej przewagi czasowej i nie powód kalibracji. W Strength oraz Technique rezerwa kontaktowa istnieje, lecz nie została pobudzona w tych bezkontaktowych próbach.

Start, TrackReading i PairRiding nie mają konsumenta w nowym świecie. Zera Attack/Defense/Strength są zgodne z brakiem rywali, partnera lub kontaktu; nie dopisano bonusu do Vmax. Dla sześciu cech bez efektu solo wszystkie trzy poziomy mają dokładnie ten sam hash zaakceptowanych stanów co neutralny (łącznie z Attack/Defense/Strength).

### Oddzielnie legacy 20/50/80


| Cecha | Poziom 20 | Poziom 50 | Poziom 80 | Wpływ na start | Wpływ na lotne okrążenie | Rzeczywisty konsument kodu |
|---|---|---|---|---|---|---|
| Start — ACTIVE | 16.304432 / 14.767965 | 16.228442 / 14.767955 | 16.160679 / 14.767933 | napęd launch 1307.270→1474.155 N; reakcja taśmy stała | brak trwałego efektu; µs | LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons; ReactionSeconds tylko dla AI |
| Speed — ACTIVE | 16.746556 / 15.327673 | 16.228442 / 14.767955 | 15.753707 / 14.253592 | ta sama siła launch, inne ograniczenie wejścia | napęd Straight/TurnExit i limit łuku | LongitudinalDynamics Straight/TurnExit; SegmentPhysics.MaxSafeTurnSpeedForRadius |
| SlideControl — ACTIVE | 16.479278 / 15.047448 | 16.228442 / 14.767955 | 15.989561 / 14.502444 | ta sama siła launch, wcześniejsze przygotowanie do łuku | limit łuku i correction | SegmentPhysics.MaxSafeTurnSpeedForRadius; LongitudinalDynamics.CalculateCornerCorrectionDeceleration... |


Mechanizmy poniżej oddzielają początek jazdy, cały pierwszy odcinek 35 m (z reakcją), siłę startową, prostą i łuk. Średnie prędkości w tych dwóch kontekstach obejmują całe cztery okrążenia, z launch w średniej prostej; nie są ustalonym Vmax.


| Rodzina/cecha/poziom | Pierwszy napęd s | 35 m s | Max launch N | Śr. prosta / łuk m/s | Max yaw rad |
|---|---|---|---|---|---|
| legacy-Start-20 | 0.240000 | 3.091158 | 1307.270 | 20.983322 / 21.402523 | 0.476291 |
| legacy-Start-50 | 0.240000 | 3.015141 | 1390.713 | 21.051031 / 21.402519 | 0.476290 |
| legacy-Start-80 | 0.240000 | 2.947420 | 1474.155 | 21.111789 / 21.402517 | 0.476290 |
| legacy-Speed-20 | 0.240000 | 3.028999 | 1390.713 | 20.346730 / 20.631933 | 0.459200 |
| legacy-Speed-50 | 0.240000 | 3.015141 | 1390.713 | 21.051031 / 21.402519 | 0.476290 |
| legacy-Speed-80 | 0.240000 | 3.004806 | 1390.713 | 21.726353 / 22.173016 | 0.493392 |
| legacy-SlideControl-20 | 0.240000 | 3.022550 | 1390.713 | 20.689385 / 21.017523 | 0.467746 |
| legacy-SlideControl-50 | 0.240000 | 3.015141 | 1390.713 | 21.051031 / 21.402519 | 0.476290 |
| legacy-SlideControl-80 | 0.240000 | 3.009034 | 1390.713 | 21.397925 / 21.787439 | 0.484837 |


Legacy Start przy stałej kanonicznej Reaction nie zmienia reakcji taśmy (0.240000 s); zwiększa rzeczywistą siłę launch. Jego stara funkcja czasu reakcji nadal jest proxy czasu reakcji taktycznej, ale TrafficEnabled=false w solo, więc nie tłumaczy tego wyniku. Legacy Speed działa na siłę zwykłej prostej/wyjścia z łuku i także bezpośrednio na limit prędkości łuku (stary mnożnik 0.94+0.12*Speed/100). To nie jest docelowy dowód jakości kanonicznej techniki ani ataku. SlideControl działa na limit 0.97+0.06*SlideControl/100 i correction; może poprawiać proste przez wyjście z poprzedniego łuku. Małe różnice pierwszych 35 m Speed/SlideControl wynikają z przygotowania do nadchodzącego łuku, mimo identycznego launch. Wyższe SlideControl podnosi max yaw, bo jedzie się szybciej; izolowana miara błędu orientacji nie jest kompletną miarą stabilności przy innym tempie.

Wszystkie trzy legacy poprawiają pierwszy czas w 20→50→80; Speed i SlideControl także lotny. Odwrócenia mikrosekundowe legacy Start na pojedynczych lotnych nie są trwałym efektem mocy startowej po kilkunastu sekundach. Nie wykryto istotnej sprzeczności typu Strength dodaje Vmax; wykryto ograniczony wpływ kanonicznego Technique i niepodłączony Start. **Brak kategorii UNEXPECTED o istotnej skali w solo**; to nie unieważnia problemu fallbacku gazu w pełnym biegu.

### Wszystkie 33 przejazdy solo

Tabela zawiera również identyczne neutralne 50 dla każdej osi; żadna próba nie została pominięta. Link każdej próby prowadzi do wejść, hashy, pełnych miar i finishu.


| Próba | Standing L1 s | Flying L2 s | Flying L3 s | Flying L4 s | Najlepsze lotne s | Cztery okrążenia s |
|---|---|---|---|---|---|---|
| [canonical-Reaction-20](full-heat-and-solo-validation/canonical-Reaction-20.json) | 16.252716 | 14.767945 | 14.767985 | 14.767959 | 14.767945 | 60.556606 |
| [canonical-Reaction-50](full-heat-and-solo-validation/canonical-Reaction-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Reaction-80](full-heat-and-solo-validation/canonical-Reaction-80.json) | 16.203778 | 14.767951 | 14.767995 | 14.767967 | 14.767951 | 60.507691 |
| [canonical-Start-20](full-heat-and-solo-validation/canonical-Start-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Start-50](full-heat-and-solo-validation/canonical-Start-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Start-80](full-heat-and-solo-validation/canonical-Start-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Technique-20](full-heat-and-solo-validation/canonical-Technique-20.json) | 16.228465 | 14.767979 | 14.767950 | 14.767986 | 14.767950 | 60.532379 |
| [canonical-Technique-50](full-heat-and-solo-validation/canonical-Technique-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Technique-80](full-heat-and-solo-validation/canonical-Technique-80.json) | 16.228421 | 14.767931 | 14.767969 | 14.767943 | 14.767931 | 60.532265 |
| [canonical-TrackReading-20](full-heat-and-solo-validation/canonical-TrackReading-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-TrackReading-50](full-heat-and-solo-validation/canonical-TrackReading-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-TrackReading-80](full-heat-and-solo-validation/canonical-TrackReading-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Attack-20](full-heat-and-solo-validation/canonical-Attack-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Attack-50](full-heat-and-solo-validation/canonical-Attack-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Attack-80](full-heat-and-solo-validation/canonical-Attack-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Defense-20](full-heat-and-solo-validation/canonical-Defense-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Defense-50](full-heat-and-solo-validation/canonical-Defense-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Defense-80](full-heat-and-solo-validation/canonical-Defense-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-PairRiding-20](full-heat-and-solo-validation/canonical-PairRiding-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-PairRiding-50](full-heat-and-solo-validation/canonical-PairRiding-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-PairRiding-80](full-heat-and-solo-validation/canonical-PairRiding-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Strength-20](full-heat-and-solo-validation/canonical-Strength-20.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Strength-50](full-heat-and-solo-validation/canonical-Strength-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [canonical-Strength-80](full-heat-and-solo-validation/canonical-Strength-80.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [legacy-Start-20](full-heat-and-solo-validation/legacy-Start-20.json) | 16.304432 | 14.767965 | 14.767999 | 14.767975 | 14.767965 | 60.608371 |
| [legacy-Start-50](full-heat-and-solo-validation/legacy-Start-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [legacy-Start-80](full-heat-and-solo-validation/legacy-Start-80.json) | 16.160679 | 14.767982 | 14.767958 | 14.767933 | 14.767933 | 60.464552 |
| [legacy-Speed-20](full-heat-and-solo-validation/legacy-Speed-20.json) | 16.746556 | 15.327677 | 15.327697 | 15.327673 | 15.327673 | 62.729604 |
| [legacy-Speed-50](full-heat-and-solo-validation/legacy-Speed-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [legacy-Speed-80](full-heat-and-solo-validation/legacy-Speed-80.json) | 15.753707 | 14.253597 | 14.253607 | 14.253592 | 14.253592 | 58.514504 |
| [legacy-SlideControl-20](full-heat-and-solo-validation/legacy-SlideControl-20.json) | 16.479278 | 15.047448 | 15.047452 | 15.047488 | 15.047448 | 61.621667 |
| [legacy-SlideControl-50](full-heat-and-solo-validation/legacy-SlideControl-50.json) | 16.228442 | 14.767955 | 14.767989 | 14.767966 | 14.767955 | 60.532352 |
| [legacy-SlideControl-80](full-heat-and-solo-validation/legacy-SlideControl-80.json) | 15.989561 | 14.502444 | 14.502475 | 14.502508 | 14.502444 | 59.496988 |


### Wydajność i koszt pomiaru


| Release, 3 próbki, mediana | Wall ms | CPU ms | Alokacje MiB | Callbacki ms | Taktyka ms | Kroki 10 ms | Wywołania solvera | Substeps | AI | Obserwacje taktyki |
|---|---|---|---|---|---|---|---|---|---|---|
| A1/0 bez obserwatora | 137.402 | 125.000 | 16.912 | 0.000 | 1.954 | 6457 | 6528 | 26112 | 2580 | 2498 |
| A1/0 + pomiary | 196.783 | 187.500 | 52.882 | 55.164 | 1.944 | 6457 | 6528 | 26112 | 2580 | 2498 |


[Surowe trzy próbki na tryb](full-heat-and-solo-validation/performance.json). Każdy bieg to 64.570 s świata; 4 substeps na każde wywołanie solvera, a nie tylko cztery na krok nadrzędny. Solver ma dodatkowe legalne wywołania przy krzywiźnie/deadline. Różnica median wall to około 59.4 ms, alokacji około 36.0 MiB, suma callbacków około 55.2 ms. Opakowanie observed zawiera też zbudowanie podsumowania i mapowanie wejść; callback nie mierzy kosztu utworzenia Snapshot przed jego wywołaniem. Nie odejmujemy czasu callbacku od wall, aby udawać dokładny czas samej fizyki. CPU to TotalProcessorTime procesu (także GC), z kwantyzacją platformy i możliwością przewyższenia wall; trzy próbki to diagnoza, nie precyzyjny benchmark sprzętowy. Wszystkie sześć hashy jest identycznych z A1/0 i nie ma optymalizacji silnika.

W zapisanych próbach `Performance.SummaryMilliseconds` oddziela agregację po symulacji, a `AcceptedObserverMilliseconds` / `ForceObserverMilliseconds` same callbacki; serializacja JSON i generowanie tego dokumentu są poza pomiarem symulacji. Trajektorie plikowe są wyłączone w benchmarku. Nie dodano eksportu pełnego stanu co krok. Dane wejściowe, podsumowania i zdarzenia zajmują kilka MB, a nie wielkie pliki snapshotów.

## C. Stan gotowości silnika


| Obszar | Stan | Dowody | Co wymaga kolejnego PR |
|---|---|---|---|
| Start | PARTIAL | Reaction steruje czasem; legacy Start siłą; canonical Start INACTIVE | Jawne podłączenie techniki startu, osobno od reakcji i mocy |
| Jazda solo | PARTIAL | 33/33 cztery okrążenia, jawne czasy i konsumenci | Istotność kontroli Technique; później osobna nawierzchnia |
| Pełne cztery okrążenia | działa w zestawie | 8/8 głównych + 2/2 kontroli, brak timeoutów | Szersza walidacja po usunięciu problemów taktyki |
| Wyprzedzanie | PARTIAL | 2 rzeczywiste po pierwszym łuku; 1 w kontroli OFF | Odróżnić wartość AI od naturalnej przewagi tempa i pola |
| Obrona | niewykazana | 0 aktywowanych DefendLine w głównych | Próba uruchamiająca obronę bez strojenia wyniku |
| Walka koło w koło | PARTIAL | Contest/Hold i skończone zmiany targetu | Rozdzielenie braku korzyści ataku od konieczności odjęcia gazu |
| Kontakty i upadki | niewzbudzone w biegach | 0/0; przerwanie sprawdza krótka regresja istniejącego Crash | Wiarygodność kontaktów pełnego heat bez sztucznego wymuszania |
| Zróżnicowanie zawodników | PARTIAL | silne efekty legacy, 3 canonical bez konsumenta, N/A rozdzielone | Docelowi konsumenci, bez Overall i bonusu do prędkości |
| Wydajność | zmierzona, bez optymalizacji | około 137 ms bez diagnostyki / 197 ms z nią za heat | Więcej stabilnych pomiarów dopiero przy decyzji o integracji |


**Ocena B — CZĘŚCIOWO.** Silnik potrafi kończyć pełne biegi w tym zestawie; nie stwierdzono blokera ukończenia ani konieczności zmiany czasu/klasyfikacji. Nie uzasadnia to jeszcze oceny A: powtarzany lift/resume, fallback po nieudanym ataku, zerowa obrona w zestawie i luki kanonicznych konsumentów wymagają osobnych prac. Nie ma podstaw do szerokich wniosków o realizmie na podstawie ośmiu biegów. Zmienna nawierzchnia nie naprawi tych luk i nie została tu implementowana.

### Weryfikacja i regresje

- 6/6 nowych testów miar: interpolacja i cofnięcie referencji, realne utrzymane minięcie, krótkie zmiany/zdublowanie, niezależność osi wejściowych, zgodność mety i dokładny hash z/bez obserwatora oraz odwrotna kolejność, Crash jako przerwanie bez fikcyjnego DNF.
- Release: 64/64 (`FullHeatSoloAuditTests|SharedWorld`), następnie 39/39 (`SideBySideRacingTests|TacticalQualityTests`), razem **103/103**, bez powtarzania historycznych audytów #54–#91.
- [Pięć świeżych powtórzeń](full-heat-and-solo-validation/repeats.json): A1/0, A2/0, A2/1, Technique80, legacy SlideControl80. ExactHash/Laps/Passes/Decisions/Contacts=true dla wszystkich; wejście odwrócone, identyczne profile. Stan sprzętu/nawierzchni i wersja fizyki te same.
- `dotnet restore`, lokalna kompilacja Release z warnings-as-errors: sukces. Guard 39 zamrożonych konsumentów: sukces. Discovery: 2616/2616 przypadków ma dokładnie jeden shard (core 1700, trajectory 395, historical-analysis 473, four-rider 48), bez braków/duplikatów. Python: 89/89 kalibracji + 7/7 testów CI. Standardowe Windows CI zostaje bez zmian; jego wynik na opublikowanym HEAD jest widoczny w Checks Draft PR, nie jest tu z góry deklarowany jako zielony. Zielone CI nie zmienia oceny B.

Wszystkie dane tego dokumentu pochodzą z lokalnych plików evidence; generator sprawdza kompletność 33 solo, cztery sumy okrążeń, statusy, flagi, neutralne hashe i powtórzenia przed zapisem. Nie pobiera danych z innej wersji fizyki. Manifest SHA-256 obejmuje konfigurację, każdy JSON i obie trajektorie.
