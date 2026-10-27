# #142 — Racing Objective & Artificial Penalty Ablation

**PARTIAL: pojedyncze preferencje szkodzą, ale sam zachłanny postęp nie wystarcza.**
Baza #141 `266df048536c256ced410a1045a5924899153237`. Oryginalne silnik, opony,
Box2D, masa/I, mu, tor i aktuator bez zmian. Reakcja 0,24 s, decyzje 0,2 s,
predyktor H09 i legalny zestaw akcji identyczne. Nowe polityki OFF; bez treningu.

## Co ograniczało zawodnika

Audyt 38 zapisanych obserwacji #136/#137 obejmuje 28 unikalnych stanów dwóch
rodzin, nie 28 niezależnych zawodników. Zero nowych prognoz/światów offline.
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
Równania, wagi #16015, klasyfikacje i wszystkie rankingi są w `results.json`.

## Ciągłe natywne A/B/C

A: oryginał. B: maksymalny postęp ze starymi klasami. C: maksymalny postęp,
odrzucający naruszenie obrysu, β45°, nieprawidłowy stan i ruch wstecz względem
toru, bez priorytetu Safe nad Uncertain. Każdy pełny zakres obejmuje badany łuk,
62 m, cały kolejny łuk i dalsze 62 m; D1/H1 również trzeci łuk. Bez resetów.

| Rodzina | A: pełny czas | B: stop natywny | C: brak dopuszczalnego H09 |
|---|---:|---|---:|
|D0|18,869s|2,350s — brak certyfikacji obrysu|1,610s|
|D1|28,771s|5,770s — ruch przeciwnie do toru|1,610s|
|H0|18,712s|2,040s — β45,408°|1,210s|
|H1|27,555s|2,240s — brak certyfikacji obrysu|1,210s|

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
przekroczenie w próbce. β45° jest nieskalibrowaną obwiednią eksperymentalną,
nie dowodem fizycznego upadku. Detektory natywne zachowano.

## Izolowane decyzje i przyczyna niepowodzeń

Z identycznego zaakceptowanego stanu/pending, po tym samym prefiksie A, zmieniono
jedną decyzję i przywrócono autonomiczne A przy następnej obserwacji:

- D1, gaz po 8 s, bez preferencji gazu częściowego: **28,771→28,191s**, poprawa
0,581 s, droga krótsza o 5,460 m; +40m po badanym drugim łuku **76,36→77,87 km/h**.
- D0, kierunek po 10,8 s: **18,869→18,841s**, poprawa 0,027 s, droga dłuższa o 0,732 m.

Oba pełne zakresy legalne; nie są to autonomiczne polityki z globalnie usuniętą
karą. Przy B wszystkie 4 próby pełne zawodzą. W D0 brak dopuszczalnego held-H09
występuje od 1,6 s, w D1 od 5,0 s. Przełączenia B→A po 1,2/1,6 s oraz 4,6/5,0 s nie
odzyskują legalnej kontynuacji; A od początku kończy. To dowód znaczenia wcześniejszej
strategii i ograniczenia H09, nie globalny certyfikat nieodwracalności ani dowód
konieczności konkretnych wag yaw. Źródła i ostatnie decyzje przy stopach zachowano.

## Rekomendacja i koszt

Pierwszy kandydat do usunięcia: preferencja częściowego gazu, z osobnym pełnym
autonomicznym A/B przed wdrożeniem. Zachować rzeczywiste ograniczenia i ocenę
dalszej legalnej kontynuacji. Nie uznawać yaw/kierunku, rezerwy 80% i marginesów za
fizykę; brak izolowanego dowodu, że wszystkie te konkretne kary są potrzebne.
Usunięcie ich naraz z krótkim horyzontem nie zastępuje skutecznej strategii.

**22/24 świata, 146,8125 s CPU epizodów/prognoz +3,25 s odczytów =150,0625/450 s;
26034 prognozy.** Wliczono przerwany świat 8,516 s: porównanie zegarów różniło się
o 7×10⁻¹⁵ s. Odczyt bez nowego świata udowodnił identyczność fizyki/pending;
poprawiono wyłącznie porównanie i policzono nową próbę oddzielnie. Kontakty tej
przerwanej próby nie zostały w pełni wyeksportowane; suma jest dolną granicą.
Protokół `a3341b9`, korekta `01ec8ba`, lock przed H0/H1 `c533227`; bez strojenia.
Łącznie 8 natywnych stopów zabezpieczeń (w tym 4 diagnostyczne późne przełączenia),
4 zatrzymania C i 1 przerwanie programistyczne; zero potwierdzonych przekroczeń
obrysu w zapisanych próbkach. Bez progu CPU pojedynczej decyzji.

Walidacja lokalna i końcowy standardowy Windows CI: `results.json` oraz PR.
Workflow i produkcja bez zmian; ciężkie audyty historyczne pomija polityka Draft.
Tylko te dwa raporty, bez dodatkowych materiałów prezentacyjnych.
