# #142 — Racing Objective & Artificial Penalty Ablation

PROVISIONAL; nowe polityki domyślnie OFF. Dokładna baza #141:
`266df048536c256ced410a1045a5924899153237`. Oryginalna fizyka, opony, silnik,
Box2D, geometria, reakcja0,24s, decyzje0,2s i aktuator pozostają bez zmian.

Audyt offline:38 zapisanych obserwacji #136/#137,28 unikalnych stanów dwóch rodzin;
zero nowych prognoz i światów. Wynik A: gaz0/częściowy/1 =4/24/0. B i C:0/0/28.
Bez yaw zmienia się26/28 akcji, bez kierunku16/28, bez preferencji gazu4/28
(w2 wybór gaz1). Usunięcie pozostałych pojedynczych składników nie zmienia wyboru
w tej próbce. Nie jest to dowód legalnej poprawy czasu. Wyniki Track Awareness
dotyczą osobno jego włączenia: A ma je OFF; enabled-vs-removed zmienia1/18 pełnych
zapisanych pul. Jest to zastąpienie kosztu geometrii, nie stały dodatkowy koszt.

Przed światami zamrożono A (oryginał), B (postęp i stare klasy), C (postęp i tylko
przewidywane rzeczywiste naruszenia/nieprawidłowe stany plus zachowane β45°).
`Uncertain` jest marginesem ostrożności, nie DNF. C nie daje mu niższego priorytetu,
ale prognozowanej dopuszczalności nie nazywamy certyfikatem natywnym. Brak
dopuszczalnej prognozy C zatrzymuje jako osobną nieosiągalność kontrolera.

Plan: krótki preflight A/B/C, pełne diagnostyczne D0/D1, dwie pojedyncze ablacje
z identycznego historycznego stanu i pending (bez resetów, prefiks odtworzony
w tym samym świecie), lock przed H0/H1 i do4 ograniczonych późnych przełączeń
celu przy DNF. Oryginalne pełne A jest wspólnym odpowiednikiem izolowanej akcji;
nie liczymy aliasów i powtórzonych prefiksów jako nowych niezależnych dowodów.

Pełny zakres: badany łuk,62m prostej, kolejny łuk i dalsze62m; D1/H1 obejmują
trzeci łuk. Prędkości/czasy w tych samych przekrojach referencyjnych, z jawnymi
natywnymi próbkami otaczającymi punkt pomiaru. Maksymalnie24 światy/450s CPU;
bez progu CPU pojedynczej decyzji, CEM, nowych prognoz odzyskania lub strojenia.

Wyniki natywne i końcowy werdykt: w trakcie. Jedynymi raportami są ten README
i `results.json`; kod i testy pozostają w PR.
