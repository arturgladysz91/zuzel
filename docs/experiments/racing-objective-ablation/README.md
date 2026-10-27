# #142 — Racing Objective & Artificial Penalty Ablation

**PARTIAL: pojedyncze preferencje szkodzą, ale sam zachłanny postęp nie wystarcza.**
Baza #141 `266df048536c256ced410a1045a5924899153237`. Oryginalne silnik, opony,
Box2D, masa/I, mu, tor i aktuator bez zmian. Reakcja 0,24 s, decyzje 0,2 s,
predyktor H09 i legalny zestaw akcji identyczne. Nowe polityki OFF; bez treningu.

## Co ograniczało zawodnika

Audyt 38 obserwacji #136/#137:28 unikalnych stanów dwóch rodzin; zero nowych
prognoz/światów offline. Nie są to 28 niezależne przypadki.
Usunięcie yaw zmienia 26/28 wyborów (głównie skręt), kierunku 16/28, preferencji
gazu 4/28 (gaz1 w 2 stanach). Usunięcie osobno kosztów skrętu, geometrii,
prześwitu i Unsafe-score nie zmienia wyboru w tej próbce. Nie dowodzi to ich
globalnej zbędności. Oryginalne A wybiera gaz0/częściowy/1: 4/24/0; B/C: 0/0/28.

Yaw i kierunek są preferencjami wobec przyszłej stycznej/krzywizny, nie prawami
fizyki ani celem β=0. Kara gaz1 wynosi 1,307 punktu za odejście od preferowanych
31,82%. Kara zmiany skrętu działa obok rzeczywistego aktuatora. Obwiednia
`sqrt(0.8*capacity/curvature)` ma 20% heurystycznej rezerwy; nie jest prawem opony.
Track Awareness jest OFF w A; po włączeniu zastępuje koszt geometrii, jego delta
może być ujemna. Enabled-vs-removed zmienia 1/18 pełnych zapisanych pul.
Równania, wagi #16015 i rankingi są w `results.json`.

## Ciągłe natywne A/B/C

A: oryginał. B: maksymalny postęp ze starymi klasami. C: maksymalny postęp,
odrzucający naruszenie obrysu, β45°, nieprawidłowy stan i ruch wstecz względem
toru, bez priorytetu Safe nad Uncertain. Każdy pełny zakres obejmuje badany łuk,
62 m, cały kolejny łuk i dalsze 62 m; D1/H1 również trzeci łuk. Bez resetów.

| Rodzina | A: pełny czas | B: stop natywny | C: brak dopuszczalnego H09 |
|---|---:|---|---:|
|D0|18,869 s|2,350 s — brak certyfikacji obrysu|1,610 s|
|D1|28,771 s|5,770 s — reverse / cofnięcie postępu|1,610 s|
|H0|18,712 s|2,040 s — β45,408°|1,210 s|
|H1|27,555 s|2,240 s — brak certyfikacji obrysu|1,210 s|

Legalne ukończenia: **A 4/4, B 0/4, C 0/4**. B używa znacznie więcej gazu1,
ale nie dociera do badanych wyjść/prostych. Brakujące prędkości i pełne czasy
pozostają null. Dla A prędkość przy +40 m wynosi 75,95/76,36/78,39/75,81 km/h.
Wszystkie prędkości głównego porównania mierzone są w tych samych przekrojach,
z jawną interpolacją skalarnych metryk między zaakceptowanymi próbkami 0,01 s.

**Rozróżnienie bezpieczeństwa:** Uncertain to margines 0,2 m/0,1 m, nie DNF.
C rzeczywiście wybiera szybciej/mocniej zamiast Safe po 0,8–1,2 s, gdy prognoza
nie przekracza jeszcze twardych granic; następnie wyczerpuje skończony zbiór
dopuszczalnych prognoz. Jego cztery zatrzymania nie są czterema rzeczywistymi
wypadnięciami. D0/H1 B mają nadal dodatnie chwilowe prześwity; ujemna jest
konserwatywna dolna granica obrysu w ruchu. To brak certyfikacji, nie potwierdzone
przekroczenie w próbce. Dwie dodatkowe diagnostyki z istniejącym dokładniejszym
testem obrysu, przy identycznym prefiksie decyzji B, potwierdzają jednak przekroczenie
po kolejnych 0,02 s: D0 po 2,370 s (−0,060 m), H1 po 2,260 s (−0,030 m).
Nie są niezależnymi rodzinami. β45° jest nieskalibrowaną obwiednią eksperymentalną,
nie dowodem fizycznego upadku. Detektory natywne zachowano.

## Izolowane decyzje i przyczyna niepowodzeń

Po identycznym prefiksie, z identycznym stanem/pending, zmieniono jedną decyzję,
następnie przywrócono autonomiczne A:

- D1, gaz po 8 s, bez preferencji gazu częściowego: **28,771→28,191 s**, poprawa
0,581 s, droga krótsza o 5,460 m; +40 m po badanym drugim łuku **76,36→77,87 km/h**.
- D0, kierunek po 10,8 s: **18,869→18,841 s**, poprawa 0,027 s, droga dłuższa o 0,732 m.

Obie próby legalne; nie dowodzą korzyści z globalnego usunięcia danej kary.
Przy B wszystkie 4 pełne próby zawodzą. W D0 brak dopuszczalnego held-H09
występuje od 1,6 s, w D1 od 5,0 s. Przełączenia B→A po 1,2/1,6 s oraz 4,6/5,0 s nie
odzyskują legalnej kontynuacji; A od początku kończy. To dowód znaczenia wcześniejszej
strategii i ograniczenia H09, nie globalny certyfikat nieodwracalności ani dowód
konieczności konkretnych wag yaw. Źródła i ostatnie decyzje przy stopach zachowano.

## Rekomendacja i koszt

Najpierw sprawdzić globalne usunięcie preferencji częściowego gazu w osobnym
autonomicznym A/B. Zachować ograniczenia i ocenę dalszej legalnej kontynuacji.
Nie traktować yaw/kierunku, rezerwy 80% i marginesów jako fizyki: nie wykazano
konieczności wszystkich tych kar. Sam krótkoterminowy postęp nie wystarcza.

**24/24 światy, 152,8125 s CPU epizodów/prognoz +3,25 s odczytów =156,0625/450 s;
26682 prognozy.** Wliczono przerwany świat 8,516 s: błąd porównania zegara 7×10⁻¹⁵ s
skorygowano po dowodzie identyczności stanu/pending, bez zmiany sterownika.
Kontakty przerwanej próby są niepełne; suma jest dolną granicą.
Protokół `a3341b9`, korekta `01ec8ba`, lock H0/H1 `c533227`, dodatkowe diagnostyki
`71b2155`; bez strojenia. Łącznie 10 stopów zabezpieczeń
(w tym 4 późne przełączenia i 2 powtórzenia diagnostyczne), 4 zatrzymania C i 1
przerwanie programistyczne; 2 potwierdzone przekroczenia obrysu w dodatkowych
diagnostykach. Bez progu CPU pojedynczej decyzji.

Walidacja lokalna i standardowy Windows CI: `results.json` oraz PR.
Workflow i produkcja bez zmian; ciężkie audyty historyczne pomija polityka Draft.
Tylko te dwa raporty, bez dodatkowych materiałów prezentacyjnych.
