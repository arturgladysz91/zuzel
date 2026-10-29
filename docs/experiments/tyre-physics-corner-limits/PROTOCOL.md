# Badanie #149 — protokół przed nowymi światami

Baza: dokładnie 14cb6c2fb947d2d04b1a7979346c8947b131b4fd, Draft PR #147. Nowa gałąź experiment/tyre-physics-corner-limits-149. Budżet 1200 CPU-s z rezerwą 20 s. Wszystkie nowe modele/sterowniki OFF; bez merge, podnoszenia mu, mocy lub zastępowania G0.

1. Najpierw nowy pełny native od zera na finalnym kodzie #148 po poprawce strażnika. Osobny ledger i katalog, żadnego nadpisywania #148. Porównanie wszystkich próbek/komend oraz nowego CPU.
2. Audyt oryginalnego prawa i #140: oddzielne dt=0, obciążenie/slip/torque/Iw, pasywność i zbieżność. Dopiero po diagnozie izolowany B, nadal te same silnik/sprzęgło, mu, masa, geometria i opóźnienia.
3. Małe okna zakrętu: 70/80/90/100 km/h, offset wewnętrzny/środkowy/zewnętrzny; A/B ten sam kontroler i początkowy fixture. To diagnostyczne początki nowych światów, nie podmienianie ciała w ciągłym biegu. Zapis całych śladów i następnej prostej. β45 osobno od granicy/kontaktu/numerical; poza obwiednią wymagane dodatnie ciągłe certyfikaty, energia/yaw/napęd/aktuator i brak odwrócenia ruchu. Nie jest to automatyczny PASS wyścigowy.
4. 3–5 stratnych odcinków z istniejących danych; kontrolowane przygotowania i ogony, bez uznawania ręcznej akcji za autonomiczną poprawę.
5. Jeden ograniczony pełny planner B dopiero po zgodności predyktor/native i testach kontaktu. Pierwszą porażkę diagnozujemy przed kolejnym native. Brak certyfikowanej kontynuacji = abstain, nie fikcyjna jazda.
6. Geometria: wyprowadzenie κ(t), C2-ramp i kompresji; dane źródłowe Motoareny i jawne braki. Czułości tylko diagnostycznie, oddzielne od A/B fizyki/sterownika.

Nie zakładamy, że poprawka konstytutywna zwiększa szybkość ani realizm. Materiał #140 ma kalibrację nominalnej krzywej, nie empiryczne Fx/Fy na szutrze. Zmienione znaczenie skal sztywności będzie opisane. Budżet/światy/prognozy zapisujemy także przy niepowodzeniu. Build/testy i renderowanie zapisanych wyników nie są nowymi eksperymentami fizycznymi.
