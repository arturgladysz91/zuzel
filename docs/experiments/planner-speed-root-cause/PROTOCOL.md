# Badanie #148 — protokół przed nowymi eksperymentami

Baza b92694ada648404bdd8180f8d02ac9138b67ac66, Draft PR #146. Nowa gałąź experiment/planner-speed-root-cause-148, Draft bez merge. Budżet 1500 CPU-s z 30 s rezerwy; wszystkie eksperymenty/prognozy i odczyty analityczne w ledger. Regresje/build/formatowanie nie tworzą nowych eksperymentów. Nowe ścieżki OFF.

Kolejność: ponowne użycie danych i PR138–146; dokładny profil geometrii/seedów i alokacji; dokładna optymalizacja A z parytetem wszystkich prognoz; krótkie porównania decyzji; jeden pełny nowy przejazd od zera. Następnie diagnostyki geometrii K1 i możliwości fizycznych oraz ograniczone kontrfaktyczne próby gazu. W razie DNF najpierw diagnoza, potem ewentualny kolejny wariant.

A: podejrzewany zbędny koszt to projekcja czterech końców osi kapsuł na tor w każdym punkcie prognozy, również wtedy, gdy cały spójny obrys ma dodatnią odległość od obu krawędzi, a CoM jest wewnątrz. Będziemy sprawdzać dokładną równoważność i pozostawimy oryginalny fallback. Niezależny preflight korzysta z oryginalnej metody. Wspólny prefiks opóźnienia jest osobną możliwością, nie zakładamy równoważności po skopiowaniu koła bez testu IEEE.

B: dopiero jeśli potrzebne, tańsze propozycje lub ogon zamiast pełnego K1, przy tej samej końcowej certyfikacji. Wyniki B nie będą nazywane optymalizacją zachowującą semantykę.

G0 pozostaje progresywnym C2 torem bazowym. Zamrożony generator odmawia innych długości/radii; przed diagnostyką wyeksportujemy ograniczenia i jawnie oddzielimy ewentualny zamknięty wariant kołowy G0c od G0. G1/2 zmieniają tylko długość prostych względem G0c, G3/4 tylko promień. Nie podstawiamy geometrii diagnostycznej do wyniku głównego. Nie porównujemy samych sum czasu różnych długości.

Każdy nowy świat zapisuje pełne stany/siły/komendy i wynik także przy porażce. Dziennik autonomicznych decyzji fsync przed wykonaniem. Żadna historia przyszłych komend ani ręczny benchmark nie sterują nowym wariantem. Kontrolowane starty diagnostyk osiągów mają jawnie odrębny status od startu autonomicznego z 0 km/h.
