# #141 — Autonomous acceleration & next-corner preparation

**INCONCLUSIVE — zatrzymano po preflight CPU.** Jeden prototyp, domyślnie OFF.
Baza dokładnie #140 `932fc33ba20a8c96c2cd1a102957c9b297d412ea`.
Oryginalne silnik, opony, Box2D, masa/I, mu, tor, ograniczenia i polityka A #16015
pozostają niezmienione. Nie użyto nowego solvera #140, CEM, treningu ani strojenia wag.

## Dlaczego dotychczasowy zawodnik ograniczał gaz

Audyt 18 zapisanych obserwacji #137, przed nowymi światami, wykazał kilka przyczyn.
Kara za gaz1 wynosi 1,307 punktu i czasem przesądza o gazie częściowym. Samo jej
usunięcie daje gaz1 tylko w 2/18 stanów. Po dodatkowym zastąpieniu końcowego kosztu
yaw istniejącym kosztem średnim: 3/18. W innych stanach dominują yaw, kierunek
i geometria. H09 obejmuje kilkanaście–dwadzieścia metrów, nie całe 62 m i następny łuk.

W historycznych przejazdach redukcja aktywowała się po 0,24 s i 4,996–5,185 m
fizycznej drogi; żądano jej około 26,5–30,7 m przed łukiem. Dane D to #139C po
interwencji, H to autonomiczna historia #137 — nie jedna wspólna próba A.
W planach #137 często zmieniano niewysłaną drugą akcję; wysłanych komend z kolejki
nie anulowano. Rozkłady punktowe, źródła i hashe znajdują się w `results.json`.
Koszt geometrii wyliczono jako resztę zapisanego wyniku, nie niezależną walidację.

## Jedna zamrożona zmiana

B usuwa preferencję gazu częściowego i używa średniego kosztu yaw przy pozostałych
wagach A. Z istniejących akcji wybiera najlepszą dla każdego gazu oraz zachowuje
oryginalną akcję A jako fallback. Dla najwyżej 4 kandydatów dodaje po jednym 0,9 s
fizycznym sprawdzianie odzyskania kontroli po H09: gaz0 i kierownica z istniejącej
inwersji sił kontaktu. Kolejka, reakcja i aktuator pozostają rzeczywiste.

Poza H1.8 przygotowanie jest **niecertyfikowanym oszacowaniem**: używa osiągniętego
offsetu, zmierzonego opóźnienia toczenia, geometrii oraz czasu reakcji, ruchu
kierownicy i odpowiedzi bocznej/yaw. Promocja wymaga bezpiecznych obu krótkich
prognoz i nie gorszego residualu prędkości niż A. Wysyłana jest tylko pierwsza
akcja; następna obserwacja ponownie ocenia stan i nienaruszalną kolejkę pending.
Nie ma harmonogramu gazu, trajektorii zadanej ani przyszłego stanu natywnego.

Protokół zamrożono w `0920124`: wszystkie 4 legalne tam, gdzie A legalne; brak nowych
DNF; w przynajmniej 2 fizycznie różnych rodzinach ≥3 km/h przy +40 m oraz ≥0,10 s
poprawy pełnego czasu. Limit średniego CPU B/A ≤1,5 i B ≤0,200 s/decyzję.
Pełny zakres miał obejmować kolejny łuk i dalsze 62 m, dla D1/H1 także trzeci łuk.
D0/D1 diagnostyczne, H0/H1 potwierdzające po osobnym locku. Nie uruchomiono ich
po niezaliczeniu preflight; nie dostrajano prototypu.

## Wyniki i ograniczenia

| Pomiar D0, ciągłe 3 s | A #134 | B #141 |
|---|---:|---:|
| Koniec próby, km/h |62,058|60,973|
| Postęp referencyjny, m |53,359|51,911|
| Maks. β |8,606°|6,921°|
| Min. certyfikowany prześwit, m |0,117|0,205|
| CPU/decyzję, s |0,167708|**0,205208**|
| Prognozy |405|451|
| Nowe żądania gaz1 |0|0|
| DNF obserwowane |0|0|

Oba preflighty legalne; B zmienił 4/15 decyzji. Gaz1 przez 8% czasu pochodzi wyłącznie
z identycznego stanu początkowego do aktywacji pierwszej komendy. Względny koszt
1,224× przechodzi, bezwzględny limit 0,200 s nie przechodzi. Pomiar obejmuje zimny
start/JIT i skwantowany czas procesu; nie jest benchmarkiem ustalonej pracy czterech
zawodników. Zamrożonego progu nie zmieniono ani nie powtarzano próby.

Obie próby kończą się **wewnątrz pierwszego łuku**. Prędkości tabeli są po tym samym
czasie, na różnych dystansach. Wyjście, +20/+40/+62m, pełny porównywalny czas,
redukcja przed następnym łukiem i jego koszt prędkości pozostają **niezmierzone**.
Nie wykazano poprawy wykorzystania mocy ani możliwości bezpiecznego całego przejazdu.

OFF zachował dokładny hash całego prefiksu historii po normalizacji wyłącznie
adnotacji komendy na końcowej próbce: dłuższy stary zapis zdążył dopisać decyzję 3,00 s,
a krótki kończy się przed nią. Korektę porównania `e60a073` wykonano bez powtórki
świata i bez zmiany fizyki, sterownika lub progów; zachowano oba hashe i przyczynę.
Aktywacje 0,24 s, przeglądy 0,2 s, energia, yaw i aktuator przeszły sprawdzenia.

**2/20 światów; 6,71875 s CPU epizodów/prognoz/odczytów i podsumowań +5 s audytu
=11,71875/400 s łącznie.** Wszystkie 856 prognoz wliczone. Zero pełnych prób A/B,
zero uruchomień H0/H1. Tylko dwa pliki raportowe; brak nowych materiałów prezentacyjnych.

Walidacja lokalna: restore/build bez ostrzeżeń, C# 2433/2433, Python 237+7,
Node 61+4, discovery 2954 bez luk/duplikatów, 39 zamrożonych konsumentów i hashe
źródeł #140 PASS. Standardowy Draft Windows CI dla końcowego HEAD: wynik w PR;
konfiguracja i testy w `results.json`. Workflow bez zmian; ciężkie historyczne audyty pomija
standardowa polityka Draft. Kolejny krok: osobno zamrozić metodę pomiaru CPU
i ograniczyć powtarzane przeglądanie punktów/geometrii prognoz, następnie wrócić do
ciągłych prób A/B. Prototyp pozostaje OFF, fizyka bez zmian; pytanie o osiągi pozostaje otwarte.
