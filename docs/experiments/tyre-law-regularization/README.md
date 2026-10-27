# #140 — Tyre Constitutive Law & Numerical Regularization Separation

**PROVISIONAL, domyślnie OFF.** Baza: `1a84e527536e0dc4e77eaa1acebd2866c1579593`. Produkcja, historyczne źródła/wyniki, silnik, przełożenie, mu, masa/I korpusu, geometria i polityka #16015 pozostają bez zmian. Protokół zamrożono w `b6f1f8c`, H0/H1 przed ich nowymi wynikami w `1d694ab`. Zachowano reakcję 0,24 s, przeglądy 0,2 s, ±35°/90°/s i eksperymentalną obwiednię β45°.

## Zmiana i jej granice

Stare `dx²=0.1²*(u²+0.5²)+[Br*(R²/Iw+1/m)*0.03]²` zmienia chwilowe Fx/Fy przy zmianie Iw. Człon 0,03 ograniczał sztywność numeryczną; nie był czasowym stanem relaksacji.

Nowe czyste `TyreLaw140` otrzymuje slip wzdłużny/boczny, prędkość, Fz, mu i parametry charakterystyki. `a=slip/dx`, `b=-side/dy`, `(Fx,Fy)=mu*Fz*(a,b)/sqrt(1+a²+b²)`. Jeden wspólny budżet, bez Iw, masy, dt i tolerancji solvera. Zachowano skończone skale: `dx²=0.1²*(u²+0.5²)+3.824163847²`, `dy²=0.12float²*(u²+0.5²)+0.378028344²`. Zakotwiczono je raz w nominalnej krzywej #139 (147 kg, Iw 0,9, R 0,33, mu 1,2845). Mają status **UNCALIBRATED**; nie dobrano ich do wyniku jazdy. Poza referencją stałe skale oznaczają inną hipotezę zależności nachylenia od obciążenia.

Jeden `WheelSolver140` rozwiązuje monotoniczne równanie implicit midpoint poślizgu: `g(z)=z+c*z/sqrt(d²+z²)-|S|=0`, `g'>=1`, przedział `[0,|S|]`. Newton ma zabezpieczony przedział i jawny limit; brak zbieżności zatrzymuje próbę. Iw i dt występują w dynamice, zaś ta sama float Fx trafia do reakcji koła i Box2D. Zachowano dotychczasowe held-force sprzężenie boczne korpusu. Predyktor używa nowej ścieżki kontaktu, przy niezmienionych regułach wyboru akcji. Przód pozostaje oryginalny: lokalne boczne Fy, pasywność, brak napędu; jego skala nachylenia nadal wymaga kalibracji.

## Wyniki

- **Test Iw:** dla 0,45 / 0,90 / 1,80 czyste Fx/Fy wynosi identycznie **629,703087 / 769,323412 N**; różnica 0 N. Przy dt=0,01 s omega po kroku wynosi 131,152758 / 131,022868 / 130,957205 rad/s. Dynamika pozostaje zależna od Iw.
- **2592 testy koła:** maksymalny residual **1,46×10⁻⁷ m/s**, błąd bilansu koło+wzdłużny korpus **3,08×10⁻⁹ J**, znormalizowany 1,16×10⁻¹⁵; najwyżej 3 iteracje. Straty nieujemne, wspólny budżet zachowany, wyniki skończone przy zerze i dużym wheelspin. Uwzględniono pracę zewnętrznego obciążenia.
- **Zbieżność:** dt 0,020 / 0,010 / 0,005 / 0,0025 s, sześć scenariuszy, wspólna chwila 0,2 s. Błędy kolejnych zagęszczeń maleją we wszystkich przypadkach; rząd 0,77–1,12. Pełne sprzężenie planarne pozostaje zasadniczo pierwszego rzędu. Nie wykazano wyższego rzędu ani ogólnej przewagi dokładności nad starym modelem. Fx/Fy, omega, slip, momenty, residuals, energia i CPU są w `results.json`.
- **Przód:** lokalne Fx ≤3,06×10⁻⁵ N, praca niedodatnia; różnica siły względem dt przy utrzymanym kącie wynosi 0 N. Fizyczna zmiana kąta aktuatora może zmieniać siłę w kroku.
- **Preflight:** dokładny historyczny fingerprint prefiksu OFF. Nowy predyktor versus Box2D po 0,5 s: błąd pozycji 9,55×10⁻⁶ m, omega 1,10×10⁻⁵ rad/s. Osiem starych pełnych wyników/fingerprintów odczytano z #139, bez ponowienia przejazdów.

|Rodzina|Nowy dłuższy impuls|Nowy krótki impuls|Max β krótkiego|v wyjście / +62 m, km/h|
|---|---|---|---:|---:|
|D0|DNF β45°|Ukończony|15,14°|60,62 / 73,51|
|D1|DNF β45°|Ukończony|13,51°|62,97 / 73,47|
|H0|DNF β45°|Ukończony|12,09°|65,85 / 77,89|
|H1|DNF β45°|Ukończony|16,74°|59,00 / 73,07|

Oba modele: dłuższy impuls 0/4, krótki 4/4. To reguły B/C z #139: przygotowanie i 0,6 s żądań gaz1 versus powrót obu kanałów do #134A od kolejnego przeglądu. Gaz1 był aktywny na każdym krótkim wyjściu. Osobno sprawdzono historyczną bramkę dwóch łuków i pełną następną prostą 62 m; punkty +20/+40/+62 są w JSON. Dwie historie z perturbacją początkowej orientacji ±0,25° i yaw ±0,01 rad/s ukończyły wariant krótki. Nie dowodzi to stabilnego dużego driftu; lokalne badanie nielegalnego `D0-failed` także nie jest legalnym resetem.

W nowych światach maksymalny błąd bilansu punktowych prac wyniósł **0,01481 J**, 18,11% tolerancji `0,05 J+10⁻⁶*K_body`; maksymalny nadmiar energii ponad pracę napędu był ujemny. Błąd aktualizacji yaw ≤4,67×10⁻⁷ rad/s wspiera brak podwójnego momentu. Wszystkie 4 nowe DNF zachowano z przyczyną i ostatnimi stanami; osobno wskazano 4 historyczne DNF. Szczegółowe ślady ograniczono do jednego D0 DNF i jednego poprawnego D0.

## Koszt, odtworzenie i werdykt

**12/24 nowych światów, 123,53125/450 s CPU**, w tym matematyka i wszystkie prognozy; 2 039 578 natywnych/prognozowanych kontaktów, 22 348 prognoz. Odczyt cache 1,5 s CPU i publikacja 0,203125 s są osobno. Bez CEM, treningu, nowych viewerów i archiwów. Skupione testy: 5 C# i 6 testów danych PASS; standardowy Windows CI końcowego HEAD jest identyfikowany w PR, workflow bez zmian.

Źródła i konfiguracje: `results.json` → `Protocol`, `Reproduction`, `Reservations`, `Mathematics`, `SameStateComparison`, `CachedOld`, `Native`. Generator: `python tools/tyre-law-regularization/generate.py --check`. CLI: `dotnet run --project tools/tyre-law-regularization -c Release -- --enable-tyre-separation140 <nowy-results.json> freeze`, następnie `math`, `preflight`, `diagnostic`, commit lock, `confirmation`; `finalize.py` tylko kondensuje dane. Zakończone rezerwacje nie są ponawiane.

**NUMERICAL IMPROVEMENT CONFIRMED + PHYSICS MODEL DEFICIENCY REMAINS.** Potwierdzono separację charakterystyki od dynamiki Iw i przejście bramek numerycznych. Nominalna jazda pozostała niemal taka sama. Nadal brak pomiarów żużlowego Fx/Fy(α,κ,Fz), odpowiedzi przejściowej i obciążonego koła; [status empiryczny #139](../motorcycle-physics-validation/EMPIRICAL.md) obowiązuje. Kolejny krok to ich identyfikacja i kalibracja jawnych parametrów opony. Wariant pozostaje OFF.
