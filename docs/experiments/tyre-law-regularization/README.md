# #140 — Tyre Constitutive Law & Numerical Regularization Separation

PROVISIONAL, domyślnie OFF. Baza dokładnie `1a84e527536e0dc4e77eaa1acebd2866c1579593`; gałąź `experiment/tyre-law-regularization-140`. Oryginalne źródła i wyniki #139 pozostają bez zmian.

Problem: `dx² = 0.1²*(u²+0.5²) + [Br*(R²/Iw+1/m)*0.03]²` łączy chwilową charakterystykę kontaktu z bezwładnością dynamiki. Nowe czyste `TyreLaw140` zachowuje gładkie nasycenie na jednym budżecie μFz, lecz nie przyjmuje Iw, masy, dt ani tolerancji solvera. Skończone skale prędkości poślizgu są jawnymi parametrami materiałowymi. Zamrożono je z referencyjnej krzywej #139 dla m=147 kg, Iw=0,9 kg·m², R=0,33 m i μ=1,2845. To sposób zachowania nominalnej kotwicy, **nie kalibracja rzeczywistej opony**. Nie dopasowano prędkości ani czasu okrążenia.

Jeden solver: implicit midpoint koła i wzdłużnej reakcji korpusu, rozwiązanie monotonicznego skalarnego równania przez Newtona z jawnym przedziałem. Iw i dt wpływają na rozwój omega i poślizg w kroku. Charakterystyka materiałowa nie zmienia się z dt. Dotychczasowa skończona regularność przy małych prędkościach pozostaje; nie usunięto jej na ślepo. Boczne sprzężenie korpusu i przedni kontakt zachowują dotychczasowe przybliżenie held-force; 0,03 s przodu oznacza skalę nachylenia, nie czasowy stan relaksacji.

`results.json` zawiera zamrożone źródła, parametry, kryteria, rezerwacje i późniejsze wyniki. Kolejność: matematyka → preflight → D0/D1 i małe perturbacje → lock → H0/H1. Plan: 12 nowych światów, limit 24 / 450 s CPU; stare B/C z #139 czytane z cache. Polityka #16015, silnik, mu, masa/I korpusu, geometria, reakcja 0,24 s, przeglądy 0,2 s, aktuator i obwiednia β45° pozostają bez zmian.

Odtwarzanie z katalogu repozytorium: `dotnet run --project tools/tyre-law-regularization -c Release -- --enable-tyre-separation140 <nowy-results.json> freeze`, potem `math`, `preflight`, `diagnostic`, osobny lock i `confirmation`. Nie nadpisywać protokołu ani powtarzać zakończonych rezerwacji. `python tools/tyre-law-regularization/generate.py --check` kontroluje izolację źródeł. Wyniki i werdykt zostaną uzupełnione po przejściu bramek; nie powstają viewery, ZIP, MP4 ani osobne raporty.
