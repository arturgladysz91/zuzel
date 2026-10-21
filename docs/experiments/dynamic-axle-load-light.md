# PR #114 LIGHT — dynamiczny rozkład nacisku

**Wynik: pozostawić OFF.** Jeden nowy pierwszy łuk jest bezpieczny, lecz korzyść
dla kontroli nie jest jednoznaczna, a liczba zapytań inverse wzrosła o 12,29%.
Twardy limit LIGHT nie został zaliczony; nie uruchamiano etapu C ani 8 okrążeń.
Baza: zweryfikowany Draft #113, `fc 5d 184e 95fa 3d 6d 12c 27abd 59ab 8de 7e 2630bee`.

## 1–2. Obciążenie osi i różnica względem 40/60 — istniejące dane

**Etap A:0 nowych światów Box 2D.** Z13 933 zapisanych stanów #113 obliczono
`ax=dot(v_i−v_previous,forward_i)/h_previous`. To nie pochodna modułu prędkości.
Początkowo ax=0; dalej wyłącznie poprzedni zaakceptowany interwał, bez filtra.
`rear=a/(a+b)+ax*0.55/(9.81*(a+b))`, a=0,70 m, b=0,60 m.

| Faza | Tył min / średnia / max [%] | Średni przód [%] | Różnica średniego tyłu od 60% [pp] |
|---|---:|---:|---:|
| Prosta |52,29 /60,19 /61,50|39,81|+0,19|
| Wejście |52,31 /53,90 /61,01|46,10|−6,10|
| Środek |53,44 /54,44 /59,66|45,56|−5,56|
| Wyjście |54,51 /58,20 /60,26|41,80|−1,80|
| Całość |52,29 /57,23 /61,50|42,77|−2,77|

Średnie ważone czasem. Brak ujemnych nacisków. Różnice w łuku uzasadniały
jedną próbę B. Już **statyczna geometria** daje 46,15/53,85 zamiast 40/60;
wpływu tej zmiany i samego transferu nie rozdzielono osobnymi symulacjami.
[Dane A](dynamic-axle-load-light/existing-states.json) i
[przebieg pierwszego okrążenia](dynamic-axle-load-light/first-lap-load-trace.json)
zawierają rzeczywiste próbki, bez interpolacji.

Wysokość 0,55 m jest **prowizoryczna i niezmierzona**. To hipoteza podziału
efektywnego B proporcjonalnie do oszacowanego nacisku, nie skalibrowaneμN.

## 3–4. Wyłamanie, wheelspin i momenty — jedna nowa symulacja

**Etap B:1 nowy świat**, ten sam start, INNER, progressive-b, silnik i regulator
#113. Baseline pochodzi z zapisu, bez ponownej symulacji. Stop przy pierwszym
zaakceptowanym stanie za tym samym wyjściem; końcowe referencje 133,588/133,607 m.

| Metryka pierwszego łuku | #113 zapis | Dynamiczny nacisk |
|---|---:|---:|
| Czas do wyjścia [s] |5,82|5,78|
| β max / średnia bezwzględna [°] |7,138 /4,745|9,192 /5,543|
| Wheelspin max / średnia [%] |12,643 /10,279|14,884 /10,985|
| Średnia prędkość [km/h] |65,238|65,534|
| Peak Fy przód / tył [N] |814,69 /951,31|838,30 /993,61|
| Średnie momenty przód / tył [Nm] |456,44 /−455,16|465,08 /−464,34|
| Peak momentów przód / tył [Nm] |550,04 /570,79|580,16 /596,16|
| Min kreda / banda [m] |1,349 /8,948|1,325 /9,086|
| Dolne granice między próbkami [m] |1,241 /8,844|1,218 /8,983|
| Błąd XY RMS / max [m] |0,360 /0,733|0,334 /0,741|

Moment netto średnio 1,278→0,744 Nm; większe przeciwne momenty nadal niemal się
równoważą. Peaki nie występują jednocześnie. Błąd XY policzono z zamrożonego
gęstego eksportu C3; interpolacja jest wyłącznie pomiarem odniesienia.
Rzeczywiste XY, heading, v i sterowanie pozostają natywnym zapisem CSV.gz.

W B przód/tył mają średnio 41,28/58,72% nacisku. Teoretyczne N przodu
557,33–688,25 N, tyłu 753,82–884,74 N; sumują się do 1442,07 N.
Budżety BF=826,41–1020,54 N, BR=1117,76–1311,89 N, **BF+BR=B≈2138,30 N**.
Nie zmieniono całkowitego B ani charakterystyk kontaktów.

Straty przód / tył boczna / tył wzdłużna:14,533/8,395/3,597→
12,646/10,044/3,808 kJ. Silnik 39,257→39,165 kJ, opory−11,150→−11,187 kJ,
łożysko 0,424→0,429 kJ. Residual energii−0,052→+0,075 J; brak kreacji energii
ponad pracę silnika/tolerancję float. Niezależny ledger wyjaśnia residual pracą
błędu impulsu native (<1e−6 J niewyjaśnione). Rotacja jest rozliczona raz.

## 5. Bezpieczeństwo, kontrola i koszt

Pierwszy łuk zachowuje marginesy bezpieczeństwa. RMS lekko maleje, ale peak
błędu i prześwit przy kredzie pogarszają się. Większy β/wheelspin nie dowodzi
poprawy. **Brak jednoznacznej korzyści**, zatem etap C pozostaje zamknięty.

Nowa flaga `EnableDynamicAxleLoad=false` wymaga obu flag #112/#113. Obliczenie
nacisku następuje raz po zaakceptowanym kroku:581 aktualizacji dla 580 kroków,
wliczając stan początkowy. Wszystkie zapytania kierownicy dostają tę samą kopię.
Oryginalny rear root otrzymuje jawny BR, bez mutacji parametrów i ponownego solve.
Ujemny surowy nacisk jest oznaczany i zatrzymuje audyt, bez clampu/unoszenia koła.

**Koszt rzeczywistego przejazdu:**584→580 kroków, ale 28 826→32 368 zapytań.
Liczba przedziałów z rootem inverse rośnie 599→862. Niezmieniona bisekcja #113
wykonuje 14 zapytań na przedział: `14*263−35*4=3542` dodatkowych zapytań.
Mechanizm nacisku nie dodaje wywołań, lecz **cały wariant przekracza wymagany
limit**. Nie zmieniano regulatora, żeby ukryć ten wynik.

Pomiar na identycznych 13 933 stanach i zapisanej kierownicy, bez Box 2D/commit:
sam nacisk 0,498 ms (**1,98%** bazowego kosztu kontaktów), pełna ścieżka
OFF/ON25,179/27,905 ms (**+10,83%**, mediany 3 serii). Liczba solve i ich
iteracji identyczna:13 933 solve,27 861 iteracji Newtona na wariant.
Wzrost czasu pojedynczego kontaktu obejmuje nową ścieżkę przekazania/walidacji
budżetu i zmienność pomiaru; udziałów tych nie rozdzielano. Pewną przyczyną
wzrostu liczby zapytań całego przejazdu są powyższe dodatkowe bisekcje.
[Pomiar miarodajny](dynamic-axle-load-light/performance-matched.json) dekoduje
wejścia przed pomiarem i robi warm-up; wstępny `performance.json` zawierał narzut słowników.

## 6. Rekomendacja

**Nie przyjmować wariantu jako poprawy LIGHT.** Najpierw ograniczyć koszt
inverse #113 oraz oddzielić statyczne 46/54 od transferu w zapytaniach na zapisach.
Brak dowodu uzasadniającego przebudowę opony. Potrzebne są dane o statycznym
rozkładzie masy i wysokości CoM, zanim nazwiemy ten podział realistycznym.
Wszystkie flagi pozostają OFF; nie zmieniono Grip, napędu, masy/inercji,
geometrii, linii, kolizji, viewera ani `src/CoreSim`.

## Odtworzenie i walidacja

[comparison.json](dynamic-axle-load-light/comparison.json) i manifest zawierają
pełne pomiary. Zachowano 985/985 historycznych hashy i fingerprinty OFF;
testy odtwarzają zapisane siły i liczniki #113. Dodatkowo tylko niezbędne
krótkie testy jednostkowe; brak historycznych macierzy i optymalizatora.
Lokalnie:26/26 C#,129/129 Python,7/7 CI policy,13/13 Node; Release bez ostrzeżeń.
[validation.json](dynamic-axle-load-light/validation.json).

Windows/PowerShell, repozytorium, świeży katalog wyników:

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
$outDir = 'results/dynamic-axle-load-recapture'
python tools/shared-world-speedway/dynamic-axle-load-light.py --data $outDir
foreach ($stage in @('freeze','first-bend','performance','contact-root-work')) {
  dotnet run --no-build -c Release --project tools/shared-world-speedway -- dynamic-axle-load-light $outDir $stage
  if ($LASTEXITCODE -ne 0) { throw "Gate failed: $stage" }
}
python tools/shared-world-speedway/dynamic-axle-load-light.py compare --data $outDir
```

Raw eksport odmawia nadpisania; nie uruchamiać ponownie do istniejącego katalogu
dowodów. Standardowe Windows Draft CI dla końcowego HEAD i jego wynik są
w opisie Draft PR. Bez merge. Lokalnego `results/` nie porządkowano ani nie dodano do PR.
